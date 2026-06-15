namespace BrunoCPF.Modifiable.Common.Properties
{
    /// <summary>
    /// Represents a change applied to a property, carrying the delta and optional context.
    /// </summary>
    public sealed record ValueDelta<TValue, TContext>(TValue Delta, TContext? Context);
}
