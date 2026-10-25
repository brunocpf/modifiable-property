namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// A named change-time operation for <see cref="DeltaOperation.Custom"/> filters, registered in
    /// <see cref="CustomOperations"/>. Works in <see cref="double"/>; the result is converted back (and
    /// rounded for integral types) like the built-in operations.
    /// </summary>
    /// <param name="delta">The incoming delta.</param>
    /// <param name="operand">The filter's <see cref="DeltaFilter{TSource}.Value"/>.</param>
    /// <returns>The transformed delta; must be finite.</returns>
    public delegate double CustomDeltaOperation(double delta, double operand);
}
