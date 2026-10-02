using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Mullvad.Protector.Core.Models;

namespace Mullvad.Protector.Core.Analyzer;

public sealed class AssemblyAnalyzer
{
    private readonly ProtectionContext _ctx;

    public AssemblyAnalyzer(ProtectionContext ctx) => _ctx = ctx;

    public void Analyze()
    {
        var module = _ctx.Module;

        // Mark entry point as skip-rename
        if (module.EntryPoint is { } ep)
            _ctx.SkipRename.Add(ep);

        foreach (var type in module.GetTypes())
        {
            if (IsCompilerGenerated(type))
                continue;

            if (IsRenamableType(type))
                _ctx.RenamableTypes.Add(type);

            foreach (var method in type.Methods)
            {
                if (!method.HasBody)
                    continue;

                AnalyzeMethod(method, type);
            }

            foreach (var field in type.Fields)
            {
                if (IsRenamableField(field))
                    _ctx.RenamableFields.Add(field);
            }

            foreach (var prop in type.Properties)
            {
                if (IsRenamableMember(prop))
                    _ctx.RenamableProperties.Add(prop);
            }

            foreach (var ev in type.Events)
            {
                if (IsRenamableMember(ev))
                    _ctx.RenamableEvents.Add(ev);
            }
        }
    }

    private void AnalyzeMethod(MethodDef method, TypeDef owner)
    {
        // Rename eligibility
        if (IsRenamableMethod(method))
            _ctx.RenamableMethods.Add(method);

        var body = method.Body;
        if (body is null) return;

        body.SimplifyBranches();
        body.SimplifyMacros(method.Parameters);

        // Collect ldstr instructions
        foreach (var instr in body.Instructions)
        {
            if (instr.OpCode == OpCodes.Ldstr && instr.Operand is string s && s.Length > 0)
            {
                if (!_ctx.StringTable.ContainsKey(s))
                {
                    int idx = _ctx.StringTable.Count;
                    byte[] plain = System.Text.Encoding.UTF8.GetBytes(s);
                    int key = RandomKey();
                    byte[] enc = EncryptString(plain, key);
                    _ctx.StringTable[s] = new StringEntry
                    {
                        Index = idx,
                        EncryptedBytes = enc,
                        Key = key
                    };
                }

                _ctx.StringCallSites.Add(new StringCallSite
                {
                    ContainingMethod = method,
                    LdstrInstruction = instr,
                    Entry = _ctx.StringTable[s]
                });
            }
        }

        // CF eligibility: skip methods with exception handlers for safety
        if (!body.HasExceptionHandlers && body.Instructions.Count > 6)
            _ctx.CfMethods.Add(method);

        // Constant eligibility
        bool hasConst = body.Instructions.Any(i =>
            i.OpCode == OpCodes.Ldc_I4 ||
            i.OpCode == OpCodes.Ldc_I4_S ||
            (i.OpCode.Code >= Code.Ldc_I4_0 && i.OpCode.Code <= Code.Ldc_I4_8));

        if (hasConst)
            _ctx.ConstantMethods.Add(method);
    }

    private bool IsRenamableType(TypeDef type)
    {
        if (type.IsGlobalModuleType) return false;
        if (type.IsWindowsRuntime) return false;
        if (HasAttribute(type, "System.Runtime.InteropServices.ComVisibleAttribute")) return false;
        if (HasAttribute(type, "System.Runtime.InteropServices.ClassInterfaceAttribute")) return false;
        if (_ctx.SkipRename.Contains(type)) return false;
        // WinForms/WPF: ComponentResourceManager(typeof(T)) looks up embedded resources
        // by type.FullName at runtime. If we rename the type the lookup fails silently
        // and the form loads with no controls, no size, and appears invisible.
        if (HasOwnedResource(type)) return false;
        return true;
    }

    private bool HasOwnedResource(TypeDef type)
    {
        string fullName = (type.Namespace.Length > 0 ? type.Namespace + "." : "") + type.Name;
        if (fullName.Length == 0) return false;
        foreach (var res in _ctx.Module.Resources)
        {
            var rn = res.Name.String;
            if (rn == fullName || rn.StartsWith(fullName + ".", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    // Well-known virtual method names that could override external base class members.
    // Renaming these breaks the vtable slot — skip them all conservatively.
    private static readonly HashSet<string> ReservedVirtualNames = new(StringComparer.Ordinal)
    {
        "Equals", "GetHashCode", "ToString", "Finalize", "CompareTo",
        "CompareOrdinal", "GetEnumerator", "MoveNext", "Current", "Dispose",
        "Reset", "op_Equality", "op_Inequality", "op_LessThan", "op_GreaterThan",
        "op_LessThanOrEqual", "op_GreaterThanOrEqual", "GetObjectData",
        "GetBaseException", "Clone", "CopyTo", "Add", "Remove", "Clear",
        "Contains", "Count", "IndexOf", "Insert", "RemoveAt", "GetType",
    };

    private bool IsRenamableMethod(MethodDef method)
    {
        if (_ctx.SkipRename.Contains(method)) return false;
        if (method.IsConstructor || method.IsStaticConstructor) return false;
        if (method.IsPinvokeImpl) return false;
        if (method.IsRuntime) return false;
        if (method.Name == "Main") return false;

        // Skip explicit interface implementations — their names encode the interface+method
        if (method.HasOverrides) return false;

        // Skip overrides of any virtual method that might originate from an external type
        if (method.IsVirtual)
        {
            if (ReservedVirtualNames.Contains(method.Name)) return false;
            if (!method.IsNewSlot && HasExternalBase(method))
                return false;
        }

        return true;
    }

    private bool IsRenamableField(FieldDef field)
    {
        if (_ctx.SkipRename.Contains(field)) return false;
        if (field.DeclaringType.IsSerializable) return false;
        return true;
    }

    private bool IsRenamableMember(IMemberDef member)
    {
        return !_ctx.SkipRename.Contains(member);
    }

    private static bool HasExternalBase(MethodDef method)
    {
        var baseRef = method.DeclaringType.BaseType;
        while (baseRef != null)
        {
            var baseDef = baseRef.ResolveTypeDef();
            if (baseDef is null)
                return true; // unresolvable → external assembly
            if (baseDef.Module != method.Module)
                return true; // resolved but in different module
            baseRef = baseDef.BaseType;
        }
        return false;
    }

    private static bool IsCompilerGenerated(TypeDef type) =>
        HasAttribute(type, "System.Runtime.CompilerServices.CompilerGeneratedAttribute");

    private static bool HasAttribute(IHasCustomAttribute member, string fullName) =>
        member.CustomAttributes.Any(a => a.TypeFullName == fullName);

    private static readonly Random _rng = new();

    private static int RandomKey() => _rng.Next(1, 256);

    private static byte[] EncryptString(byte[] plain, int key)
    {
        var enc = new byte[plain.Length];
        for (int i = 0; i < plain.Length; i++)
            enc[i] = (byte)(plain[i] ^ (key ^ i));
        return enc;
    }
}
