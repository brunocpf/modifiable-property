namespace BrunoCPF.Modifiable.Common.Math
{
    /// <summary>
    /// Defines arithmetic operations for a numeric value type.
    /// </summary>
    public interface IValueMath<TValue> where TValue : struct
    {
        /// <summary>
        /// Adds two values.
        /// </summary>
        public TValue Add(TValue a, TValue b);

        /// <summary>
        /// Subtracts the second value from the first.
        /// </summary>
        public TValue Subtract(TValue a, TValue b);
    }
}
