using System;
using BrunoCPF.Modifiable.Common.Math;

namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// Applies a single modifier operation to a value.
    /// </summary>
    public static class ModifierMath
    {
        /// <summary>
        /// Applies a built-in <paramref name="operation"/> with <paramref name="operand"/> to
        /// <paramref name="value"/>. The result is converted back through <paramref name="math"/>, so
        /// integral types round per step.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// <paramref name="operation"/> is <see cref="ModifierOperation.Custom"/>; use the
        /// <see cref="Modifier"/> overload, which carries the operation name.
        /// </exception>
        public static TValue Apply<TValue>(
            TValue value,
            ModifierOperation operation,
            double operand,
            IScalableValueMath<TValue> math)
            where TValue : struct
        {
            if (operation == ModifierOperation.Custom)
            {
                throw new ArgumentException(
                    "Custom operations need a name; use the Apply overload that takes a Modifier.",
                    nameof(operation));
            }

            return Apply(value, operation, operand, customName: null, math, operations: null);
        }

        /// <summary>
        /// Applies <paramref name="modifier"/> to <paramref name="value"/>.
        /// </summary>
        /// <param name="value">The incoming value.</param>
        /// <param name="modifier">The modifier.</param>
        /// <param name="math">Math for <typeparamref name="TValue"/>.</param>
        /// <param name="operations">Registry for <see cref="ModifierOperation.Custom"/> modifiers.</param>
        /// <exception cref="InvalidOperationException">
        /// A custom modifier's operation isn't registered, or it returned a non-finite result.
        /// </exception>
        public static TValue Apply<TValue>(
            TValue value,
            Modifier modifier,
            IScalableValueMath<TValue> math,
            CustomOperations? operations = null)
            where TValue : struct
        {
            if (modifier is null)
            {
                throw new ArgumentNullException(nameof(modifier));
            }

            return Apply(value, modifier.Operation, modifier.Value, modifier.CustomName, math, operations);
        }

        private static TValue Apply<TValue>(
            TValue value,
            ModifierOperation operation,
            double operand,
            string? customName,
            IScalableValueMath<TValue> math,
            CustomOperations? operations)
            where TValue : struct
        {
            if (math is null)
            {
                throw new ArgumentNullException(nameof(math));
            }

            double current = math.ToDouble(value);
            double result = operation switch
            {
                ModifierOperation.Add => current + operand,
                ModifierOperation.Multiply => current * operand,
                ModifierOperation.Override => operand,
                ModifierOperation.AtLeast => System.Math.Max(current, operand),
                ModifierOperation.AtMost => System.Math.Min(current, operand),
                ModifierOperation.Custom => CustomOperations.ApplyModifier(operations, customName!, current, operand),
                _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
            };

            return math.FromDouble(result);
        }
    }
}
