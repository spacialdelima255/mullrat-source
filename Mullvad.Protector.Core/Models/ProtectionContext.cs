using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Mullvad.Protector.Core.Models;

public enum ProtectionMode { Standard, Strong }

public sealed class ProtectionContext
{
    public required ModuleDefMD Module { get; init; }
    public required string InputPath { get; init; }
    public required string OutputPath { get; init; }
    public required ProtectionMode Mode { get; init; }

    // Symbols eligible for renaming
    public List<TypeDef> RenamableTypes { get; } = new();
    public List<MethodDef> RenamableMethods { get; } = new();
    public List<FieldDef> RenamableFields { get; } = new();
    public List<PropertyDef> RenamableProperties { get; } = new();
    public List<EventDef> RenamableEvents { get; } = new();

    // String entries: key = original string, value = entry with index + encrypted data
    public Dictionary<string, StringEntry> StringTable { get; } = new(StringComparer.Ordinal);

    // All ldstr call sites that have been mapped to a string entry
    public List<StringCallSite> StringCallSites { get; } = new();

    // Methods eligible for control flow transformation
    public List<MethodDef> CfMethods { get; } = new();

    // Methods eligible for constant protection
    public List<MethodDef> ConstantMethods { get; } = new();

    // The injected decoder type reference (set by StringProtector)
    public TypeDef? DecoderType { get; set; }
    public MethodDef? DecoderMethod { get; set; }

    // Progress / logging
    public Action<string>? Log { get; set; }

    // Symbols that MUST NOT be renamed (entry point, P/Invoke targets, etc.)
    public HashSet<IMemberDef> SkipRename { get; } = new(ReferenceEqualityComparer.Instance);

    // Type + method token of decoder class — excluded from string/CF passes
    public HashSet<MethodDef> SkipStringProtect { get; } = new(ReferenceEqualityComparer.Instance);

    // Virtual override methods whose bases are in external assemblies.
    // SymbolRenamer adds an explicit .override MemberRef before renaming these
    // so the CLR can still resolve the vtable slot after the name changes.
}

public sealed class StringEntry
{
    public int Index { get; init; }
    public byte[] EncryptedBytes { get; init; } = Array.Empty<byte>();
    public int Key { get; init; }
}

public sealed class StringCallSite
{
    public MethodDef ContainingMethod { get; init; } = null!;
    public Instruction LdstrInstruction { get; init; } = null!;
    public StringEntry Entry { get; init; } = null!;
}
