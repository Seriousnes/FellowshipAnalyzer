// Polyfill so that `record` / `init` accessors work when targeting netstandard2.0.
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit
{
}
