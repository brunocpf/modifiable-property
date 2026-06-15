namespace BrunoCPF.Modifiable.Common.Math
{
    /// <summary>
    /// Float implementation of <see cref="IValueMath{TValue}"/>.
    /// </summary>
    public sealed class FloatValueMath : IValueMath<float>
    {
        public float Add(float a, float b) => a + b;
        public float Subtract(float a, float b) => a - b;
    }
}
