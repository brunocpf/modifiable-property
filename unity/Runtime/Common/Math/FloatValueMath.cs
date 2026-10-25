namespace BrunoCPF.Modifiable.Common.Math
{
    /// <summary>
    /// Float implementation of <see cref="IValueMath{TValue}"/>.
    /// </summary>
    public sealed class FloatValueMath : IScalableValueMath<float>
    {
        /// <summary>a + b; infinite results saturate at ±<see cref="float.MaxValue"/> and NaN throws.</summary>
        public float Add(float a, float b) => Rounder.SaturateFloat(a + b);

        /// <summary>a − b; infinite results saturate at ±<see cref="float.MaxValue"/> and NaN throws.</summary>
        public float Subtract(float a, float b) => Rounder.SaturateFloat(a - b);

        public double ToDouble(float value) => value;

        /// <summary>Converts back, saturating at ±<see cref="float.MaxValue"/>; NaN throws.</summary>
        public float FromDouble(double value) => Rounder.SaturateFloat((float)value);
    }
}
