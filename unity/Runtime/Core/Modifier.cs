using System;

namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// A closure-free, read-time value transformation: an operation and an operand. Unlike
    /// <see cref="Common.Properties.ValueModifier{TValue}"/>, it can be compared, copied, saved and
    /// described, and its effect is reported step by step by <see cref="ModifierStack.Explain{TValue}"/>.
    /// </summary>
    /// <param name="Id">Stable identifier used for replacement, removal and breakdowns.</param>
    /// <param name="Operation">How <paramref name="Value"/> combines with the incoming value.</param>
    /// <param name="Value">The operand.</param>
    /// <param name="Priority">Lower numbers run earlier; equal priorities keep their given order.</param>
    /// <param name="StackGroup">Optional group whose members are resolved by a <see cref="StackPolicy"/>.</param>
    /// <param name="CustomName">
    /// The registered operation name; required for <see cref="ModifierOperation.Custom"/> and not
    /// allowed otherwise.
    /// </param>
    public sealed record Modifier(
        string Id,
        ModifierOperation Operation,
        double Value,
        int Priority = 0,
        string? StackGroup = null,
        string? CustomName = null)
    {
        /// <summary>Stable identifier used for replacement, removal and breakdowns.</summary>
        public string Id { get; } = string.IsNullOrWhiteSpace(Id)
            ? throw new ArgumentException("Modifier id cannot be blank.", nameof(Id))
            : Id;

        /// <summary>How <see cref="Value"/> combines with the incoming value.</summary>
        public ModifierOperation Operation { get; } = Enum.IsDefined(typeof(ModifierOperation), Operation)
            ? Operation
            : throw new ArgumentOutOfRangeException(nameof(Operation), Operation, null);

        /// <summary>The operand.</summary>
        public double Value { get; } = double.IsFinite(Value)
            ? Value
            : throw new ArgumentOutOfRangeException(nameof(Value), Value, "Modifier value must be finite.");

        /// <summary>Optional group whose members are resolved by a <see cref="StackPolicy"/>.</summary>
        public string? StackGroup { get; } = StackGroup is not null && StackGroup.Trim().Length == 0
            ? throw new ArgumentException("Stack group cannot be blank; use null for no group.", nameof(StackGroup))
            : StackGroup;

        /// <summary>The registered operation name for <see cref="ModifierOperation.Custom"/>; otherwise null.</summary>
        public string? CustomName { get; } = CustomOperationNames.Validate(Operation == ModifierOperation.Custom, CustomName);
    }
}
