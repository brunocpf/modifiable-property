namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// How modifiers that share a <see cref="Modifier.StackGroup"/> combine.
    /// </summary>
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
