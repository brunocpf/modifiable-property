namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// One modifier's contribution in a <see cref="ModifierBreakdown{TValue, TSource}"/>.
    /// </summary>
    /// <param name="Priority">The modifier priority.</param>
    /// <param name="Before">The value entering this step.</param>
    /// <param name="After">The value leaving this step (equal to <paramref name="Before"/> when suppressed).</param>
    /// <param name="Suppressed">Whether a <see cref="StackPolicy"/> excluded this modifier.</param>
    /// <param name="Source">Where the modifier came from, if it has a source.</param>
    /// <param name="Data">The data modifier, or null for a function-based modifier.</param>
    public sealed record ModifierStep<TValue, TSource>(
        int Priority,
        TValue Before,
        TValue After,
        bool Suppressed,
        TSource? Source,
        Modifier<TSource>? Data)
        where TValue : struct;
}
