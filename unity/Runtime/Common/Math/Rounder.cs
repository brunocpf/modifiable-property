using System;

namespace BrunoCPF.Modifiable.Common.Math
{
    /// <summary>
    /// Shared rounding and saturation for the built-in <see cref="IScalableValueMath{TValue}"/> types.
    /// Results never wrap around or become infinite: they saturate at the type's limits. NaN is rejected.
    /// </summary>
    internal static class Rounder
    {
        public static double Round(double value, RoundingMode mode, double min, double max)
        {
            RequireNumber(value);

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

        public static int SaturateToInt(long value) =>
            value > int.MaxValue ? int.MaxValue : value < int.MinValue ? int.MinValue : (int)value;

        public static long AddSaturating(long a, long b)
        {
            long result = unchecked(a + b);

            // Overflow happened when both operands share a sign that the result doesn't.
            return ((a ^ result) & (b ^ result)) < 0 ? (a < 0 ? long.MinValue : long.MaxValue) : result;
        }

        public static long SubtractSaturating(long a, long b)
        {
            long result = unchecked(a - b);

            // Overflow happened when the operands differ in sign and the result's sign differs from a's.
            return ((a ^ b) & (a ^ result)) < 0 ? (a < 0 ? long.MinValue : long.MaxValue) : result;
        }

        public static float SaturateFloat(float value)
        {
            RequireNumber(value);
            return float.IsPositiveInfinity(value) ? float.MaxValue
                : float.IsNegativeInfinity(value) ? float.MinValue
                : value;
        }

        public static double SaturateDouble(double value)
        {
            RequireNumber(value);
            return double.IsPositiveInfinity(value) ? double.MaxValue
                : double.IsNegativeInfinity(value) ? double.MinValue
                : value;
        }

        private static void RequireNumber(double value)
        {
            if (double.IsNaN(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The result is not a number (NaN).");
            }
        }
    }
}
