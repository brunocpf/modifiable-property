namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// Which deltas a <see cref="DeltaFilter{TSource}"/> applies to.
    /// </summary>
    public enum DeltaDirection
    {
        /// <summary>Every delta.</summary>
        Any,

        /// <summary>Only positive deltas (healing, gains).</summary>
        Increase,

        /// <summary>Only negative deltas (damage, costs).</summary>
        Decrease,
    }
}
