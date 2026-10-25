using System;

namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// Shared validation for the <c>CustomName</c> of modifiers and delta filters.
    /// </summary>
    internal static class CustomOperationNames
    {
        public static string? Validate(bool isCustom, string? customName)
        {
            if (isCustom && string.IsNullOrWhiteSpace(customName))
            {
                throw new ArgumentException("A Custom operation needs a non-blank CustomName.", "CustomName");
            }

            if (!isCustom && customName is not null)
            {
                throw new ArgumentException("CustomName is only allowed with a Custom operation.", "CustomName");
            }

            return customName;
        }

        /// <summary>
        /// Re-checks the Operation/CustomName pairing at evaluation time, because <c>with</c> expressions
        /// can change either one without running the constructor.
        /// </summary>
        public static void RequireConsistent(bool isCustom, string? customName)
        {
            if (isCustom && string.IsNullOrWhiteSpace(customName))
            {
                throw new InvalidOperationException("A Custom operation needs a non-blank CustomName.");
            }

            if (!isCustom && customName is not null)
            {
                throw new InvalidOperationException("CustomName is only allowed with a Custom operation.");
            }
        }
    }
}
