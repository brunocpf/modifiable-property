namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// How modifiers that share a <see cref="Modifier{TSource}.StackGroup"/> combine.
    /// </summary>
    /// <remarks>
    /// <see cref="HighestOnly"/> and <see cref="LowestOnly"/> compare raw operands, so every modifier in
    /// such a group must use the same operation (and custom name); evaluation throws otherwise. Pick the
    /// policy by what "strongest" means for the group: <see cref="HighestOnly"/> for bonuses
    /// (+30 beats +10, ×1.5 beats ×1.2), <see cref="LowestOnly"/> for penalties (−30 beats −10, ×0.5
    /// beats ×0.8).
    /// </remarks>
    public enum StackPolicy
    {
        /// <summary>Every modifier in the group applies.</summary>
        Stack,

        /// <summary>Only the modifier with the largest operand applies; ties keep the earliest.</summary>
        HighestOnly,

        /// <summary>Only the modifier with the smallest operand applies; ties keep the earliest.</summary>
        LowestOnly,
    }
}
