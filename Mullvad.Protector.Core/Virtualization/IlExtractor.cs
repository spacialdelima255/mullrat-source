using System.IO;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Mullvad.Protector.Core.Virtualization;

internal sealed class ExtractedMethod
{
    public byte[]     IlBytes    { get; set; } = [];
    public int        MaxStack   { get; set; }
    public string[]   TokenTable { get; set; } = []; // "offset\x01kind\x01value"
    public string[]   Signature  { get; set; } = []; // [0]=ret ("" = void), [1..]=params
}

/// <summary>
/// Serialises a dnlib CilBody to raw IL bytes + a string-keyed token table.
/// Only eligible methods (no locals, no EH, ≤120 instructions, no generics) are extracted.
/// </summary>
internal static class IlExtractor
{
    public static ExtractedMethod? TryExtract(MethodDef method, ModuleDefMD module)
    {
        var body = method.Body;
        if (body is null) return null;
        if (body.HasExceptionHandlers)      return null;
        if (body.Variables.Count > 0)       return null;  // locals unsupported in v1
        if (body.Instructions.Count < 2)    return null;
        if (body.Instructions.Count > 120)  return null;
        if (method.IsConstructor || method.IsStaticConstructor) return null;
        if (method.HasGenericParameters)    return null;
        if (method.DeclaringType.HasGenericParameters) return null;
        if (method.DeclaringType.IsValueType) return null; // struct 'this' is byref
        // Cap params to avoid overflowing Action<>/Func<> generic arities (max 16)
        int totalParams = (method.IsStatic ? 0 : 1) + method.MethodSig.Params.Count;
        if (totalParams > 10) return null;

        try
        {
            // Canonical form — all branches long, no macro opcodes
            body.SimplifyBranches();
            body.SimplifyMacros(method.Parameters);

            var (ilBytes, rawTokenRefs) = SerializeIL(body);

            var tokTable = BuildTokenTable(rawTokenRefs, module);
            if (tokTable is null) return null;

            var sig = BuildSignature(method, module);
            if (sig is null) return null;

            return new ExtractedMethod
            {
                IlBytes    = ilBytes,
                MaxStack   = Math.Max((int)body.MaxStack, 8),
                TokenTable = tokTable,
                Signature  = sig,
            };
        }
        catch { return null; }
    }

    // ------------------------------------------------------------------
    // IL serialisation
    // ------------------------------------------------------------------

    private static (byte[], List<(int Offset, object Operand, OperandType OpKind)>)
        SerializeIL(CilBody body)
    {
        var instrs = body.Instructions;
        int count  = instrs.Count;
        var offsets = new int[count];
        var sizes   = new int[count];

        // Pass 1: measure
        int pos = 0;
        for (int i = 0; i < count; i++)
        {
            offsets[i] = pos;
            sizes[i]   = MeasureInstr(instrs[i]);
            pos += sizes[i];
        }

        var byInstr = new Dictionary<Instruction, int>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < count; i++) byInstr[instrs[i]] = i;

        // Pass 2: encode
        var bytes  = new byte[pos];
        var tokens = new List<(int, object, OperandType)>();
        int w = 0;

        for (int i = 0; i < count; i++)
        {
            var instr = instrs[i];
            var op    = instr.OpCode;

            // Opcode bytes
            if (op.Size == 2) bytes[w++] = 0xFE;
            bytes[w++] = op.Size == 2 ? (byte)((int)op.Value & 0xFF) : (byte)(int)op.Value;

            switch (op.OperandType)
            {
                case OperandType.InlineNone: break;

                case OperandType.InlineI:
                    Wi32(bytes, w, (int)instr.Operand!); w += 4; break;

                case OperandType.InlineI8:
                    Wi64(bytes, w, (long)instr.Operand!); w += 8; break;

                case OperandType.ShortInlineR:
                    BitConverter.GetBytes((float)instr.Operand!).CopyTo(bytes, w); w += 4; break;

                case OperandType.InlineR:
                    BitConverter.GetBytes((double)instr.Operand!).CopyTo(bytes, w); w += 8; break;

                case OperandType.InlineVar:
                {
                    ushort idx = instr.Operand is Local lv
                        ? (ushort)lv.Index
                        : (ushort)((Parameter)instr.Operand!).Index;
                    bytes[w] = (byte)idx; bytes[w + 1] = (byte)(idx >> 8); w += 2;
                    break;
                }

                case OperandType.InlineBrTarget:
                {
                    var tgt    = (Instruction)instr.Operand!;
                    int tgtOff = offsets[byInstr[tgt]];
                    int next   = offsets[i] + sizes[i];
                    Wi32(bytes, w, tgtOff - next); w += 4;
                    break;
                }

                case OperandType.InlineSwitch:
                {
                    var tgts   = (IList<Instruction>)instr.Operand!;
                    int switchNext = offsets[i] + sizes[i];
                    Wi32(bytes, w, tgts.Count); w += 4;
                    foreach (var t in tgts) { Wi32(bytes, w, offsets[byInstr[t]] - switchNext); w += 4; }
                    break;
                }

                case OperandType.InlineString:
                case OperandType.InlineMethod:
                case OperandType.InlineType:
                case OperandType.InlineField:
                case OperandType.InlineTok:
                    tokens.Add((w, instr.Operand!, op.OperandType));
                    Wi32(bytes, w, 0); w += 4;
                    break;

                case OperandType.InlineSig:
                    throw new NotSupportedException("calli");

                // Short forms should not survive Simplify*, but handle defensively
                case OperandType.ShortInlineI:
                    bytes[w++] = instr.Operand is sbyte sb ? (byte)sb : (byte)(int)instr.Operand!;
                    break;
                case OperandType.ShortInlineBrTarget:
                {
                    var tgt  = (Instruction)instr.Operand!;
                    int next = offsets[i] + sizes[i];
                    bytes[w++] = (byte)(offsets[byInstr[tgt]] - next);
                    break;
                }
                case OperandType.ShortInlineVar:
                    bytes[w++] = instr.Operand is Local slv
                        ? (byte)slv.Index
                        : (byte)((Parameter)instr.Operand!).Index;
                    break;
            }
        }

        return (bytes, tokens);
    }

    private static int MeasureInstr(Instruction instr)
    {
        int sz = instr.OpCode.Size;
        sz += instr.OpCode.OperandType switch
        {
            OperandType.InlineNone         => 0,
            OperandType.InlineI            => 4,
            OperandType.InlineI8           => 8,
            OperandType.ShortInlineR       => 4,
            OperandType.InlineR            => 8,
            OperandType.InlineVar          => 2,
            OperandType.InlineBrTarget     => 4,
            OperandType.InlineSwitch       => 4 + ((IList<Instruction>)instr.Operand!).Count * 4,
            OperandType.InlineString       => 4,
            OperandType.InlineMethod       => 4,
            OperandType.InlineType         => 4,
            OperandType.InlineField        => 4,
            OperandType.InlineTok          => 4,
            OperandType.InlineSig          => 4,
            OperandType.ShortInlineI       => 1,
            OperandType.ShortInlineBrTarget=> 1,
            OperandType.ShortInlineVar     => 1,
            _                              => 0,
        };
        return sz;
    }

    // ------------------------------------------------------------------
    // Token table
    // ------------------------------------------------------------------

    private static string[]? BuildTokenTable(
        List<(int Offset, object Operand, OperandType OpKind)> refs,
        ModuleDefMD module)
    {
        var entries = new List<string>(refs.Count);
        foreach (var (off, operand, opKind) in refs)
        {
            string? entry = opKind switch
            {
                OperandType.InlineString => EncodeString(off, operand),
                OperandType.InlineMethod => EncodeMethod(off, operand as IMethod, module),
                OperandType.InlineType   => EncodeType(off, operand as ITypeDefOrRef, module),
                OperandType.InlineField  => EncodeField(off, operand as IField, module),
                OperandType.InlineTok    => EncodeTok(off, operand, module),
                _                        => null,
            };
            if (entry is null) return null;
            entries.Add(entry);
        }
        return [.. entries];
    }

    private static string? EncodeString(int off, object operand) =>
        operand is string s ? $"{off}\x01S\x01{s}" : null;

    private static string? EncodeType(int off, ITypeDefOrRef? tdr, ModuleDefMD module)
    {
        string? name = TypeName(tdr, module);
        return name is null ? null : $"{off}\x01T\x01{name}";
    }

    private static string? EncodeField(int off, IField? field, ModuleDefMD module)
    {
        if (field is null) return null;
        var fd = field.ResolveFieldDef();
        string? typeName, fieldName;
        if (fd is not null)
        {
            typeName  = TypeName(fd.DeclaringType, module);
            fieldName = fd.Name.String;
        }
        else if (field is MemberRef mr)
        {
            typeName  = TypeName(mr.DeclaringType as ITypeDefOrRef, module);
            fieldName = mr.Name.String;
        }
        else return null;

        return typeName is null ? null : $"{off}\x01F\x01{typeName}\x02{fieldName}";
    }

    private static string? EncodeMethod(int off, IMethod? method, ModuleDefMD module)
    {
        if (method is null) return null;

        string? typeName, methodName;
        MethodSig? sig;

        var md = method.ResolveMethodDef();
        if (md is not null)
        {
            typeName   = TypeName(md.DeclaringType, module);
            methodName = md.Name.String;
            sig        = md.MethodSig;
        }
        else if (method is MemberRef mr)
        {
            typeName   = TypeName(mr.DeclaringType as ITypeDefOrRef, module);
            methodName = mr.Name.String;
            sig        = mr.MethodSig;
        }
        else return null;

        if (typeName is null || sig is null) return null;

        var sb = new System.Text.StringBuilder();
        sb.Append(off).Append('\x01').Append('M').Append('\x01');
        sb.Append(typeName).Append('\x02').Append(methodName).Append('\x02');

        var retName = SigTypeName(sig.RetType);
        if (retName is null) return null;
        sb.Append(retName);

        foreach (var p in sig.Params)
        {
            var pn = SigTypeName(p);
            if (pn is null) return null;
            sb.Append('\x02').Append(pn);
        }

        return sb.ToString();
    }

    private static string? EncodeTok(int off, object operand, ModuleDefMD module) => operand switch
    {
        ITypeDefOrRef tdr => EncodeType(off, tdr, module),
        IMethod m         => EncodeMethod(off, m, module),
        IField f          => EncodeField(off, f, module),
        _                 => null,
    };

    // ------------------------------------------------------------------
    // Method signature  [0]=retType ("" = void), [1..]=param types
    // ------------------------------------------------------------------

    private static string[]? BuildSignature(MethodDef method, ModuleDefMD module)
    {
        var sig = method.MethodSig;
        if (sig is null) return null;

        var list = new List<string>();

        var retName = SigTypeName(sig.RetType);
        if (retName is null) return null;
        list.Add(retName == "System.Void" ? "" : retName);

        // Instance methods: DynamicMethod param[0] is 'this'
        if (!method.IsStatic)
        {
            var thisName = TypeName(method.DeclaringType, module);
            if (thisName is null) return null;
            list.Add(thisName);
        }

        foreach (var p in sig.Params)
        {
            var pn = SigTypeName(p);
            if (pn is null) return null;
            list.Add(pn);
        }

        return [.. list];
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string? TypeName(ITypeDefOrRef? tdr, ModuleDefMD module)
    {
        if (tdr is null) return null;
        if (tdr is TypeRef tr)
        {
            string ns = tr.Namespace.String;
            string n  = tr.Name.String;
            return ns.Length > 0 ? $"{ns}.{n}" : n;
        }
        if (tdr is TypeDef td)
        {
            // Same-module type → use current (possibly renamed) name
            // VmRuntime.Lkt searches all loaded assemblies, so the current name is correct
            string ns = td.Namespace.String;
            string n  = td.Name.String;
            return ns.Length > 0 ? $"{ns}.{n}" : n;
        }
        if (tdr is TypeSpec ts && ts.TypeSig is not null)
            return SigTypeName(ts.TypeSig);
        return null;
    }

    private static string? SigTypeName(TypeSig? sig)
    {
        if (sig is null) return null;
        return sig.ElementType switch
        {
            ElementType.Void    => "System.Void",
            ElementType.Boolean => "System.Boolean",
            ElementType.Char    => "System.Char",
            ElementType.I1      => "System.SByte",
            ElementType.U1      => "System.Byte",
            ElementType.I2      => "System.Int16",
            ElementType.U2      => "System.UInt16",
            ElementType.I4      => "System.Int32",
            ElementType.U4      => "System.UInt32",
            ElementType.I8      => "System.Int64",
            ElementType.U8      => "System.UInt64",
            ElementType.R4      => "System.Single",
            ElementType.R8      => "System.Double",
            ElementType.String  => "System.String",
            ElementType.Object  => "System.Object",
            ElementType.I       => "System.IntPtr",
            ElementType.U       => "System.UIntPtr",
            ElementType.Class   => TypeName(sig.TryGetTypeDefOrRef(), null!),
            ElementType.ValueType => TypeName(sig.TryGetTypeDefOrRef(), null!),
            _                   => null,
        };
    }

    private static void Wi32(byte[] b, int p, int v)
    {
        b[p] = (byte)v; b[p+1] = (byte)(v>>8); b[p+2] = (byte)(v>>16); b[p+3] = (byte)(v>>24);
    }

    private static void Wi64(byte[] b, int p, long v)
    {
        for (int i = 0; i < 8; i++) b[p+i] = (byte)(v >> (i*8));
    }
}
