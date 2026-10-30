namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// One filter that changed a delta, reported in <see cref="DeltaResult{TValue, TSource}.FiredFilters"/>.
    /// </summary>
    /// <param name="Priority">The filter priority.</param>
    /// <param name="Before">The delta entering the filter.</param>
    /// <param name="After">The delta leaving the filter.</param>
    /// <param name="Source">Where the filter came from, if it has a source.</param>
    /// <param name="Data">The data filter, or null for a function-based filter.</param>
    public sealed record DeltaStep<TValue, TSource>(
        int Priority,
        TValue Before,
        TValue After,
        TSource? Source,
        DeltaFilter<TSource>? Data)
        where TValue : struct;
}
