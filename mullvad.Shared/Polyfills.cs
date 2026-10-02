// Allows C# 9+ init-only properties and record types when targeting netstandard2.0.
// The compiler references these names; providing them here satisfies the lookup
// without requiring a newer TFM.

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
