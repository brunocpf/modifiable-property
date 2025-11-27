using System;
using System.Collections.Generic;
using System.Linq;
using BrunoCPF.Modifiable.Common.Math;
using R3;

namespace BrunoCPF.Modifiable.Common.Properties
{
    /// <summary>
    /// Reactive value that accepts deltas, applies filters and modifiers, and enforces bounds.
    /// </summary>
    public sealed class ModifiableProperty<TValue, TContext>
        : Observable<TValue>, IDisposable where TValue : struct, IComparable
    {
        private readonly Subject<ValueDelta<TValue, TContext>> _rawDeltas;
        private readonly BehaviorSubject<IReadOnlyList<ValueDeltaFilter<TValue, TContext>>> _filters;
        private readonly ReactiveProperty<IReadOnlyList<ValueModifier<TValue>>> _modifiers;
        private readonly ValueBounds<TValue> _bounds;
        private readonly IValueMath<TValue>? _valueMath;
        private readonly CompositeDisposable _disposables = new();

        /// <summary>
        /// Current effective value after modifiers are applied.
        /// </summary>
        public TValue CurrentValue => Effective.CurrentValue;

        /// <summary>
        /// Stream of processed deltas after filters are applied.
        /// </summary>
        public Observable<ValueDelta<TValue, TContext>> ProcessedDeltas { get; }

        /// <summary>
        /// Base subject before modifiers are applied.
        /// </summary>
        public ReadOnlyReactiveProperty<TValue> Base { get; }

        /// <summary>
        /// Final effective subject after modifiers are applied.
        /// </summary>
        public ReadOnlyReactiveProperty<TValue> Effective { get; }

        /// <summary>
        /// Observable list of current modifiers.
        /// </summary>
        public Observable<IReadOnlyList<ValueModifier<TValue>>> Modifiers => _modifiers.AsObservable();

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
            _bounds = bounds ?? new ValueBounds<TValue>();
            _rawDeltas = new Subject<ValueDelta<TValue, TContext>>();

            _modifiers = new ReactiveProperty<IReadOnlyList<ValueModifier<TValue>>>(
                initialModifiers?.ToList() ?? new List<ValueModifier<TValue>>()
            );

            _filters = new BehaviorSubject<IReadOnlyList<ValueDeltaFilter<TValue, TContext>>>(
                initialFilters?.ToList() ?? new List<ValueDeltaFilter<TValue, TContext>>()
            );

            _valueMath = valueMath ?? ValueMath.GetValueMath<TValue>();

            ProcessedDeltas = _rawDeltas
                .WithLatestFrom(_filters, (delta, filters) => ApplyFilters(delta, filters))
                .Share();

            var clampedInitial = _bounds.Clamp(initialValue);

            Base = ProcessedDeltas
                .Scan(clampedInitial, (currentValue, delta) => ApplyBounds(currentValue, delta.Delta))
                .DistinctUntilChanged()
                .ToReadOnlyReactiveProperty(clampedInitial)
                .AddTo(_disposables);

            Effective = Base
                .CombineLatest(_modifiers, (baseValue, modifiers) => ApplyModifiers(baseValue, modifiers))
                .ToReadOnlyReactiveProperty()
                .AddTo(_disposables);
        }

        /// <summary>
        /// Creates a modifiable property with optional bounds, initial modifiers/filters, and math helper.
        /// </summary>
        public ModifiableProperty(
            TValue initialValue,
            TValue min = default,
            TValue max = default,
            IEnumerable<ValueModifier<TValue>>? initialModifiers = null,
            IEnumerable<ValueDeltaFilter<TValue, TContext>>? initialFilters = null,
            IValueMath<TValue>? valueMath = null
        ) : this(initialValue, new ValueBounds<TValue>(min, max), initialModifiers, initialFilters, valueMath)
        { }

        private ValueDelta<TValue, TContext> ApplyFilters(
            ValueDelta<TValue, TContext> delta,
            IReadOnlyList<ValueDeltaFilter<TValue, TContext>> filters)
        {
            var filteredDelta = delta;

            foreach (var filter in filters)
            {
                filteredDelta = filter.FilterFunc(filteredDelta);
            }

            return filteredDelta;
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

        private TValue ApplyModifiers(TValue baseValue, IReadOnlyList<ValueModifier<TValue>> modifiers)
        {
            var modifiedValue = baseValue;

            foreach (var modifier in modifiers)
            {
                modifiedValue = modifier.ModifyFunc(modifiedValue);
            }

            return modifiedValue;
        }

        /// <summary>
        /// Injects a delta with context into the property.
        /// </summary>
        public void AddDelta(ValueDelta<TValue, TContext> delta) => _rawDeltas.OnNext(delta);

        /// <summary>
        /// Injects a delta without context into the property.
        /// </summary>
        public void AddDelta(TValue delta) => _rawDeltas.OnNext(new ValueDelta<TValue, TContext>(delta, default));

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
        /// Add or replace a modifier with the same ID.
        /// Higher priority modifiers are applied later.
        /// </summary>
        public void AddOrUpdateModifier(ValueModifier<TValue> modifier)
        {
            List<ValueModifier<TValue>> modifiers = _modifiers.Value.ToList();
            int index = modifiers.FindIndex(m => m.Id == modifier.Id);
            if (index >= 0)
            {
                modifiers[index] = modifier;
            }
            else
            {
                modifiers.Add(modifier);
            }

            modifiers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            _modifiers.Value = modifiers;
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
            List<ValueModifier<TValue>> modifiers = _modifiers.Value.ToList();
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
        /// </summary>
        public void AddOrUpdateFilter(ValueDeltaFilter<TValue, TContext> filter)
        {
            List<ValueDeltaFilter<TValue, TContext>> filters = _filters.Value.ToList();
            int index = filters.FindIndex(f => f.Id == filter.Id);

            if (index >= 0)
            {
                filters[index] = filter;
            }
            else
            {
                filters.Add(filter);
            }

            filters.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            _filters.OnNext(filters);
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
            List<ValueDeltaFilter<TValue, TContext>> filters = _filters.Value.ToList();
            _ = filters.RemoveAll(f => f.Id == id);
            _filters.OnNext(filters);
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
        public void ClearFilters() => _filters.OnNext(new List<ValueDeltaFilter<TValue, TContext>>());

        /// <summary>
        /// Disposes subscriptions and internal observables.
        /// </summary>
        public void Dispose()
        {
            _disposables.Dispose();
            _rawDeltas.Dispose();
            _filters.Dispose();
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
    }
}
