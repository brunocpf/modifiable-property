namespace BrunoCPF.Modifiable.Common.Math
{
    /// <summary>
    /// Long integer implementation of <see cref="IValueMath{TValue}"/>. Adding, overriding and clamping
    /// with integral operands is exact; multiplying, fractional operands and custom operations go through
    /// <see cref="double"/>, so magnitudes above 2^53 lose precision there.
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

        /// <summary>a + b, saturating at <see cref="long.MinValue"/> / <see cref="long.MaxValue"/>.</summary>
        public long Add(long a, long b) => Rounder.AddSaturating(a, b);

        /// <summary>a − b, saturating at <see cref="long.MinValue"/> / <see cref="long.MaxValue"/>.</summary>
        public long Subtract(long a, long b) => Rounder.SubtractSaturating(a, b);
        public double ToDouble(long value) => value;
        public long FromDouble(double value) => Rounder.RoundToLong(value, Rounding);
    }
}
