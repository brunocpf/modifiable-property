namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// How a <see cref="Modifier{TSource}"/> combines its operand with the incoming value.
    /// </summary>
    public enum ModifierOperation
    {
        /// <summary>value + operand.</summary>
        Add,

        /// <summary>value × operand.</summary>
        Multiply,

        /// <summary>Replace the value with the operand.</summary>
        Override,

        /// <summary>max(value, operand).</summary>
        AtLeast,

        /// <summary>min(value, operand).</summary>
        AtMost,

        /// <summary>
        /// A named operation from a <see cref="CustomOperations"/> registry, given by
        /// <see cref="Modifier{TSource}.CustomName"/>.
        /// </summary>
        Custom,
    }
}
