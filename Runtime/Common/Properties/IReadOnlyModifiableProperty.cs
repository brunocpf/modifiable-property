using System;

namespace BrunoCPF.Modifiable.Common.Properties
{
    /// <summary>
    /// Read-only interface for a modifiable property.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IReadOnlyModifiableProperty<T> : IObservable<T>
    {
        /// <summary>
        /// Gets the current value of the property.
        /// </summary>
        public T CurrentValue { get; }
    }
}
