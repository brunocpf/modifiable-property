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
    public sealed record Modifier<TSource>(
        ModifierOperation Operation,
        double Value,
        int Priority = 0,
        string? StackGroup = null,
        string? CustomName = null,
        TSource? Source = default)
    {
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
