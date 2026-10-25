namespace BrunoCPF.Modifiable.Common.Math
{
    /// <summary>
    /// Double implementation of <see cref="IValueMath{TValue}"/>.
    /// </summary>
    public sealed class DoubleValueMath : IScalableValueMath<double>
    {
        /// <summary>a + b; infinite results saturate at ±<see cref="double.MaxValue"/> and NaN throws.</summary>
        public double Add(double a, double b) => Rounder.SaturateDouble(a + b);

        /// <summary>a − b; infinite results saturate at ±<see cref="double.MaxValue"/> and NaN throws.</summary>
        public double Subtract(double a, double b) => Rounder.SaturateDouble(a - b);

        public double ToDouble(double value) => value;

        /// <summary>Returns the value, saturating at ±<see cref="double.MaxValue"/>; NaN throws.</summary>
        public double FromDouble(double value) => Rounder.SaturateDouble(value);
    }
}
