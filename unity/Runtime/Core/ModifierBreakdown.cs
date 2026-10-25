using System.Collections.Generic;

namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// A step-by-step account of how modifiers turned a base value into a result, for tooltips and
    /// debugging ("Base 40 → +10 (Sword) → ×1.2 (Boost) = 60").
    /// </summary>
    /// <param name="Base">The value before any modifier.</param>
    /// <param name="Result">The value after every applied modifier.</param>
    /// <param name="Steps">Each modifier in evaluation order, including suppressed ones.</param>
    public sealed record ModifierBreakdown<TValue, TSource>(
        TValue Base,
        TValue Result,
        IReadOnlyList<ModifierStep<TValue, TSource>> Steps)
        where TValue : struct;
}
