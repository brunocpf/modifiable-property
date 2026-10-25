using System;
using BrunoCPF.Modifiable.Common.Math;

namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// Applies a single <see cref="ModifierOperation"/> to a value.
    /// </summary>
    public static class ModifierMath
    {
        /// <summary>
        /// Applies <paramref name="operation"/> with <paramref name="operand"/> to <paramref name="value"/>.
        /// The result is converted back through <paramref name="math"/>, so integral types round per step.
        /// </summary>
        public static TValue Apply<TValue>(
            TValue value,
            ModifierOperation operation,
            double operand,
            IScalableValueMath<TValue> math)
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
                _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
            };

            return math.FromDouble(result);
        }

        /// <summary>
        /// Applies <paramref name="modifier"/> to <paramref name="value"/>.
        /// </summary>
        public static TValue Apply<TValue>(TValue value, Modifier modifier, IScalableValueMath<TValue> math)
            where TValue : struct
        {
            if (modifier is null)
            {
                throw new ArgumentNullException(nameof(modifier));
            }

            return Apply(value, modifier.Operation, modifier.Value, math);
        }
    }
}
