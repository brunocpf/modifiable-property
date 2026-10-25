namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// How a <see cref="DeltaFilter"/> transforms an incoming delta.
    /// </summary>
    public enum DeltaOperation
    {
        /// <summary>delta × operand (damage reduction, EXP boost).</summary>
        Scale,

        /// <summary>delta + operand (flat reduction or bonus).</summary>
        Add,

        /// <summary>Keep the sign but limit the magnitude to the operand (damage cap).</summary>
        CapMagnitude,

        /// <summary>Replace the delta with zero (healing block); the operand is ignored.</summary>
        Block,
    }
}
