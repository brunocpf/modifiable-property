using System;

namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// A closure-free, change-time delta transformation. Unlike
    /// <see cref="Common.Properties.ValueDeltaFilter{TValue, TContext}"/>, it can be compared, copied,
    /// saved and described, and <see cref="DeltaPipeline"/> reports when it fired.
    /// </summary>
    /// <param name="Id">Stable identifier used for replacement, removal and fired-filter reports.</param>
    /// <param name="Operation">How the delta is transformed.</param>
    /// <param name="Value">The operand; ignored by <see cref="DeltaOperation.Block"/>.</param>
    /// <param name="AppliesTo">Which deltas the filter applies to.</param>
    /// <param name="Priority">Lower numbers run earlier; equal priorities keep their given order.</param>
    /// <param name="CustomName">
    /// The registered operation name; required for <see cref="DeltaOperation.Custom"/> and not
    /// allowed otherwise.
    /// </param>
    public sealed record DeltaFilter(
        string Id,
        DeltaOperation Operation,
        double Value = 0d,
        DeltaDirection AppliesTo = DeltaDirection.Any,
        int Priority = 0,
        string? CustomName = null)
    {
        /// <summary>Stable identifier used for replacement, removal and fired-filter reports.</summary>
        public string Id { get; } = string.IsNullOrWhiteSpace(Id)
            ? throw new ArgumentException("Delta filter id cannot be blank.", nameof(Id))
            : Id;

        /// <summary>How the delta is transformed.</summary>
        public DeltaOperation Operation { get; } = Enum.IsDefined(typeof(DeltaOperation), Operation)
            ? Operation
            : throw new ArgumentOutOfRangeException(nameof(Operation), Operation, null);

        /// <summary>The operand; ignored by <see cref="DeltaOperation.Block"/>.</summary>
        public double Value { get; } = !double.IsFinite(Value)
            ? throw new ArgumentOutOfRangeException(nameof(Value), Value, "Delta filter value must be finite.")
            : Operation == DeltaOperation.CapMagnitude && Value < 0d
                ? throw new ArgumentOutOfRangeException(nameof(Value), Value, "A magnitude cap cannot be negative.")
                : Value;

        /// <summary>Which deltas the filter applies to.</summary>
        public DeltaDirection AppliesTo { get; } = Enum.IsDefined(typeof(DeltaDirection), AppliesTo)
            ? AppliesTo
            : throw new ArgumentOutOfRangeException(nameof(AppliesTo), AppliesTo, null);

        /// <summary>The registered operation name for <see cref="DeltaOperation.Custom"/>; otherwise null.</summary>
        public string? CustomName { get; } = CustomOperationNames.Validate(Operation == DeltaOperation.Custom, CustomName);
    }
}
