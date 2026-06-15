using System;
using R3;

namespace BrunoCPF.Modifiable.Common.Properties
{
    /// <summary>
    /// Read-only interface for a modifiable property.
    /// </summary>
    /// <typeparam name="TValue">The underlying value type.</typeparam>
    /// <typeparam name="TContext">The delta context type carried with each change.</typeparam>
    public interface IReadOnlyModifiableProperty<TValue, TContext> : IObservable<TValue>
    {
        /// <summary>
        /// Gets the current value of the property.
        /// </summary>
        public TValue CurrentValue { get; }

        /// <summary>
        /// Base subject before modifiers are applied.
        /// </summary>
        public ReadOnlyReactiveProperty<TValue> Base { get; }

        /// <summary>
        /// Gets an observable stream of processed value deltas.
        /// </summary>
        public Observable<ValueDelta<TValue, TContext>> ProcessedDeltas { get; }
    }
}
