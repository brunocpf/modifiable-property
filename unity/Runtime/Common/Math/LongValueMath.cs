namespace BrunoCPF.Modifiable.Common.Math
{
    /// <summary>
    /// Long integer implementation of <see cref="IValueMath{TValue}"/>.
    /// </summary>
    public sealed class LongValueMath : IValueMath<long>
    {
        public long Add(long a, long b) => a + b;
        public long Subtract(long a, long b) => a - b;
    }
}
