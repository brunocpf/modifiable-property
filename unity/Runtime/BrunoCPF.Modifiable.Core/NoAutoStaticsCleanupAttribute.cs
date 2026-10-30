// Polyfill for Unity 7's lifecycle analyzer (UAL0010/UAL0013), which asks every type with static
// fields or auto-properties to declare how its statics are reset between Play sessions. The real
// attribute lives in UnityEngine, which this engine-free assembly can't reference; the analyzer
// matches the attribute by full name, so this internal copy satisfies it. It marks statics that
// hold immutable, stateless values and stay valid across reloads. Outside Unity it does nothing.
namespace Unity.Scripting.LifecycleManagement
{
    [System.AttributeUsage(
        System.AttributeTargets.Class | System.AttributeTargets.Struct | System.AttributeTargets.Field |
        System.AttributeTargets.Property | System.AttributeTargets.Event,
        Inherited = false)]
    internal sealed class NoAutoStaticsCleanupAttribute : System.Attribute
    {
    }
}
