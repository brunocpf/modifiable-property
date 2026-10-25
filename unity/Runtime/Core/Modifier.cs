using System;

namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// A closure-free, read-time value transformation: an operation and an operand. It can be compared,
    /// copied, saved and described, and its effect is reported step by step by
    /// <see cref="ModifierStack.Explain{TValue, TSource}"/>.
    /// </summary>
    /// <typeparam name="TSource">
    /// What the modifier came from (a status, item, skill, …). Used for attribution and removal only;
    /// it doesn't have to be unique. Use a reference or nullable type if a modifier may have no source.
    /// </typeparam>
    /// <param name="Operation">How <paramref name="Value"/> combines with the incoming value.</param>
    /// <param name="Value">The operand.</param>
    /// <param name="Priority">Lower numbers run earlier; equal priorities keep their given order.</param>
    /// <param name="StackGroup">Optional group whose members are resolved by a <see cref="StackPolicy"/>.</param>
    /// <param name="CustomName">
    /// The registered operation name; required for <see cref="ModifierOperation.Custom"/> and not
    /// allowed otherwise.
    /// </param>
    /// <param name="Source">Optional origin, reported in breakdowns.</param>
    /// <remarks>
    /// Every member supports <c>with</c>. Each value is validated on its own when set; the pairing of
    /// <see cref="Operation"/> and <see cref="CustomName"/> is checked by the constructor and again
    /// when the modifier is evaluated.
    /// </remarks>
    public sealed record Modifier<TSource>(
        ModifierOperation Operation,
        double Value,
        int Priority = 0,
        string? StackGroup = null,
        string? CustomName = null,
        TSource? Source = default)
    {
        private readonly ModifierOperation _operation = RecordValidation.Defined(Operation, nameof(Operation));
        private readonly double _value = RecordValidation.Finite(Value, nameof(Value));
        private readonly string? _stackGroup = RecordValidation.NullOrNonBlank(StackGroup, nameof(StackGroup));
        private readonly string? _customName = CustomOperationNames.Validate(Operation == ModifierOperation.Custom, CustomName);

        /// <summary>How <see cref="Value"/> combines with the incoming value.</summary>
        public ModifierOperation Operation
        {
            get => _operation;
            init => _operation = RecordValidation.Defined(value, nameof(Operation));
        }

        /// <summary>The operand.</summary>
        public double Value
        {
            get => _value;
            init => _value = RecordValidation.Finite(value, nameof(Value));
        }

        /// <summary>Optional group whose members are resolved by a <see cref="StackPolicy"/>.</summary>
        public string? StackGroup
        {
            get => _stackGroup;
            init => _stackGroup = RecordValidation.NullOrNonBlank(value, nameof(StackGroup));
        }

        /// <summary>The registered operation name for <see cref="ModifierOperation.Custom"/>; otherwise null.</summary>
        public string? CustomName
        {
            get => _customName;
            init => _customName = RecordValidation.NullOrNonBlank(value, nameof(CustomName));
        }
    }
}
