// Polyfill for the `init` accessor on netstandard2.0. The C#
// compiler needs IsExternalInit to be in scope when compiling
// `init`-only properties; the type lives in System.Runtime on
// .NET 5+ but is missing from the netstandard2.0 surface.
// Defining it here is the canonical workaround.
#if NETSTANDARD2_0
#pragma warning disable IDE0130 // namespace does not match folder structure (intentional polyfill)
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#pragma warning restore IDE0130
#endif
