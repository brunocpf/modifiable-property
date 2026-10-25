// Minimal stand-ins for the UnityEngine / R3.Unity APIs the sample uses, so it can be compiled
// outside Unity. Extend this only when a sample needs another engine type.
namespace UnityEngine
{
    public class MonoBehaviour
    {
    }

    public static class Debug
    {
        public static void Log(object message)
        {
        }
    }
}

namespace R3
{
    public static class MonoBehaviourDisposableExtensions
    {
        public static T AddTo<T>(this T disposable, UnityEngine.MonoBehaviour behaviour)
            where T : System.IDisposable => disposable;
    }
}
