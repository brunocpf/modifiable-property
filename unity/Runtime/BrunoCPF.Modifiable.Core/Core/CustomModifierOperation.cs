namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// A named read-time operation for <see cref="ModifierOperation.Custom"/> modifiers, registered in
    /// <see cref="CustomOperations"/>. Works in <see cref="double"/>; the result is converted back (and
    /// rounded for integral types) like the built-in operations.
    /// </summary>
    /// <param name="value">The incoming value.</param>
    /// <param name="operand">The modifier's <see cref="Modifier{TSource}.Value"/>.</param>
    /// <returns>The transformed value; must be finite.</returns>
    public delegate double CustomModifierOperation(double value, double operand);
}
