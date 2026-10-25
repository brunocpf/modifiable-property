using System;

namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// A closure-free, change-time delta transformation. It can be compared, copied, saved and
    /// described, and <see cref="DeltaPipeline"/> reports when it fired.
    /// </summary>
    /// <typeparam name="TSource">
    /// What the filter came from (a guard, passive, status, …). Used for attribution and removal only;
    /// it doesn't have to be unique. Use a reference or nullable type if a filter may have no source.
    /// </typeparam>
    /// <param name="Operation">How the delta is transformed.</param>
    /// <param name="Value">The operand; ignored by <see cref="DeltaOperation.Block"/>.</param>
    /// <param name="AppliesTo">Which deltas the filter applies to.</param>
    /// <param name="Priority">Lower numbers run earlier; equal priorities keep their given order.</param>
    /// <param name="CustomName">
    /// The registered operation name; required for <see cref="DeltaOperation.Custom"/> and not
    /// allowed otherwise.
    /// </param>
    /// <param name="Source">Optional origin, reported when the filter fires.</param>
    /// <remarks>
    /// Every member supports <c>with</c>. Each value is validated on its own when set; rules that span
    /// members (a <see cref="DeltaOperation.CapMagnitude"/> operand can't be negative, the pairing of
    /// <see cref="Operation"/> and <see cref="CustomName"/>) are checked by the constructor and again
    /// when the filter is applied.
    /// </remarks>
    public sealed record DeltaFilter<TSource>(
        DeltaOperation Operation,
        double Value = 0d,
        DeltaDirection AppliesTo = DeltaDirection.Any,
        int Priority = 0,
        string? CustomName = null,
        TSource? Source = default)
    {
        private readonly DeltaOperation _operation = RecordValidation.Defined(Operation, nameof(Operation));
        private readonly double _value = Operation == DeltaOperation.CapMagnitude && Value < 0d
            ? throw new ArgumentOutOfRangeException(nameof(Value), Value, "A magnitude cap cannot be negative.")
            : RecordValidation.Finite(Value, nameof(Value));
        private readonly DeltaDirection _appliesTo = RecordValidation.Defined(AppliesTo, nameof(AppliesTo));
        private readonly string? _customName = CustomOperationNames.Validate(Operation == DeltaOperation.Custom, CustomName);

        /// <summary>How the delta is transformed.</summary>
        public DeltaOperation Operation
        {
            get => _operation;
            init => _operation = RecordValidation.Defined(value, nameof(Operation));
        }

        /// <summary>The operand; ignored by <see cref="DeltaOperation.Block"/>.</summary>
        public double Value
        {
            get => _value;
            init => _value = RecordValidation.Finite(value, nameof(Value));
        }

        /// <summary>Which deltas the filter applies to.</summary>
        public DeltaDirection AppliesTo
        {
            get => _appliesTo;
            init => _appliesTo = RecordValidation.Defined(value, nameof(AppliesTo));
        }

        /// <summary>The registered operation name for <see cref="DeltaOperation.Custom"/>; otherwise null.</summary>
        public string? CustomName
        {
            get => _customName;
            init => _customName = RecordValidation.NullOrNonBlank(value, nameof(CustomName));
        }
    }
}
