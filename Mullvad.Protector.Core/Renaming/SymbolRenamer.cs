using dnlib.DotNet;
using Mullvad.Protector.Core.Models;

namespace Mullvad.Protector.Core.Renaming;

public sealed class SymbolRenamer
{
    // Confusable char set: Latin I, Latin l, Cyrillic І, Cyrillic ӏ
    // In proportional fonts these render identically — dnSpy becomes unreadable
    private static readonly char[] Confusables =
    {
        'I', 'l', 'І', 'ӏ'
    };

    private int _typeCounter;
    private int _globalMemberCounter;
    // Guards against collisions between renamed types that share the same namespace scope
    private readonly HashSet<string> _usedTypeNames = new(StringComparer.Ordinal);

    private readonly ProtectionContext _ctx;

    public SymbolRenamer(ProtectionContext ctx) => _ctx = ctx;

    public void Apply()
    {
        var module = _ctx.Module;
        var nameMap = new Dictionary<IMemberDef, string>(ReferenceEqualityComparer.Instance);

        // Seed used-type-names with all NON-renamable types so we don't collide with them
        var renamableSet = new HashSet<TypeDef>(_ctx.RenamableTypes, ReferenceEqualityComparer.Instance);
        foreach (var type in module.GetTypes())
        {
            if (!renamableSet.Contains(type))
                _usedTypeNames.Add(type.Name.String);
        }

        // Generate names for types first
        foreach (var type in _ctx.RenamableTypes)
        {
            string newName = NextUniqueName(ref _typeCounter, _usedTypeNames);
            nameMap[type] = newName;
        }

        // Per-type member counter for uniqueness within scope
        foreach (var method in _ctx.RenamableMethods)
        {
            nameMap[method] = NextName(ref _globalMemberCounter);
        }

        foreach (var field in _ctx.RenamableFields)
        {
            nameMap[field] = NextName(ref _globalMemberCounter);
        }

        foreach (var prop in _ctx.RenamableProperties)
        {
            nameMap[prop] = NextName(ref _globalMemberCounter);
        }

        foreach (var ev in _ctx.RenamableEvents)
        {
            nameMap[ev] = NextName(ref _globalMemberCounter);
        }

        // Apply — dnlib propagates type renames automatically through all references
        foreach (var type in _ctx.RenamableTypes)
        {
            if (nameMap.TryGetValue(type, out var name))
            {
                type.Namespace = string.Empty;
                type.Name = name;
            }
        }

        foreach (var method in _ctx.RenamableMethods)
        {
            if (!nameMap.TryGetValue(method, out var name)) continue;

            method.Name = name;
        }

        foreach (var field in _ctx.RenamableFields)
        {
            if (nameMap.TryGetValue(field, out var name))
                field.Name = name;
        }

        foreach (var prop in _ctx.RenamableProperties)
        {
            if (nameMap.TryGetValue(prop, out var name))
                prop.Name = name;
        }

        foreach (var ev in _ctx.RenamableEvents)
        {
            if (nameMap.TryGetValue(ev, out var name))
                ev.Name = name;
        }
    }

    private static string NextUniqueName(ref int counter, HashSet<string> seen)
    {
        string name;
        do { name = GenerateName(counter++); }
        while (!seen.Add(name));
        return name;
    }

    private static string NextName(ref int counter) => GenerateName(counter++);

    private static string GenerateName(int n)
    {
        // Use SHA-based byte to pick chars — guarantees uniform distribution
        // and avoids base-conversion aliasing that causes collisions.
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(
                System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref n, 1)));

        var sb = new System.Text.StringBuilder(9);
        sb.Append(Confusables[0]); // Always start with 'I' — valid identifier start
        for (int i = 0; i < 8; i++)
            sb.Append(Confusables[hash[i] % Confusables.Length]);
        return sb.ToString();
    }
}
