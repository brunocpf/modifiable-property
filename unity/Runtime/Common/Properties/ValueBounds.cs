using System;

namespace BrunoCPF.Modifiable.Common.Properties
{
    /// <summary>
    /// Defines inclusive minimum and maximum bounds for a value type.
    /// </summary>
    public sealed record ValueBounds<TValue>(TValue? Min = default, TValue? Max = default) where TValue : IComparable
    {
        /// <summary>
        /// Clamps the given value to the configured bounds.
        /// </summary>
        public TValue Clamp(TValue value)
        {
            if (Min is not null && value.CompareTo(Min) < 0)
            {
                return Min;
            }

            if (Max is not null && value.CompareTo(Max) > 0)
            {
                return Max;
            }

            return value;
        }

        /// <summary>
        /// Checks whether the value falls within the bounds.
        /// </summary>
        public bool Contains(TValue value)
        {
            if (Min is not null && value.CompareTo(Min) < 0)
            {
                return false;
            }

            if (Max is not null && value.CompareTo(Max) > 0)
            {
                return false;
            }

            return true;
        }
    }
}
