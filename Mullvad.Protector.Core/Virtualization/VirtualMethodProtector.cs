using System.Security.Cryptography;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Mullvad.Protector.Core.Models;

namespace Mullvad.Protector.Core.Virtualization;

/// <summary>
/// Virtualises eligible method bodies so that dnSpy / IDA only see:
///   return (T) VR.R(id, new object[] { arg0, arg1, ... });
///
/// The original IL is XOR-encrypted and stored in the injected VmRuntime
/// class; it is reconstructed into a DynamicMethod on first call.
/// An HMAC over the entire encrypted table and per-entry SHA-256 hashes
/// mean any in-memory or on-disk patch is detected before execution.
///
/// Run this pass AFTER renaming + string protection, BEFORE control-flow
/// transformation.
/// </summary>
public sealed class VirtualMethodProtector
{
    private readonly ProtectionContext _ctx;

    public VirtualMethodProtector(ProtectionContext ctx) => _ctx = ctx;

    public void Apply()
    {
        var module = _ctx.Module;

        // Collect candidates
        var candidates = new List<MethodDef>();
        foreach (var type in module.GetTypes())
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;
                if (_ctx.SkipStringProtect.Contains(method)) continue;
                candidates.Add(method);
            }
        }

        // Extract IL for each candidate
        var extracted = new List<(MethodDef Method, ExtractedMethod Data)>();
        foreach (var method in candidates)
        {
            var data = IlExtractor.TryExtract(method, module);
            if (data is not null)
                extracted.Add((method, data));
        }

        if (extracted.Count == 0)
        {
            _ctx.Log?.Invoke("[*] Virtualization: no eligible methods");
            return;
        }

        _ctx.Log?.Invoke($"[+] Virtualization: {extracted.Count} methods eligible");

        // ── Generate per-run cryptographic material ──────────────────────────

        // 16-byte XOR key — randomised on every protection run.
        byte[] vmValidationKey = RandomNumberGenerator.GetBytes(16);

        // Pre-encrypt all IL
        var encryptedIl = new List<byte[]>(extracted.Count);
        for (int i = 0; i < extracted.Count; i++)
            encryptedIl.Add(XorEncrypt(extracted[i].Data.IlBytes, i, vmValidationKey));

        // ── Inject VmRuntime into the target module ──────────────────────────

        var (vrType, initMethod, rMethod) = InjectRuntime(module);
        if (vrType is null || initMethod is null || rMethod is null)
        {
            _ctx.Log?.Invoke("[!] Virtualization: runtime inject failed");
            return;
        }

        // ── Inject module .cctor that calls VR.Init(...) ─────────────────────

        InjectModuleInit(module, initMethod, extracted, encryptedIl, vmValidationKey);

        // ── Debug: dump token tables to tk_debug.txt for corruption diagnosis ──
        try
        {
            string debugDir = System.IO.Path.GetDirectoryName(_ctx.OutputPath) ?? ".";
            string debugPath = System.IO.Path.Combine(debugDir, "tk_debug.txt");
            var sb2 = new System.Text.StringBuilder();
            for (int i = 0; i < extracted.Count; i++)
            {
                sb2.AppendLine($"id={i} method={extracted[i].Method.FullName}");
                var tt = extracted[i].Data.TokenTable;
                for (int j = 0; j < tt.Length; j++)
                {
                    var s = tt[j];
                    var hex = new System.Text.StringBuilder();
                    for (int k = 0; k < s.Length && k < 16; k++)
                    {
                        if (k > 0) hex.Append(',');
                        hex.Append(((int)s[k]).ToString("X4"));
                    }
                    sb2.AppendLine($"  te[{j}] len={s.Length} chars={hex}");
                }
            }
            System.IO.File.WriteAllText(debugPath, sb2.ToString(), System.Text.Encoding.UTF8);
        }
        catch { }

        // ── Replace each eligible method body with a VR.R stub ───────────────

        for (int i = 0; i < extracted.Count; i++)
            ReplaceWithStub(extracted[i].Method, i, rMethod, module);

        _ctx.Log?.Invoke($"[+] Virtualization: {extracted.Count} methods protected");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Runtime injection
    // ──────────────────────────────────────────────────────────────────────────

    private (TypeDef?, MethodDef?, MethodDef?) InjectRuntime(ModuleDefMD target)
    {
        try
        {
            string protectorPath = typeof(VmRuntime).Assembly.Location;
            using var src = ModuleDefMD.Load(protectorPath);

            var srcType = src.GetTypes()
                .FirstOrDefault(t => t.FullName == typeof(VmRuntime).FullName);
            if (srcType is null) return default;

            var importer = new Importer(target, ImporterOptions.TryToUseDefs);
            var newType  = CloneTypeDef(srcType, target, importer);

            // Grab references to Init/R BEFORE any renaming — same MethodDef objects
            MethodDef? initMd = newType.FindMethod("Init");
            MethodDef? rMd    = newType.FindMethod("R");

            RenameVrType(newType, target);

            newType.Namespace = string.Empty;
            target.Types.Add(newType);

            return (newType, initMd, rMd);
        }
        catch (Exception ex)
        {
            _ctx.Log?.Invoke($"[!] InjectRuntime: {ex.Message}");
            return default;
        }
    }

    private static void RenameVrType(TypeDef type, ModuleDefMD module)
    {
        var used = module.GetTypes().Select(t => t.Name.String)
                         .ToHashSet(StringComparer.Ordinal);
        int counter = 0;
        string PickNew()
        {
            string n; do { n = ConfName(counter++); } while (!used.Add(n)); return n;
        }

        type.Name = PickNew();
        foreach (var m in type.Methods)
            if (!m.IsConstructor && !m.IsStaticConstructor)
                m.Name = PickNew();
        foreach (var f in type.Fields)
            f.Name = PickNew();
    }

    private static string ConfName(int n)
    {
        char[] chars = { 'I', 'l', 'І', 'ӏ' };
        var hash = SHA256.HashData(
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(
                System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref n, 1)));
        var sb = new System.Text.StringBuilder(9);
        sb.Append(chars[0]);
        for (int i = 0; i < 8; i++) sb.Append(chars[hash[i] % chars.Length]);
        return sb.ToString();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // TypeDef / CilBody deep clone — two-phase so intra-type references resolve
    // to the cloned members, not to the protector assembly.
    // ──────────────────────────────────────────────────────────────────────────

    private static TypeDef CloneTypeDef(TypeDef src, ModuleDefMD target, Importer importer)
    {
        var dst = new TypeDefUser(src.Namespace.String, src.Name.String,
            src.BaseType is not null ? importer.Import(src.BaseType) : null);
        dst.Attributes = src.Attributes;

        // Phase 1 — create field and method shells so intra-type references
        // can be resolved to the new type during body cloning.
        var fieldMap  = new Dictionary<FieldDef, FieldDef>(ReferenceEqualityComparer.Instance);
        var methodMap = new Dictionary<MethodDef, MethodDef>(ReferenceEqualityComparer.Instance);

        foreach (var sf in src.Fields)
        {
            var df = new FieldDefUser(sf.Name.String,
                (FieldSig)importer.Import(sf.FieldSig), sf.Attributes);
            dst.Fields.Add(df);
            fieldMap[sf] = df;
        }

        foreach (var sm in src.Methods)
        {
            var dm = new MethodDefUser(sm.Name.String,
                (MethodSig)importer.Import(sm.MethodSig),
                sm.ImplAttributes, sm.Attributes);
            dst.Methods.Add(dm);
            methodMap[sm] = dm;
        }

        // Phase 2 — clone bodies with self-reference awareness
        for (int i = 0; i < src.Methods.Count; i++)
        {
            var sm = src.Methods[i];
            var dm = dst.Methods[i];

            if (sm.HasBody)
                dm.Body = CloneBody(sm.Body, importer, fieldMap, methodMap);

            // Copy P/Invoke mapping (e.g. native interop declarations)
            if (sm.HasImplMap)
            {
                dm.ImplMap = new ImplMapUser(
                    new ModuleRefUser(target, sm.ImplMap.Module.Name.String),
                    sm.ImplMap.Name.String,
                    sm.ImplMap.Attributes);
            }
        }

        return dst;
    }

    private static CilBody CloneBody(CilBody src, Importer importer,
        Dictionary<FieldDef, FieldDef>?   fieldMap  = null,
        Dictionary<MethodDef, MethodDef>? methodMap = null)
    {
        var dst = new CilBody { InitLocals = src.InitLocals, MaxStack = src.MaxStack };

        foreach (var loc in src.Variables)
            dst.Variables.Add(new Local((TypeSig)importer.Import(loc.Type)));

        // Map old → new instruction objects
        var map = new Dictionary<Instruction, Instruction>(ReferenceEqualityComparer.Instance);
        foreach (var si in src.Instructions)
        {
            var di = new Instruction(si.OpCode);
            map[si] = di;
            dst.Instructions.Add(di);
        }

        // Patch operands — self-type members use the pre-built maps to avoid
        // external TypeRef/MemberRef back to the protector assembly.
        for (int i = 0; i < src.Instructions.Count; i++)
        {
            var si = src.Instructions[i];
            var di = dst.Instructions[i];

            di.Operand = si.Operand switch
            {
                null => null,
                Instruction tgt  => map[tgt],
                IList<Instruction> list => list.Select(t => map[t]).ToArray(),
                FieldDef fd when fieldMap  != null && fieldMap.ContainsKey(fd)  => fieldMap[fd],
                MethodDef md when methodMap != null && methodMap.ContainsKey(md) => methodMap[md],
                ITypeDefOrRef tdr => importer.Import(tdr),
                IMethod m         => importer.Import(m),
                IField f          => importer.Import(f),
                string s          => s,
                _                 => si.Operand,  // int, float, sbyte, …
            };
        }

        foreach (var eh in src.ExceptionHandlers)
        {
            dst.ExceptionHandlers.Add(new ExceptionHandler(eh.HandlerType)
            {
                TryStart     = eh.TryStart     is not null ? map[eh.TryStart]     : null,
                TryEnd       = eh.TryEnd       is not null ? map[eh.TryEnd]       : null,
                HandlerStart = eh.HandlerStart is not null ? map[eh.HandlerStart] : null,
                HandlerEnd   = eh.HandlerEnd   is not null ? map[eh.HandlerEnd]   : null,
                FilterStart  = eh.FilterStart  is not null ? map[eh.FilterStart]  : null,
                CatchType    = eh.CatchType    is not null
                    ? (ITypeDefOrRef)importer.Import(eh.CatchType) : null,
            });
        }

        return dst;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Module .cctor — VR.Init(c, ms, tk, sg, eh, vk, ih)
    // ──────────────────────────────────────────────────────────────────────────

    private static void InjectModuleInit(
        ModuleDefMD module,
        MethodDef initMethod,
        List<(MethodDef Method, ExtractedMethod Data)> entries,
        List<byte[]> encryptedIl,
        byte[] vmValidationKey)
    {
        var cctor = module.GlobalType.FindOrCreateStaticConstructor();
        var body  = cctor.Body ?? new CilBody { InitLocals = true };
        cctor.Body = body;
        body.InitLocals = true;

        var instrs = body.Instructions;
        if (instrs.Count > 0 && instrs[^1].OpCode == OpCodes.Ret)
            instrs.RemoveAt(instrs.Count - 1);

        int n = entries.Count;

        // TypeRefs for newarr
        var byteTypeRef  = module.CorLibTypes.Byte.ToTypeDefOrRef();
        var byteArrRef   = new TypeSpecUser(new SZArraySig(module.CorLibTypes.Byte));
        var int32TypeRef = module.CorLibTypes.Int32.ToTypeDefOrRef();
        var strTypeRef   = module.CorLibTypes.String.ToTypeDefOrRef();
        var strArrRef    = new TypeSpecUser(new SZArraySig(module.CorLibTypes.String));

        var emit = new List<Instruction>();

        Instruction LdcI(int v) => v switch
        {
            -1 => new Instruction(OpCodes.Ldc_I4_M1),
            0  => new Instruction(OpCodes.Ldc_I4_0),
            1  => new Instruction(OpCodes.Ldc_I4_1),
            2  => new Instruction(OpCodes.Ldc_I4_2),
            3  => new Instruction(OpCodes.Ldc_I4_3),
            4  => new Instruction(OpCodes.Ldc_I4_4),
            5  => new Instruction(OpCodes.Ldc_I4_5),
            6  => new Instruction(OpCodes.Ldc_I4_6),
            7  => new Instruction(OpCodes.Ldc_I4_7),
            8  => new Instruction(OpCodes.Ldc_I4_8),
            _  when v >= -128 && v <= 127 => new Instruction(OpCodes.Ldc_I4_S, (sbyte)v),
            _  => new Instruction(OpCodes.Ldc_I4, v),
        };

        // Helper: build a byte[] local from a fixed byte array
        void EmitByteArray(Local loc, byte[] data)
        {
            emit.Add(LdcI(data.Length));
            emit.Add(new Instruction(OpCodes.Newarr, byteTypeRef));
            emit.Add(new Instruction(OpCodes.Stloc, loc));
            for (int j = 0; j < data.Length; j++)
            {
                emit.Add(new Instruction(OpCodes.Ldloc, loc));
                emit.Add(LdcI(j));
                emit.Add(LdcI(data[j]));
                emit.Add(new Instruction(OpCodes.Stelem_I1));
            }
        }

        // Helper: build a byte[][] local from a list of byte arrays
        void EmitByteJaggedArray(Local loc, IReadOnlyList<byte[]> data)
        {
            emit.Add(LdcI(data.Count));
            emit.Add(new Instruction(OpCodes.Newarr, byteArrRef));
            emit.Add(new Instruction(OpCodes.Stloc, loc));
            for (int i = 0; i < data.Count; i++)
            {
                byte[] row = data[i];
                emit.Add(new Instruction(OpCodes.Ldloc, loc));
                emit.Add(LdcI(i));
                emit.Add(LdcI(row.Length));
                emit.Add(new Instruction(OpCodes.Newarr, byteTypeRef));
                for (int j = 0; j < row.Length; j++)
                {
                    emit.Add(new Instruction(OpCodes.Dup));
                    emit.Add(LdcI(j));
                    emit.Add(LdcI(row[j]));
                    emit.Add(new Instruction(OpCodes.Stelem_I1));
                }
                emit.Add(new Instruction(OpCodes.Stelem_Ref));
            }
        }

        // ── byte[][] c (encrypted IL) ────────────────────────────────────────
        var locC = new Local(new SZArraySig(new SZArraySig(module.CorLibTypes.Byte)));
        body.Variables.Add(locC);
        EmitByteJaggedArray(locC, encryptedIl);

        // ── int[] ms (maxStack) ──────────────────────────────────────────────
        var locMs = new Local(new SZArraySig(module.CorLibTypes.Int32));
        body.Variables.Add(locMs);

        emit.Add(LdcI(n));
        emit.Add(new Instruction(OpCodes.Newarr, int32TypeRef));
        emit.Add(new Instruction(OpCodes.Stloc, locMs));
        for (int i = 0; i < n; i++)
        {
            emit.Add(new Instruction(OpCodes.Ldloc, locMs));
            emit.Add(LdcI(i));
            emit.Add(LdcI(entries[i].Data.MaxStack));
            emit.Add(new Instruction(OpCodes.Stelem_I4));
        }

        // ── string[][] tk (token tables) ────────────────────────────────────
        var locTk = new Local(new SZArraySig(new SZArraySig(module.CorLibTypes.String)));
        body.Variables.Add(locTk);
        emit.AddRange(BuildJaggedStringArray(n, entries.Select(e => e.Data.TokenTable).ToArray(),
            locTk, strArrRef, strTypeRef, LdcI));

        // ── string[][] sg (signatures) ───────────────────────────────────────
        var locSg = new Local(new SZArraySig(new SZArraySig(module.CorLibTypes.String)));
        body.Variables.Add(locSg);
        emit.AddRange(BuildJaggedStringArray(n, entries.Select(e => e.Data.Signature).ToArray(),
            locSg, strArrRef, strTypeRef, LdcI));

        // ── byte[] vk (XOR key) ──────────────────────────────────────────────
        var locVk = new Local(new SZArraySig(module.CorLibTypes.Byte));
        body.Variables.Add(locVk);
        EmitByteArray(locVk, vmValidationKey);

        // ── call VR.Init(c, ms, tk, sg, vk) ─────────────────────────────────
        emit.Add(new Instruction(OpCodes.Ldloc, locC));
        emit.Add(new Instruction(OpCodes.Ldloc, locMs));
        emit.Add(new Instruction(OpCodes.Ldloc, locTk));
        emit.Add(new Instruction(OpCodes.Ldloc, locSg));
        emit.Add(new Instruction(OpCodes.Ldloc, locVk));
        emit.Add(new Instruction(OpCodes.Call, initMethod));
        emit.Add(new Instruction(OpCodes.Ret));

        for (int i = 0; i < emit.Count; i++)
            instrs.Insert(i, emit[i]);

        body.MaxStack = (ushort)Math.Max((int)body.MaxStack, 8);
        body.UpdateInstructionOffsets();
    }

    private static List<Instruction> BuildJaggedStringArray(
        int n, string[][] data, Local loc,
        ITypeDefOrRef strArrRef, ITypeDefOrRef strTypeRef,
        Func<int, Instruction> ldc)
    {
        var list = new List<Instruction>();

        list.Add(ldc(n));
        list.Add(new Instruction(OpCodes.Newarr, strArrRef));
        list.Add(new Instruction(OpCodes.Stloc, loc));

        for (int i = 0; i < n; i++)
        {
            list.Add(new Instruction(OpCodes.Ldloc, loc));
            list.Add(ldc(i));

            string[] row = data[i];
            list.Add(ldc(row.Length));
            list.Add(new Instruction(OpCodes.Newarr, strTypeRef));
            for (int j = 0; j < row.Length; j++)
            {
                list.Add(new Instruction(OpCodes.Dup));
                list.Add(ldc(j));
                list.Add(new Instruction(OpCodes.Ldstr, row[j]));
                list.Add(new Instruction(OpCodes.Stelem_Ref));
            }
            list.Add(new Instruction(OpCodes.Stelem_Ref));
        }

        return list;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Stub replacement — replace method body with VR.R(id, new object[]{...})
    // ──────────────────────────────────────────────────────────────────────────

    private static void ReplaceWithStub(
        MethodDef method, int id, MethodDef vrR, ModuleDefMD module)
    {
        var body = method.Body ?? new CilBody();
        method.Body = body;
        body.Instructions.Clear();
        body.Variables.Clear();
        body.ExceptionHandlers.Clear();

        var instrs = body.Instructions;
        var sig    = method.MethodSig;
        bool isVoid = sig.RetType.ElementType == ElementType.Void;

        Instruction LdcI(int v) => v switch
        {
            0 => new Instruction(OpCodes.Ldc_I4_0),
            1 => new Instruction(OpCodes.Ldc_I4_1),
            2 => new Instruction(OpCodes.Ldc_I4_2),
            3 => new Instruction(OpCodes.Ldc_I4_3),
            _ when v >= -128 && v <= 127 => new Instruction(OpCodes.Ldc_I4_S, (sbyte)v),
            _ => new Instruction(OpCodes.Ldc_I4, v),
        };

        instrs.Add(LdcI(id));

        int argCount = (method.IsStatic ? 0 : 1) + sig.Params.Count;
        instrs.Add(LdcI(argCount));
        instrs.Add(new Instruction(OpCodes.Newarr, module.CorLibTypes.Object.ToTypeDefOrRef()));

        for (int i = 0; i < argCount; i++)
        {
            instrs.Add(new Instruction(OpCodes.Dup));
            instrs.Add(LdcI(i));
            instrs.Add(i switch
            {
                0 => new Instruction(OpCodes.Ldarg_0),
                1 => new Instruction(OpCodes.Ldarg_1),
                2 => new Instruction(OpCodes.Ldarg_2),
                3 => new Instruction(OpCodes.Ldarg_3),
                _ => new Instruction(OpCodes.Ldarg, method.Parameters[i]),
            });

            TypeSig argSig = method.IsStatic
                ? sig.Params[i]
                : (i == 0 ? method.DeclaringType.ToTypeSig() : sig.Params[i - 1]);

            if (NeedsBox(argSig))
            {
                var boxType = argSig.TryGetTypeDefOrRef()
                    ?? module.CorLibTypes.Object.ToTypeDefOrRef();
                instrs.Add(new Instruction(OpCodes.Box, boxType));
            }

            instrs.Add(new Instruction(OpCodes.Stelem_Ref));
        }

        instrs.Add(new Instruction(OpCodes.Call, vrR));

        if (isVoid)
        {
            instrs.Add(new Instruction(OpCodes.Pop));
        }
        else
        {
            var retTdr = sig.RetType.TryGetTypeDefOrRef()
                ?? module.CorLibTypes.Object.ToTypeDefOrRef();
            instrs.Add(NeedsBox(sig.RetType)
                ? new Instruction(OpCodes.Unbox_Any, retTdr)
                : new Instruction(OpCodes.Castclass, retTdr));
        }

        instrs.Add(new Instruction(OpCodes.Ret));

        body.MaxStack   = 8;
        body.InitLocals = false;
        body.UpdateInstructionOffsets();
    }

    private static bool NeedsBox(TypeSig sig)
    {
        var et = sig.ElementType;
        return et is (>= ElementType.Boolean and <= ElementType.R8)
                  or ElementType.I or ElementType.U
                  or ElementType.ValueType;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // XOR encryption — formula must match VmRuntime.Xor exactly.
    // key schedule: plain[i] ^ vk[i % 16] ^ ((id ^ i) & 0xFF)
    // ──────────────────────────────────────────────────────────────────────────

    private static byte[] XorEncrypt(byte[] data, int id, byte[] vk)
    {
        var r = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
            r[i] = (byte)(data[i] ^ vk[i % 16] ^ ((id ^ i) & 0xFF));
        return r;
    }
}
