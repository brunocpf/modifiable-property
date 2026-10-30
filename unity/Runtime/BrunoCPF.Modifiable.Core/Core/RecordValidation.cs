using System;

namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// Per-member validation shared by the <see cref="Modifier{TSource}"/> and
    /// <see cref="DeltaFilter{TSource}"/> constructors and <c>init</c> accessors.
    /// </summary>
    internal static class RecordValidation
    {
        public static TEnum Defined<TEnum>(TEnum value, string parameterName) where TEnum : struct, Enum =>
            Enum.IsDefined(typeof(TEnum), value)
                ? value
                : throw new ArgumentOutOfRangeException(parameterName, value, null);

        public static double Finite(double value, string parameterName) =>
            double.IsFinite(value)
                ? value
                : throw new ArgumentOutOfRangeException(parameterName, value, "The value must be finite.");

        public static string? NullOrNonBlank(string? value, string parameterName) =>
            value is not null && value.Trim().Length == 0
                ? throw new ArgumentException("The value cannot be blank; use null to leave it unset.", parameterName)
                : value;
    }
}
