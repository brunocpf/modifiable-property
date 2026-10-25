using System;
using System.Collections.Generic;
using System.Linq;
using BrunoCPF.Modifiable.Common.Math;
using BrunoCPF.Modifiable.Core;
using R3;

namespace BrunoCPF.Modifiable.Common.Properties
{
    /// <summary>
    /// Reactive value that accepts deltas, applies filters and modifiers, and enforces bounds.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <typeparam name="TContext">The context carried with each delta.</typeparam>
    /// <typeparam name="TSource">
    /// What modifiers and filters come from (a status, item, skill, …). Used for attribution and
    /// <see cref="RemoveModifiers"/> / <see cref="RemoveFilters"/>; it doesn't have to be unique.
    /// </typeparam>
    /// <remarks>
    /// This is a reactive wrapper over the pure core in <see cref="BrunoCPF.Modifiable.Core"/>. It accepts
    /// both closure-free data entries (<see cref="Modifier{TSource}"/>, <see cref="DeltaFilter{TSource}"/>)
    /// and functions, in one priority order. Entries have no ids: remove one with the handle returned by
    /// <c>Push…</c>, or all of a source's entries with <c>Remove…(source)</c>.
    /// </remarks>
    public class ModifiableProperty<TValue, TContext, TSource>
        : Observable<TValue>, IReadOnlyModifiableProperty<TValue, TContext>, IDisposable where TValue : struct, IComparable
    {
        private readonly ReactiveProperty<TValue> _base;
        private readonly Subject<ValueDelta<TValue, TContext>> _processedDeltas = new();
        private readonly Subject<DeltaResult<TValue, TSource>> _appliedDeltas = new();
        private readonly ReactiveProperty<IReadOnlyList<ModifierEntry<TValue, TSource>>> _modifiers =
            new(Array.Empty<ModifierEntry<TValue, TSource>>());
        private List<FilterEntry> _filters = new();
        private readonly ValueBounds<TValue> _bounds;
        private readonly IValueMath<TValue>? _valueMath;
        private readonly IScalableValueMath<TValue>? _scalableMath;
        private readonly CompositeDisposable _disposables = new();
        private Func<string, StackPolicy>? _stacking;
        private CustomOperations? _operations;
        private bool _disposed;

        /// <summary>
        /// Current effective value after modifiers are applied.
        /// </summary>
        public TValue CurrentValue => Effective.CurrentValue;

        /// <summary>
        /// Stream of processed deltas after filters are applied.
        /// </summary>
        public Observable<ValueDelta<TValue, TContext>> ProcessedDeltas => _processedDeltas;

        /// <summary>
        /// Stream of every applied delta's full outcome: requested, filtered and applied amounts, and the
        /// filters that changed it with their sources. Emits after <see cref="Base"/> has updated.
        /// </summary>
        public Observable<DeltaResult<TValue, TSource>> AppliedDeltas => _appliedDeltas;

        /// <summary>
        /// Base subject before modifiers are applied.
        /// </summary>
        public ReadOnlyReactiveProperty<TValue> Base { get; }

        /// <summary>
        /// Final effective subject after modifiers are applied.
        /// </summary>
        public ReadOnlyReactiveProperty<TValue> Effective { get; }

        /// <summary>
        /// Observable list of installed modifiers in evaluation order. Emits whenever it changes.
        /// </summary>
        public Observable<IReadOnlyList<ModifierEntry<TValue, TSource>>> Modifiers => _modifiers;

        /// <summary>
        /// Policy per <see cref="Modifier{TSource}.StackGroup"/>; groups stack by default. Changing it
        /// re-evaluates <see cref="Effective"/>.
        /// </summary>
        public Func<string, StackPolicy>? Stacking
        {
            get => _stacking;
            set
            {
                ThrowIfDisposed();
                _stacking = value;
                _modifiers.Value = _modifiers.Value.ToArray();
            }
        }

        /// <summary>
        /// Registry for <see cref="ModifierOperation.Custom"/> modifiers and <see cref="DeltaOperation.Custom"/>
        /// filters. Set it before adding custom entries; changing it re-evaluates <see cref="Effective"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The new registry is missing an operation that a current custom entry uses.
        /// </exception>
        public CustomOperations? Operations
        {
            get => _operations;
            set
            {
                ThrowIfDisposed();
                foreach (ModifierEntry<TValue, TSource> entry in _modifiers.Value)
                {
                    RequireCustomOperation(value, entry.Data?.CustomName, isModifier: true);
                }

                foreach (FilterEntry entry in _filters)
                {
                    RequireCustomOperation(value, entry.Data?.CustomName, isModifier: false);
                }

                _operations = value;
                _modifiers.Value = _modifiers.Value.ToArray();
            }
        }

        /// <summary>
        /// Creates an unbounded modifiable property.
        /// </summary>
        public ModifiableProperty(TValue initialValue)
            : this(initialValue, bounds: null)
        { }

        /// <summary>
        /// Creates a modifiable property with optional bounds and math helper.
        /// </summary>
        public ModifiableProperty(
            TValue initialValue,
            ValueBounds<TValue>? bounds = null,
            IValueMath<TValue>? valueMath = null)
        {
            _bounds = bounds ?? ValueBounds<TValue>.Unbounded;
            _valueMath = valueMath ?? ValueMath.GetValueMath<TValue>();
            _scalableMath = _valueMath as IScalableValueMath<TValue>;

            _base = new ReactiveProperty<TValue>(_bounds.Clamp(initialValue));

            Base = _base.ToReadOnlyReactiveProperty().AddTo(_disposables);

            Effective = _base
                .CombineLatest(_modifiers, (baseValue, modifiers) => ApplyModifiers(baseValue, modifiers, steps: null))
                .ToReadOnlyReactiveProperty()
                .AddTo(_disposables);
        }

        /// <summary>
        /// Creates a modifiable property with an optional inclusive <paramref name="min"/> and
        /// <paramref name="max"/>; an omitted side is unbounded.
        /// </summary>
        public ModifiableProperty(
            TValue initialValue,
            TValue? min = null,
            TValue? max = null,
            IValueMath<TValue>? valueMath = null)
            : this(
                initialValue,
                new ValueBounds<TValue>(min.HasValue, min.GetValueOrDefault(), max.HasValue, max.GetValueOrDefault()),
                valueMath)
        { }

        /// <summary>
        /// Inserts <paramref name="item"/> after every entry of equal or lower priority, keeping the list
        /// priority-ordered and stable (equal priorities stay in insertion order).
        /// </summary>
        private static void InsertByPriority<T>(List<T> list, T item, Func<T, int> priority)
        {
            int itemPriority = priority(item);
            int index = list.Count;
            while (index > 0 && priority(list[index - 1]) > itemPriority)
            {
                index--;
            }

            list.Insert(index, item);
        }

        private TValue ApplyBounds(TValue currentValue, TValue delta)
        {
            TValue newValue;
            if (_valueMath != null)
            {
                newValue = _valueMath.Add(currentValue, delta);
            }
            else
            {
                newValue = delta;
            }

            var boundedValue = _bounds.Clamp(newValue);

            return boundedValue;
        }

        private TValue ApplyModifiers(
            TValue baseValue,
            IReadOnlyList<ModifierEntry<TValue, TSource>> modifiers,
            List<ModifierStep<TValue, TSource>>? steps)
        {
            bool[] suppressed = ModifierStack.FindSuppressed(
                modifiers.Select(static entry => entry.Data).ToArray(),
                _stacking);

            var modifiedValue = baseValue;

            for (int index = 0; index < modifiers.Count; index++)
            {
                ModifierEntry<TValue, TSource> entry = modifiers[index];
                TValue before = modifiedValue;

                if (entry.Function is not null)
                {
                    modifiedValue = entry.Function(modifiedValue);
                }
                else if (!suppressed[index])
                {
                    // Data modifiers are only accepted when scalable math (and, for custom ones, a
                    // registered operation) exists; see PushModifier and Operations.
                    modifiedValue = ModifierMath.Apply(modifiedValue, entry.Data!, _scalableMath!, _operations);
                }

                steps?.Add(new ModifierStep<TValue, TSource>(
                    entry.Priority, before, modifiedValue, suppressed[index], entry.Source, entry.Data));
            }

            return modifiedValue;
        }

        /// <summary>
        /// Injects a delta with context into the property.
        /// </summary>
        public void AddDelta(ValueDelta<TValue, TContext> delta)
        {
            ThrowIfDisposed();

            TValue before = _base.Value;
            var fired = new List<DeltaStep<TValue, TSource>>();
            var filtered = delta;

            foreach (var entry in _filters)
            {
                ValueDelta<TValue, TContext> next = entry.Function is not null
                    ? entry.Function(filtered)
                    : new ValueDelta<TValue, TContext>(
                        DeltaPipeline.ApplyFilter(filtered.Delta, entry.Data!, _scalableMath!, _operations),
                        filtered.Context);

                if (!EqualityComparer<TValue>.Default.Equals(next.Delta, filtered.Delta))
                {
                    fired.Add(new DeltaStep<TValue, TSource>(entry.Priority, filtered.Delta, next.Delta, entry.Source, entry.Data));
                }

                filtered = next;
            }

            TValue after = ApplyBounds(before, filtered.Delta);
            _base.Value = after;

            _processedDeltas.OnNext(filtered);
            _appliedDeltas.OnNext(new DeltaResult<TValue, TSource>(
                before,
                delta.Delta,
                filtered.Delta,
                after,
                _valueMath != null ? _valueMath.Subtract(after, before) : after,
                fired));
        }

        /// <summary>
        /// Injects a delta with context into the property.
        /// </summary>
        public void AddDelta(TValue delta, TContext? context) => AddDelta(new ValueDelta<TValue, TContext>(delta, context));

        /// <summary>
        /// Injects a delta without context into the property.
        /// </summary>
        public void AddDelta(TValue delta) => AddDelta(new ValueDelta<TValue, TContext>(delta, default));

        /// <summary>
        /// Directly set the base value by injecting the required delta.
        /// </summary>
        public void SetBaseValue(TValue newValue)
        {
            var current = Base.CurrentValue;

            TValue delta;
            if (_valueMath != null)
            {
                delta = _valueMath.Subtract(newValue, current);
            }
            else
            {
                delta = newValue;
            }

            AddDelta(delta);
        }

        /// <summary>
        /// Returns a step-by-step account of how the installed modifiers produce <see cref="CurrentValue"/>
        /// from the current base value. Function entries appear with a null
        /// <see cref="ModifierStep{TValue, TSource}.Data"/>.
        /// </summary>
        public ModifierBreakdown<TValue, TSource> Explain()
        {
            ThrowIfDisposed();
            var steps = new List<ModifierStep<TValue, TSource>>();
            TValue baseValue = _base.Value;
            TValue result = ApplyModifiers(baseValue, _modifiers.Value, steps);
            return new ModifierBreakdown<TValue, TSource>(baseValue, result, steps);
        }

        /// <summary>
        /// Installs a data modifier after every modifier of equal or lower priority. Dispose the returned
        /// handle to remove exactly this entry.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// <typeparamref name="TValue"/> has no <see cref="IScalableValueMath{TValue}"/>, or a custom
        /// modifier's operation isn't registered in <see cref="Operations"/>.
        /// </exception>
        public IDisposable PushModifier(Modifier<TSource> modifier)
        {
            if (modifier is null)
            {
                throw new ArgumentNullException(nameof(modifier));
            }

            RequireScalableMath();
            RequireCustomOperation(_operations, modifier.CustomName, isModifier: true);
            return Install(new ModifierEntry<TValue, TSource>(modifier));
        }

        /// <summary>
        /// Installs a function modifier after every modifier of equal or lower priority. Dispose the
        /// returned handle to remove exactly this entry.
        /// </summary>
        public IDisposable PushModifier(Func<TValue, TValue> modify, int priority = 0, TSource? source = default)
        {
            if (modify is null)
            {
                throw new ArgumentNullException(nameof(modify));
            }

            return Install(new ModifierEntry<TValue, TSource>(modify, priority, source));
        }

        /// <summary>
        /// Removes every modifier whose source equals <paramref name="source"/>.
        /// </summary>
        /// <returns>The number of modifiers removed.</returns>
        public int RemoveModifiers(TSource source)
        {
            // No-op once disposed: removal is cleanup that routinely runs during teardown.
            if (_disposed)
            {
                return 0;
            }

            var kept = _modifiers.Value.Where(entry => !EqualityComparer<TSource>.Default.Equals(entry.Source!, source)).ToArray();
            int removed = _modifiers.Value.Count - kept.Length;
            if (removed > 0)
            {
                _modifiers.Value = kept;
            }

            return removed;
        }

        /// <summary>
        /// Removes every modifier.
        /// </summary>
        public void ClearModifiers()
        {
            if (_disposed || _modifiers.Value.Count == 0)
            {
                return;
            }

            _modifiers.Value = Array.Empty<ModifierEntry<TValue, TSource>>();
        }

        /// <summary>
        /// Installs a data filter after every filter of equal or lower priority. Dispose the returned
        /// handle to remove exactly this entry.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// <typeparamref name="TValue"/> has no <see cref="IScalableValueMath{TValue}"/>, or a custom
        /// filter's operation isn't registered in <see cref="Operations"/>.
        /// </exception>
        public IDisposable PushFilter(DeltaFilter<TSource> filter)
        {
            if (filter is null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            RequireScalableMath();
            RequireCustomOperation(_operations, filter.CustomName, isModifier: false);
            return Install(new FilterEntry(filter.Priority, filter.Source, filter, null));
        }

        /// <summary>
        /// Installs a function filter after every filter of equal or lower priority. Dispose the returned
        /// handle to remove exactly this entry.
        /// </summary>
        public IDisposable PushFilter(
            Func<ValueDelta<TValue, TContext>, ValueDelta<TValue, TContext>> filter,
            int priority = 0,
            TSource? source = default)
        {
            if (filter is null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            return Install(new FilterEntry(priority, source, null, filter));
        }

        /// <summary>
        /// Removes every filter whose source equals <paramref name="source"/>.
        /// </summary>
        /// <returns>The number of filters removed.</returns>
        public int RemoveFilters(TSource source)
        {
            // No-op once disposed; see RemoveModifiers.
            if (_disposed)
            {
                return 0;
            }

            int before = _filters.Count;
            _filters = _filters.Where(entry => !EqualityComparer<TSource>.Default.Equals(entry.Source!, source)).ToList();
            return before - _filters.Count;
        }

        /// <summary>
        /// Removes every filter.
        /// </summary>
        public void ClearFilters()
        {
            // No-op once disposed; see RemoveModifiers.
            if (_disposed)
            {
                return;
            }

            _filters = new List<FilterEntry>();
        }

        /// <summary>
        /// Disposes subscriptions and internal observables.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _disposables.Dispose();
            _processedDeltas.Dispose();
            _appliedDeltas.Dispose();
            _base.Dispose();
            _modifiers.Dispose();
        }

        /// <summary>
        /// Subscribes observers to the effective value stream.
        /// </summary>
        protected override IDisposable SubscribeCore(Observer<TValue> observer) => Effective.Subscribe(
            observer.OnNext,
            observer.OnErrorResume,
            observer.OnCompleted
        );

        /// <summary>
        /// Subscribes observers to the effective value stream.
        /// </summary>
        public IDisposable Subscribe(IObserver<TValue> observer) => this.AsSystemObservable().Subscribe(observer);

        /// <summary>
        /// Returns a read-only view over this property. The view forwards reads and the value stream
        /// but exposes no mutating members, and — unlike returning the property itself typed as
        /// <see cref="IReadOnlyModifiableProperty{TValue, TContext}"/> — it cannot be cast back to
        /// <see cref="ModifiableProperty{TValue, TContext, TSource}"/> to regain write access. A new
        /// lightweight view is returned per call; cache it if you need a stable reference.
        /// </summary>
        public IReadOnlyModifiableProperty<TValue, TContext> AsReadOnly() => new ReadOnlyView(this);

        private IDisposable Install(ModifierEntry<TValue, TSource> entry)
        {
            ThrowIfDisposed();
            List<ModifierEntry<TValue, TSource>> modifiers = _modifiers.Value.ToList();
            InsertByPriority(modifiers, entry, static m => m.Priority);
            _modifiers.Value = modifiers;
            return Disposable.Create(() => Uninstall(entry));
        }

        private void Uninstall(ModifierEntry<TValue, TSource> entry)
        {
            // No-op once disposed; see RemoveModifiers.
            if (_disposed)
            {
                return;
            }

            var kept = _modifiers.Value.Where(existing => !ReferenceEquals(existing, entry)).ToArray();
            if (kept.Length != _modifiers.Value.Count)
            {
                _modifiers.Value = kept;
            }
        }

        private IDisposable Install(FilterEntry entry)
        {
            ThrowIfDisposed();
            List<FilterEntry> filters = _filters.ToList();
            InsertByPriority(filters, entry, static f => f.Priority);
            _filters = filters;
            return Disposable.Create(() => Uninstall(entry));
        }

        private void Uninstall(FilterEntry entry)
        {
            // No-op once disposed; see RemoveModifiers.
            if (_disposed)
            {
                return;
            }

            _filters = _filters.Where(existing => !ReferenceEquals(existing, entry)).ToList();
        }

        private void RequireScalableMath()
        {
            if (_scalableMath is null)
            {
                throw new InvalidOperationException(
                    $"Data modifiers and filters need an {nameof(IScalableValueMath<TValue>)} for {typeof(TValue).Name}.");
            }
        }

        private static void RequireCustomOperation(CustomOperations? operations, string? customName, bool isModifier)
        {
            if (customName is null)
            {
                return;
            }

            bool registered = operations is not null
                && (isModifier ? operations.HasModifierOperation(customName) : operations.HasDeltaOperation(customName));
            if (!registered)
            {
                throw new InvalidOperationException(
                    $"Custom {(isModifier ? "modifier" : "delta")} operation '{customName}' is not registered in {nameof(Operations)}.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        /// <summary>
        /// One installed filter: either data or a function, never both. Reference identity.
        /// </summary>
        private sealed class FilterEntry
        {
            public FilterEntry(
                int priority,
                TSource? source,
                DeltaFilter<TSource>? data,
                Func<ValueDelta<TValue, TContext>, ValueDelta<TValue, TContext>>? function)
            {
                Priority = priority;
                Source = source;
                Data = data;
                Function = function;
            }

            public int Priority { get; }
            public TSource? Source { get; }
            public DeltaFilter<TSource>? Data { get; }
            public Func<ValueDelta<TValue, TContext>, ValueDelta<TValue, TContext>>? Function { get; }
        }

        /// <summary>
        /// Private read-only adapter. Being private and not deriving from
        /// <see cref="ModifiableProperty{TValue, TContext, TSource}"/>, no consumer downcast can reach the
        /// writable API.
        /// </summary>
        private sealed class ReadOnlyView : IReadOnlyModifiableProperty<TValue, TContext>
        {
            private readonly ModifiableProperty<TValue, TContext, TSource> _source;

            public ReadOnlyView(ModifiableProperty<TValue, TContext, TSource> source) => _source = source;

            public TValue CurrentValue => _source.CurrentValue;

            public ReadOnlyReactiveProperty<TValue> Base => _source.Base;

            public Observable<ValueDelta<TValue, TContext>> ProcessedDeltas => _source.ProcessedDeltas;

            public IDisposable Subscribe(IObserver<TValue> observer) => _source.Subscribe(observer);
        }
    }

    /// <summary>
    /// A <see cref="ModifiableProperty{TValue, TContext, TSource}"/> whose modifier and filter sources are
    /// untyped (<see cref="object"/>), for when you don't need typed attribution.
    /// </summary>
    public sealed class ModifiableProperty<TValue, TContext> : ModifiableProperty<TValue, TContext, object>
        where TValue : struct, IComparable
    {
        /// <summary>
        /// Creates an unbounded modifiable property.
        /// </summary>
        public ModifiableProperty(TValue initialValue)
            : base(initialValue)
        { }

        /// <summary>
        /// Creates a modifiable property with optional bounds and math helper.
        /// </summary>
        public ModifiableProperty(TValue initialValue, ValueBounds<TValue>? bounds = null, IValueMath<TValue>? valueMath = null)
            : base(initialValue, bounds, valueMath)
        { }

        /// <summary>
        /// Creates a modifiable property with an optional inclusive <paramref name="min"/> and
        /// <paramref name="max"/>; an omitted side is unbounded.
        /// </summary>
        public ModifiableProperty(TValue initialValue, TValue? min = null, TValue? max = null, IValueMath<TValue>? valueMath = null)
            : base(initialValue, min, max, valueMath)
        { }
    }
}
