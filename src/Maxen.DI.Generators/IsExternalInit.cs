// netstandard2.0 doesn't ship this marker type, but the compiler needs it to accept
// records and init-only properties. Declaring it ourselves is the standard workaround.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
