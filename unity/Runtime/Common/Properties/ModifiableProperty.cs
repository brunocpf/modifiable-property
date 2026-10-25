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
    /// <remarks>
    /// This is a reactive wrapper over the pure core in <see cref="BrunoCPF.Modifiable.Core"/>. It accepts
    /// both closure-free data entries (<see cref="Modifier"/>, <see cref="DeltaFilter"/>) and the
    /// function-based <see cref="ValueModifier{TValue}"/> / <see cref="ValueDeltaFilter{TValue, TContext}"/>.
    /// Both kinds share one id space and one priority order.
    /// </remarks>
    public sealed class ModifiableProperty<TValue, TContext>
        : Observable<TValue>, IReadOnlyModifiableProperty<TValue, TContext>, IDisposable where TValue : struct, IComparable
    {
        private readonly ReactiveProperty<TValue> _base;
        private readonly Subject<ValueDelta<TValue, TContext>> _processedDeltas = new();
        private readonly Subject<DeltaResult<TValue>> _appliedDeltas = new();
        private readonly ReactiveProperty<IReadOnlyList<ModifierSlot>> _modifiers;
        private List<FilterSlot> _filters;
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
        /// ids of the filters that changed it. Emits after <see cref="Base"/> has updated.
        /// </summary>
        public Observable<DeltaResult<TValue>> AppliedDeltas => _appliedDeltas;

        /// <summary>
        /// Base subject before modifiers are applied.
        /// </summary>
        public ReadOnlyReactiveProperty<TValue> Base { get; }

        /// <summary>
        /// Final effective subject after modifiers are applied.
        /// </summary>
        public ReadOnlyReactiveProperty<TValue> Effective { get; }

        /// <summary>
        /// Observable list of current function-based modifiers. Emits whenever any modifier changes.
        /// </summary>
        public Observable<IReadOnlyList<ValueModifier<TValue>>> Modifiers { get; }

        /// <summary>
        /// Observable list of current data modifiers. Emits whenever any modifier changes.
        /// </summary>
        public Observable<IReadOnlyList<Modifier>> DataModifiers { get; }

        /// <summary>
        /// Policy per <see cref="Modifier.StackGroup"/>; groups stack by default. Changing it re-evaluates
        /// <see cref="Effective"/>.
        /// </summary>
        public Func<string, StackPolicy>? Stacking
        {
            get => _stacking;
            set
            {
                ThrowIfDisposed();
                _stacking = value;
                _modifiers.Value = _modifiers.Value.ToList();
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
                foreach (ModifierSlot slot in _modifiers.Value)
                {
                    RequireCustomOperation(value, slot.Data?.CustomName, isModifier: true);
                }

                foreach (FilterSlot slot in _filters)
                {
                    RequireCustomOperation(value, slot.Data?.CustomName, isModifier: false);
                }

                _operations = value;
                _modifiers.Value = _modifiers.Value.ToList();
            }
        }

        /// <summary>
        /// Creates a modifiable property with optional bounds, initial modifiers/filters, and math helper.
        /// </summary>
        public ModifiableProperty(
            TValue initialValue,
            ValueBounds<TValue>? bounds = null,
            IEnumerable<ValueModifier<TValue>>? initialModifiers = null,
            IEnumerable<ValueDeltaFilter<TValue, TContext>>? initialFilters = null,
            IValueMath<TValue>? valueMath = null
        )
        {
            _bounds = bounds ?? ValueBounds<TValue>.Unbounded;
            _valueMath = valueMath ?? ValueMath.GetValueMath<TValue>();
            _scalableMath = _valueMath as IScalableValueMath<TValue>;

            _modifiers = new ReactiveProperty<IReadOnlyList<ModifierSlot>>(
                SortByPriority(initialModifiers?.Select(ModifierSlot.From), slot => slot.Priority));
            _filters = SortByPriority(initialFilters?.Select(FilterSlot.From), slot => slot.Priority);

            _base = new ReactiveProperty<TValue>(_bounds.Clamp(initialValue));

            Base = _base.ToReadOnlyReactiveProperty().AddTo(_disposables);

            Effective = _base
                .CombineLatest(_modifiers, (baseValue, modifiers) => ApplyModifiers(baseValue, modifiers, steps: null))
                .ToReadOnlyReactiveProperty()
                .AddTo(_disposables);

            Modifiers = _modifiers.Select(static slots => (IReadOnlyList<ValueModifier<TValue>>)slots
                .Where(static slot => slot.Function is not null)
                .Select(static slot => slot.Function!)
                .ToList());

            DataModifiers = _modifiers.Select(static slots => (IReadOnlyList<Modifier>)slots
                .Where(static slot => slot.Data is not null)
                .Select(static slot => slot.Data!)
                .ToList());
        }

        /// <summary>
        /// Creates an unbounded modifiable property.
        /// </summary>
        public ModifiableProperty(TValue initialValue)
            : this(initialValue, bounds: null)
        { }

        /// <summary>
        /// Creates a modifiable property with an optional inclusive <paramref name="min"/> and
        /// <paramref name="max"/>; an omitted side is unbounded.
        /// </summary>
        public ModifiableProperty(
            TValue initialValue,
            TValue? min = null,
            TValue? max = null,
            IEnumerable<ValueModifier<TValue>>? initialModifiers = null,
            IEnumerable<ValueDeltaFilter<TValue, TContext>>? initialFilters = null,
            IValueMath<TValue>? valueMath = null
        ) : this(
            initialValue,
            new ValueBounds<TValue>(min.HasValue, min.GetValueOrDefault(), max.HasValue, max.GetValueOrDefault()),
            initialModifiers,
            initialFilters,
            valueMath)
        { }

        private static List<T> SortByPriority<T>(IEnumerable<T>? items, Func<T, int> priority)
        {
            var list = items?.ToList() ?? new List<T>();

            // List<T>.Sort is unstable; OrderBy is stable, so equal priorities keep the given order.
            return list.OrderBy(priority).ToList();
        }

        /// <summary>
        /// Replaces the entry matching <paramref name="isSame"/> in place when its priority is unchanged,
        /// otherwise inserts <paramref name="item"/> after every entry of equal or lower priority. The
        /// list is already priority-ordered, so this keeps it sorted and stable (equal priorities stay
        /// in insertion order) without a sort or extra allocation.
        /// </summary>
        private static void UpsertByPriority<T>(List<T> list, T item, Predicate<T> isSame, Func<T, int> priority)
        {
            int itemPriority = priority(item);
            int existing = list.FindIndex(isSame);

            if (existing >= 0)
            {
                if (priority(list[existing]) == itemPriority)
                {
                    list[existing] = item;
                    return;
                }

                list.RemoveAt(existing);
            }

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
            IReadOnlyList<ModifierSlot> modifiers,
            List<ModifierStep<TValue>>? steps)
        {
            HashSet<string> suppressed = ModifierStack.FindSuppressed(
                modifiers.Where(static slot => slot.Data is not null).Select(static slot => slot.Data!),
                _stacking);

            var modifiedValue = baseValue;

            foreach (var slot in modifiers)
            {
                TValue before = modifiedValue;
                bool isSuppressed = slot.Data is not null && suppressed.Contains(slot.Id);

                if (slot.Function is not null)
                {
                    modifiedValue = slot.Function.ModifyFunc(modifiedValue);
                }
                else if (!isSuppressed)
                {
                    // Data modifiers are only accepted when scalable math (and, for custom ones, a
                    // registered operation) exists; see AddOrUpdateModifier and Operations.
                    modifiedValue = ModifierMath.Apply(modifiedValue, slot.Data!, _scalableMath!, _operations);
                }

                steps?.Add(new ModifierStep<TValue>(slot.Id, slot.Priority, before, modifiedValue, isSuppressed, slot.Data));
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
            var fired = new List<string>();
            var filtered = delta;

            foreach (var slot in _filters)
            {
                ValueDelta<TValue, TContext> next = slot.Function is not null
                    ? slot.Function.FilterFunc(filtered)
                    : new ValueDelta<TValue, TContext>(
                        DeltaPipeline.ApplyFilter(filtered.Delta, slot.Data!, _scalableMath!, _operations),
                        filtered.Context);

                if (!EqualityComparer<TValue>.Default.Equals(next.Delta, filtered.Delta))
                {
                    fired.Add(slot.Id);
                }

                filtered = next;
            }

            TValue after = ApplyBounds(before, filtered.Delta);
            _base.Value = after;

            _processedDeltas.OnNext(filtered);
            _appliedDeltas.OnNext(new DeltaResult<TValue>(
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
        /// Returns a step-by-step account of how the current modifiers produce <see cref="CurrentValue"/>
        /// from the current base value. Function-based modifiers appear with a null
        /// <see cref="ModifierStep{TValue}.Data"/>.
        /// </summary>
        public ModifierBreakdown<TValue> Explain()
        {
            ThrowIfDisposed();
            var steps = new List<ModifierStep<TValue>>();
            TValue baseValue = _base.Value;
            TValue result = ApplyModifiers(baseValue, _modifiers.Value, steps);
            return new ModifierBreakdown<TValue>(baseValue, result, steps);
        }

        /// <summary>
        /// Add or replace a modifier with the same ID.
        /// Higher priority modifiers are applied later; modifiers with equal priority are applied in
        /// insertion order. Replacing a modifier keeps its position while its priority is unchanged.
        /// </summary>
        public void AddOrUpdateModifier(ValueModifier<TValue> modifier) => UpsertModifier(ModifierSlot.From(modifier));

        /// <summary>
        /// Add or replace a data modifier with the same ID. Ordering follows
        /// <see cref="AddOrUpdateModifier(ValueModifier{TValue})"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// <typeparamref name="TValue"/> has no <see cref="IScalableValueMath{TValue}"/>, or a custom
        /// modifier's operation isn't registered in <see cref="Operations"/>.
        /// </exception>
        public void AddOrUpdateModifier(Modifier modifier)
        {
            if (modifier is null)
            {
                throw new ArgumentNullException(nameof(modifier));
            }

            RequireScalableMath();
            RequireCustomOperation(_operations, modifier.CustomName, isModifier: true);
            UpsertModifier(ModifierSlot.From(modifier));
        }

        /// <summary>
        /// Add or replace a modifier with the same ID.
        /// Higher priority modifiers are applied later.
        /// </summary>
        public void AddOrUpdateModifier(
            string id,
            Func<TValue, TValue> modifyFunc,
            int priority = 0)
        {
            ValueModifier<TValue> modifier = new(id, modifyFunc, priority);
            AddOrUpdateModifier(modifier);
        }

        /// <summary>
        /// Remove a modifier by ID.
        /// </summary>
        public void RemoveModifier(string id)
        {
            // No-op once disposed: removal is cleanup, and the disposables handed out by
            // PushModifier routinely run during teardown, when the owning property may already
            // be gone (e.g. dependency disposal ordering).
            if (_disposed)
            {
                return;
            }

            List<ModifierSlot> modifiers = _modifiers.Value.ToList();
            _ = modifiers.RemoveAll(m => m.Id == id);
            _modifiers.Value = modifiers;
        }

        /// <summary>
        /// Add a modifier and get an <see cref="IDisposable"/> that removes it when disposed.
        /// Higher priority modifiers are applied later.
        /// </summary>
        public IDisposable PushModifier(ValueModifier<TValue> modifier)
        {
            AddOrUpdateModifier(modifier);
            return Disposable.Create(() => RemoveModifier(modifier.Id));
        }

        /// <summary>
        /// Add a data modifier and get an <see cref="IDisposable"/> that removes it when disposed.
        /// </summary>
        public IDisposable PushModifier(Modifier modifier)
        {
            AddOrUpdateModifier(modifier);
            return Disposable.Create(() => RemoveModifier(modifier.Id));
        }

        /// <summary>
        /// Add a modifier and get an <see cref="IDisposable"/> that removes it when disposed.
        /// Higher priority modifiers are applied later.
        /// </summary>
        public IDisposable PushModifier(
            string id,
            Func<TValue, TValue> modifyFunc,
            int priority = 0)
        {
            AddOrUpdateModifier(id, modifyFunc, priority);
            return Disposable.Create(() => RemoveModifier(id));
        }

        /// <summary>
        /// Add or replace a filter with the same ID.
        /// Filters with equal priority run in insertion order. Replacing a filter keeps its position
        /// while its priority is unchanged.
        /// </summary>
        public void AddOrUpdateFilter(ValueDeltaFilter<TValue, TContext> filter) => UpsertFilter(FilterSlot.From(filter));

        /// <summary>
        /// Add or replace a data filter with the same ID. Ordering follows
        /// <see cref="AddOrUpdateFilter(ValueDeltaFilter{TValue, TContext})"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// <typeparamref name="TValue"/> has no <see cref="IScalableValueMath{TValue}"/>, or a custom
        /// filter's operation isn't registered in <see cref="Operations"/>.
        /// </exception>
        public void AddOrUpdateFilter(DeltaFilter filter)
        {
            if (filter is null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            RequireScalableMath();
            RequireCustomOperation(_operations, filter.CustomName, isModifier: false);
            UpsertFilter(FilterSlot.From(filter));
        }

        /// <summary>
        /// Add or replace a filter with the same ID.
        /// </summary>
        public void AddOrUpdateFilter(string id, Func<ValueDelta<TValue, TContext>, ValueDelta<TValue, TContext>> filterFunc, int priority = 0)
        {
            ValueDeltaFilter<TValue, TContext> filter = new(id, filterFunc, priority);
            AddOrUpdateFilter(filter);
        }

        /// <summary>
        /// Remove a filter by ID.
        /// </summary>
        public void RemoveFilter(string id)
        {
            // No-op once disposed; see RemoveModifier.
            if (_disposed)
            {
                return;
            }

            List<FilterSlot> filters = _filters.ToList();
            _ = filters.RemoveAll(f => f.Id == id);
            _filters = filters;
        }

        /// <summary>
        /// Add a filter and get an <see cref="IDisposable"/> that removes it when disposed.
        /// </summary>
        public IDisposable PushFilter(ValueDeltaFilter<TValue, TContext> filter)
        {
            AddOrUpdateFilter(filter);
            return Disposable.Create(() => RemoveFilter(filter.Id));
        }

        /// <summary>
        /// Add a data filter and get an <see cref="IDisposable"/> that removes it when disposed.
        /// </summary>
        public IDisposable PushFilter(DeltaFilter filter)
        {
            AddOrUpdateFilter(filter);
            return Disposable.Create(() => RemoveFilter(filter.Id));
        }

        /// <summary>
        /// Add a filter and get an <see cref="IDisposable"/> that removes it when disposed.
        /// </summary>
        public IDisposable PushFilter(string id, Func<ValueDelta<TValue, TContext>, ValueDelta<TValue, TContext>> filterFunc, int priority = 0)
        {
            AddOrUpdateFilter(id, filterFunc, priority);
            return Disposable.Create(() => RemoveFilter(id));
        }

        /// <summary>
        /// Remove all filters.
        /// </summary>
        public void ClearFilters()
        {
            // No-op once disposed; see RemoveModifier.
            if (_disposed)
            {
                return;
            }

            _filters = new List<FilterSlot>();
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
        /// <see cref="ModifiableProperty{TValue, TContext}"/> to regain write access. A new lightweight
        /// view is returned per call; cache it if you need a stable reference.
        /// </summary>
        public IReadOnlyModifiableProperty<TValue, TContext> AsReadOnly() => new ReadOnlyView(this);

        private void UpsertModifier(ModifierSlot slot)
        {
            ThrowIfDisposed();
            List<ModifierSlot> modifiers = _modifiers.Value.ToList();
            UpsertByPriority(modifiers, slot, m => m.Id == slot.Id, m => m.Priority);
            _modifiers.Value = modifiers;
        }

        private void UpsertFilter(FilterSlot slot)
        {
            ThrowIfDisposed();
            List<FilterSlot> filters = _filters.ToList();
            UpsertByPriority(filters, slot, f => f.Id == slot.Id, f => f.Priority);
            _filters = filters;
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
        /// One modifier entry: either function-based or data, never both.
        /// </summary>
        private sealed class ModifierSlot
        {
            private ModifierSlot(string id, int priority, ValueModifier<TValue>? function, Modifier? data)
            {
                Id = id;
                Priority = priority;
                Function = function;
                Data = data;
            }

            public string Id { get; }
            public int Priority { get; }
            public ValueModifier<TValue>? Function { get; }
            public Modifier? Data { get; }

            public static ModifierSlot From(ValueModifier<TValue> modifier) =>
                new ModifierSlot(modifier.Id, modifier.Priority, modifier, null);

            public static ModifierSlot From(Modifier modifier) =>
                new ModifierSlot(modifier.Id, modifier.Priority, null, modifier);
        }

        /// <summary>
        /// One filter entry: either function-based or data, never both.
        /// </summary>
        private sealed class FilterSlot
        {
            private FilterSlot(string id, int priority, ValueDeltaFilter<TValue, TContext>? function, DeltaFilter? data)
            {
                Id = id;
                Priority = priority;
                Function = function;
                Data = data;
            }

            public string Id { get; }
            public int Priority { get; }
            public ValueDeltaFilter<TValue, TContext>? Function { get; }
            public DeltaFilter? Data { get; }

            public static FilterSlot From(ValueDeltaFilter<TValue, TContext> filter) =>
                new FilterSlot(filter.Id, filter.Priority, filter, null);

            public static FilterSlot From(DeltaFilter filter) =>
                new FilterSlot(filter.Id, filter.Priority, null, filter);
        }

        /// <summary>
        /// Private read-only adapter. Being private and not deriving from
        /// <see cref="ModifiableProperty{TValue, TContext}"/>, no consumer downcast can reach the
        /// writable API.
        /// </summary>
        private sealed class ReadOnlyView : IReadOnlyModifiableProperty<TValue, TContext>
        {
            private readonly ModifiableProperty<TValue, TContext> _source;

            public ReadOnlyView(ModifiableProperty<TValue, TContext> source) => _source = source;

            public TValue CurrentValue => _source.CurrentValue;

            public ReadOnlyReactiveProperty<TValue> Base => _source.Base;

            public Observable<ValueDelta<TValue, TContext>> ProcessedDeltas => _source.ProcessedDeltas;

            public IDisposable Subscribe(IObserver<TValue> observer) => _source.Subscribe(observer);
        }
    }
}
