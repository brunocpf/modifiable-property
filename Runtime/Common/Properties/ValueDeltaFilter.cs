using System;

namespace BrunoCPF.Modifiable.Common.Properties
{
    /// <summary>
    /// Represents a transformation applied to incoming deltas before accumulation.
    /// </summary>
    /// <param name="Id">Stable identifier used for replacement and removal.</param>
    /// <param name="FilterFunc">Function that receives and returns the delta.</param>
    public sealed record ValueDeltaFilter<TValue, TContext>(
        string Id,
        Func<ValueDelta<TValue, TContext>, ValueDelta<TValue, TContext>> FilterFunc
    );
}
