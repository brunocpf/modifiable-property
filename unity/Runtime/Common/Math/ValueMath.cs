namespace BrunoCPF.Modifiable.Common.Math
{
    /// <summary>
    /// Helper factory for resolving <see cref="IValueMath{TValue}"/> implementations.
    /// </summary>
    public static class ValueMath
    {
        /// <summary>
        /// Default integer math implementation.
        /// </summary>
        public static readonly IValueMath<int> Int = new IntValueMath();

        /// <summary>
        /// Default float math implementation.
        /// </summary>
        public static readonly IValueMath<float> Float = new FloatValueMath();

        /// <summary>
        /// Default double math implementation.
        /// </summary>
        public static readonly IValueMath<double> Double = new DoubleValueMath();

        /// <summary>
        /// Returns the default math implementation for the given numeric type, if one exists.
        /// </summary>
        public static IValueMath<TValue>? GetValueMath<TValue>() where TValue : struct
        {
            if (typeof(TValue) == typeof(int))
            {
                return (IValueMath<TValue>)Int;
            }
            else if (typeof(TValue) == typeof(float))
            {
                return (IValueMath<TValue>)Float;
            }
            else if (typeof(TValue) == typeof(double))
            {
                return (IValueMath<TValue>)Double;
            }
            else
            {
                return null;
            }
        }
    }
}
