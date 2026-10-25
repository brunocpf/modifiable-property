using System;

namespace BrunoCPF.Modifiable.Common.Math
{
    /// <summary>
    /// Shared rounding and saturation for the integral <see cref="IScalableValueMath{TValue}"/> types.
    /// </summary>
    internal static class Rounder
    {
        public static double Round(double value, RoundingMode mode, double min, double max)
        {
            if (double.IsNaN(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Cannot convert NaN to an integral value.");
            }

            double rounded = mode switch
            {
                RoundingMode.TowardZero => System.Math.Truncate(value),
                RoundingMode.Floor => System.Math.Floor(value),
                RoundingMode.Ceiling => System.Math.Ceiling(value),
                RoundingMode.Nearest => System.Math.Round(value, MidpointRounding.AwayFromZero),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
            };

            return rounded < min ? min : rounded > max ? max : rounded;
        }

        public static long RoundToLong(double value, RoundingMode mode)
        {
            double rounded = Round(value, mode, long.MinValue, long.MaxValue);

            // (double)long.MaxValue rounds up to 2^63, which overflows a direct cast.
            return rounded >= 9.2233720368547758E18 ? long.MaxValue : (long)rounded;
        }
    }
}
