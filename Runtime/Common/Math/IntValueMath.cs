namespace BrunoCPF.Modifiable.Common.Math
{
    /// <summary>
    /// Integer implementation of <see cref="IValueMath{TValue}"/>.
    /// </summary>
    public sealed class IntValueMath : IValueMath<int>
    {
        public int Add(int a, int b) => a + b;
        public int Subtract(int a, int b) => a - b;
    }
}
