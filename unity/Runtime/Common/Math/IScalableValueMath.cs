namespace BrunoCPF.Modifiable.Common.Math
{
    /// <summary>
    /// Value math that can convert to and from <see cref="double"/>, which lets data modifiers and
    /// delta filters (<see cref="Core.Modifier"/>, <see cref="Core.DeltaFilter"/>) scale, override and
    /// clamp values of this type.
    /// </summary>
    public interface IScalableValueMath<TValue> : IValueMath<TValue> where TValue : struct
    {
        /// <summary>
        /// Converts a value to <see cref="double"/>.
        /// </summary>
        public double ToDouble(TValue value);

        /// <summary>
        /// Converts a <see cref="double"/> result back to this type, rounding and saturating as needed.
        /// </summary>
        public TValue FromDouble(double value);
    }
}
