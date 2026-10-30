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
    /// <param name="FiredFilters">The filters that changed the delta, in evaluation order, with their sources.</param>
    public sealed record DeltaResult<TValue, TSource>(
        TValue Before,
        TValue Requested,
        TValue Filtered,
        TValue After,
        TValue Applied,
        IReadOnlyList<DeltaStep<TValue, TSource>> FiredFilters)
        where TValue : struct;
}
