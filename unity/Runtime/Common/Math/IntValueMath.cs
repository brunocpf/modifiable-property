namespace BrunoCPF.Modifiable.Common.Math
{
    /// <summary>
    /// Integer implementation of <see cref="IValueMath{TValue}"/>.
    /// </summary>
    public sealed class IntValueMath : IScalableValueMath<int>
    {
        /// <summary>
        /// Creates integer math that rounds scaled results with <paramref name="rounding"/>.
        /// </summary>
        public IntValueMath(RoundingMode rounding = RoundingMode.TowardZero) => Rounding = rounding;

        /// <summary>
        /// How fractional results are rounded.
        /// </summary>
        public RoundingMode Rounding { get; }

        public int Add(int a, int b) => a + b;
        public int Subtract(int a, int b) => a - b;
        public double ToDouble(int value) => value;
        public int FromDouble(double value) => (int)Rounder.Round(value, Rounding, int.MinValue, int.MaxValue);
    }
}
