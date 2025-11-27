namespace BrunoCPF.Modifiable.Common.Math
{
    /// <summary>
    /// Double implementation of <see cref="IValueMath{TValue}"/>.
    /// </summary>
    public sealed class DoubleValueMath : IValueMath<double>
    {
        public double Add(double a, double b) => a + b;
        public double Subtract(double a, double b) => a - b;
    }
}
