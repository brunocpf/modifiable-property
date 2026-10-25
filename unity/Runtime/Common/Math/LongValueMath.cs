namespace BrunoCPF.Modifiable.Common.Math
{
    /// <summary>
    /// Long integer implementation of <see cref="IValueMath{TValue}"/>. Data modifiers and filters
    /// work in <see cref="double"/>, so magnitudes above 2^53 lose precision when scaled.
    /// </summary>
    public sealed class LongValueMath : IScalableValueMath<long>
    {
        /// <summary>
        /// Creates long integer math that rounds scaled results with <paramref name="rounding"/>.
        /// </summary>
        public LongValueMath(RoundingMode rounding = RoundingMode.TowardZero) => Rounding = rounding;

        /// <summary>
        /// How fractional results are rounded.
        /// </summary>
        public RoundingMode Rounding { get; }

        public long Add(long a, long b) => a + b;
        public long Subtract(long a, long b) => a - b;
        public double ToDouble(long value) => value;
        public long FromDouble(double value) => Rounder.RoundToLong(value, Rounding);
    }
}
