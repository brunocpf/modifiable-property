namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// Detects operands that can be applied with the value type's own arithmetic instead of through
    /// <see cref="double"/>.
    /// </summary>
    internal static class ExactOperands
    {
        // 2^53: every integer up to this magnitude is exactly representable as a double.
        private const double MaxExactInteger = 9007199254740992d;

        public static bool IsInteger(double operand) =>
            operand == System.Math.Truncate(operand) && System.Math.Abs(operand) <= MaxExactInteger;
    }
}
