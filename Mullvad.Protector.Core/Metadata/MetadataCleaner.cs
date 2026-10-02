using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Mullvad.Protector.Core.Models;

namespace Mullvad.Protector.Core.Metadata;

/// <summary>
/// Strips metadata that reveals implementation details:
///  - DebuggableAttribute (disables JIT debug info)
///  - CompilationRelaxationsAttribute
///  - AssemblyInformationalVersionAttribute / AssemblyFileVersionAttribute
///  - Sequence points / PDB references from method bodies
///  - Removes module-level debug attributes
/// </summary>
public sealed class MetadataCleaner
{
    private static readonly HashSet<string> StripAttributes = new()
    {
        "System.Diagnostics.DebuggableAttribute",
        "System.Runtime.CompilerServices.CompilationRelaxationsAttribute",
        "System.Runtime.CompilerServices.ReferenceAssemblyAttribute",
        "System.Reflection.AssemblyInformationalVersionAttribute",
        "System.Reflection.AssemblyFileVersionAttribute",
        "System.Reflection.AssemblyDescriptionAttribute",
        "System.Reflection.AssemblyCompanyAttribute",
        "System.Reflection.AssemblyCopyrightAttribute",
        "System.Reflection.AssemblyTrademarkAttribute",
        "System.Reflection.AssemblyProductAttribute",
        "System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute",
    };

    private readonly ProtectionContext _ctx;

    public MetadataCleaner(ProtectionContext ctx) => _ctx = ctx;

    public void Apply()
    {
        var module = _ctx.Module;

        // Strip module-level custom attributes
        for (int i = module.CustomAttributes.Count - 1; i >= 0; i--)
        {
            if (StripAttributes.Contains(module.CustomAttributes[i].TypeFullName))
                module.CustomAttributes.RemoveAt(i);
        }

        // Strip assembly-level custom attributes
        var asm = module.Assembly;
        if (asm is not null)
        {
            for (int i = asm.CustomAttributes.Count - 1; i >= 0; i--)
            {
                if (StripAttributes.Contains(asm.CustomAttributes[i].TypeFullName))
                    asm.CustomAttributes.RemoveAt(i);
            }
        }

        // Strip per-type and per-method attributes + sequence points
        foreach (var type in module.GetTypes())
        {
            StripTypeAttributes(type);

            foreach (var method in type.Methods)
            {
                StripMethodAttributes(method);
                StripSequencePoints(method);
            }
        }

        // PDB state is handled by writer options — sequence points already stripped above
    }

    private static void StripTypeAttributes(TypeDef type)
    {
        for (int i = type.CustomAttributes.Count - 1; i >= 0; i--)
        {
            var name = type.CustomAttributes[i].TypeFullName;
            if (StripAttributes.Contains(name) ||
                name == "System.Runtime.CompilerServices.NullableContextAttribute" ||
                name == "System.Runtime.CompilerServices.NullableAttribute")
                type.CustomAttributes.RemoveAt(i);
        }
    }

    private static void StripMethodAttributes(MethodDef method)
    {
        for (int i = method.CustomAttributes.Count - 1; i >= 0; i--)
        {
            var name = method.CustomAttributes[i].TypeFullName;
            if (StripAttributes.Contains(name) ||
                name == "System.Runtime.CompilerServices.NullableContextAttribute")
                method.CustomAttributes.RemoveAt(i);
        }

        // Remove param custom attributes that reveal names
        foreach (var param in method.ParamDefs)
        {
            for (int i = param.CustomAttributes.Count - 1; i >= 0; i--)
                param.CustomAttributes.RemoveAt(i);
        }
    }

    private static void StripSequencePoints(MethodDef method)
    {
        if (method.Body?.Instructions is null) return;
        foreach (var instr in method.Body.Instructions)
            instr.SequencePoint = null;
    }
}
