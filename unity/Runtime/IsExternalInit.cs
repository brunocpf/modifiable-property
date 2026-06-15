// Polyfill enabling `init` accessors / records on netstandard2.1 (Unity 6, and the
// SDK library's netstandard2.1 target). On net5.0+ this type ships in the BCL, so the
// guard prevents a CS0436 duplicate-definition clash if the code is ever retargeted.
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
