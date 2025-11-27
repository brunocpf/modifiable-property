using System;

namespace BrunoCPF.Modifiable.Common.Properties
{
    /// <summary>
    /// Represents a transformation applied to the base value after deltas are accumulated.
    /// </summary>
    /// <param name="Id">Stable identifier used for replacement and removal.</param>
    /// <param name="ModifyFunc">Function that returns the transformed value.</param>
    /// <param name="Priority">Lower numbers run earlier.</param>
    public sealed record ValueModifier<TValue>(string Id, Func<TValue, TValue> ModifyFunc, int Priority = 0);
}
