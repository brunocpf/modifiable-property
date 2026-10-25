using System.Collections.Generic;

namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// The outcome of running one delta through filters and bounds.
    /// </summary>
    /// <param name="Before">The stored value before the change.</param>
    /// <param name="Requested">The delta as requested.</param>
    /// <param name="Filtered">The delta after filters, before bounds.</param>
    /// <param name="After">The stored value after filters and bounds.</param>
    /// <param name="Applied">The change actually made (<paramref name="After"/> − <paramref name="Before"/>).</param>
    /// <param name="FiredFilters">Ids of the filters that changed the delta, in evaluation order.</param>
    public sealed record DeltaResult<TValue>(
        TValue Before,
        TValue Requested,
        TValue Filtered,
        TValue After,
        TValue Applied,
        IReadOnlyList<string> FiredFilters)
        where TValue : struct;
}
