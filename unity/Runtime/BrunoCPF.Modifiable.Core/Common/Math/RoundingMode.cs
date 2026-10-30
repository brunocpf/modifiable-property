namespace BrunoCPF.Modifiable.Common.Math
{
    /// <summary>
    /// How an integral value type rounds the fractional results of scaling operations.
    /// </summary>
    public enum RoundingMode
    {
        /// <summary>Drop the fraction (2.7 → 2, -2.7 → -2). Symmetric for gains and losses.</summary>
        TowardZero,

        /// <summary>Round down (2.7 → 2, -2.7 → -3).</summary>
        Floor,

        /// <summary>Round up (2.2 → 3, -2.2 → -2).</summary>
        Ceiling,

        /// <summary>Round to the nearest integer, with midpoints away from zero (2.5 → 3, -2.5 → -3).</summary>
        Nearest,
    }
}
