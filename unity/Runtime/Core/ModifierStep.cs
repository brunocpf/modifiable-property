namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// One modifier's contribution in a <see cref="ModifierBreakdown{TValue}"/>.
    /// </summary>
    /// <param name="Id">The modifier id.</param>
    /// <param name="Priority">The modifier priority.</param>
    /// <param name="Before">The value entering this step.</param>
    /// <param name="After">The value leaving this step (equal to <paramref name="Before"/> when suppressed).</param>
    /// <param name="Suppressed">Whether a <see cref="StackPolicy"/> excluded this modifier.</param>
    /// <param name="Data">The data modifier, or null for a function-based modifier.</param>
    public sealed record ModifierStep<TValue>(
        string Id,
        int Priority,
        TValue Before,
        TValue After,
        bool Suppressed,
        Modifier? Data)
        where TValue : struct;
}
