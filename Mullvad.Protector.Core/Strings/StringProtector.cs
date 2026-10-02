using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Mullvad.Protector.Core.Models;

namespace Mullvad.Protector.Core.Strings;

/// <summary>
/// Replaces ldstr instructions with calls to an injected runtime decoder.
/// Decoder class is injected as a private static class using confusable names.
/// </summary>
public sealed class StringProtector
{
    private readonly ProtectionContext _ctx;

    private static readonly char[] ConfChars = { 'I', 'l', 'І', 'ӏ' };

    public StringProtector(ProtectionContext ctx) => _ctx = ctx;

    public void Apply()
    {
        if (_ctx.StringCallSites.Count == 0) return;

        var module = _ctx.Module;

        // Build and inject the decoder class
        var (decoderType, decodeMethod) = InjectDecoder(module);
        _ctx.DecoderType = decoderType;
        _ctx.DecoderMethod = decodeMethod;

        // Replace every ldstr call site
        foreach (var site in _ctx.StringCallSites)
        {
            var body = site.ContainingMethod.Body;
            var instr = site.LdstrInstruction;
            var entry = site.Entry;

            // Replace: ldstr "original"
            // With:    ldstr "<base64_enc>"; ldc.i4 <key>; call D
            instr.OpCode = OpCodes.Ldstr;
            instr.Operand = Convert.ToBase64String(entry.EncryptedBytes);

            // Insert key push and call after ldstr
            int idx = body.Instructions.IndexOf(instr);
            body.Instructions.Insert(idx + 1, new Instruction(OpCodes.Ldc_I4, entry.Key));
            body.Instructions.Insert(idx + 2, new Instruction(OpCodes.Call, decodeMethod));
        }

        // Re-optimize modified methods
        var touched = _ctx.StringCallSites
            .Select(s => s.ContainingMethod)
            .Distinct()
            .ToList();

        foreach (var m in touched)
        {
            m.Body.OptimizeBranches();
            m.Body.OptimizeMacros();
        }
    }

    private (TypeDef, MethodDef) InjectDecoder(ModuleDefMD module)
    {
        // Confusable class name — looks like blank I characters in dnSpy
        string className = ConfName(3);
        string methodName = ConfName(1);

        var objectRef = module.CorLibTypes.Object.ToTypeDefOrRef();

        var decoderType = new TypeDefUser(string.Empty, className, objectRef);
        decoderType.Attributes =
            TypeAttributes.NotPublic |
            TypeAttributes.Sealed |
            TypeAttributes.Abstract |        // static class
            TypeAttributes.BeforeFieldInit;
        module.Types.Add(decoderType);

        // Import references we need
        var stringType   = module.CorLibTypes.String;
        var int32Type    = module.CorLibTypes.Int32;
        var byteType     = module.CorLibTypes.Byte;
        var byteArraySig = new SZArraySig(byteType);
        var voidType     = module.CorLibTypes.Void;
        var boolType     = module.CorLibTypes.Boolean;

        var corLibRef = module.CorLibTypes.AssemblyRef;

        // System.Convert.FromBase64String(string) -> byte[]
        var convertTypeRef = new TypeRefUser(module, "System", "Convert", corLibRef);
        var fromBase64 = new MemberRefUser(module, "FromBase64String",
            MethodSig.CreateStatic(byteArraySig, stringType),
            convertTypeRef);

        // System.Text.Encoding.get_UTF8() -> Encoding
        var encodingTypeRef = new TypeRefUser(module, "System.Text", "Encoding", corLibRef);
        var encodingClassSig = new ClassSig(encodingTypeRef);
        var getUtf8 = new MemberRefUser(module, "get_UTF8",
            MethodSig.CreateStatic(encodingClassSig),
            encodingTypeRef);

        // Encoding.GetString(byte[]) -> string
        var getStringMethod = new MemberRefUser(module, "GetString",
            MethodSig.CreateInstance(stringType, byteArraySig),
            encodingTypeRef);

        // Build D(string b64, int key) -> string
        var decodeMethod = new MethodDefUser(methodName,
            MethodSig.CreateStatic(stringType, stringType, int32Type),
            MethodImplAttributes.IL | MethodImplAttributes.Managed,
            MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig);

        decoderType.Methods.Add(decodeMethod);

        var body = new CilBody();
        decodeMethod.Body = body;

        // Locals: byte[] bytes (0), int i (1)
        var locBytes = new Local(byteArraySig);
        var locI     = new Local(int32Type);
        body.Variables.Add(locBytes);
        body.Variables.Add(locI);
        body.InitLocals = true;

        var instrList = body.Instructions;

        // bytes = Convert.FromBase64String(b64)
        instrList.Add(OpCodes.Ldarg_0.ToInstruction());
        instrList.Add(new Instruction(OpCodes.Call, fromBase64));
        instrList.Add(new Instruction(OpCodes.Stloc, locBytes));

        // i = 0
        instrList.Add(OpCodes.Ldc_I4_0.ToInstruction());
        instrList.Add(new Instruction(OpCodes.Stloc, locI));

        // LOOP_CHECK: if i >= bytes.Length goto END
        var loopCheck = OpCodes.Nop.ToInstruction();
        var loopEnd   = OpCodes.Nop.ToInstruction();
        instrList.Add(loopCheck);
        instrList.Add(new Instruction(OpCodes.Ldloc, locI));
        instrList.Add(new Instruction(OpCodes.Ldloc, locBytes));
        instrList.Add(OpCodes.Ldlen.ToInstruction());
        instrList.Add(OpCodes.Conv_I4.ToInstruction());
        instrList.Add(new Instruction(OpCodes.Bge, loopEnd));

        // bytes[i] = (byte)(bytes[i] ^ (key ^ i))
        instrList.Add(new Instruction(OpCodes.Ldloc, locBytes));   // bytes (stelem target)
        instrList.Add(new Instruction(OpCodes.Ldloc, locI));        // i    (stelem index)
        instrList.Add(new Instruction(OpCodes.Ldloc, locBytes));    // bytes
        instrList.Add(new Instruction(OpCodes.Ldloc, locI));        // i
        instrList.Add(OpCodes.Ldelem_U1.ToInstruction());            // bytes[i]
        instrList.Add(OpCodes.Ldarg_1.ToInstruction());              // key
        instrList.Add(new Instruction(OpCodes.Ldloc, locI));        // i
        instrList.Add(OpCodes.Xor.ToInstruction());                  // key ^ i
        instrList.Add(OpCodes.Xor.ToInstruction());                  // bytes[i] ^ (key ^ i)
        instrList.Add(OpCodes.Stelem_I1.ToInstruction());            // bytes[i] = result

        // i++
        instrList.Add(new Instruction(OpCodes.Ldloc, locI));
        instrList.Add(OpCodes.Ldc_I4_1.ToInstruction());
        instrList.Add(OpCodes.Add.ToInstruction());
        instrList.Add(new Instruction(OpCodes.Stloc, locI));
        instrList.Add(new Instruction(OpCodes.Br, loopCheck));

        // END: return Encoding.UTF8.GetString(bytes)
        instrList.Add(loopEnd);
        instrList.Add(new Instruction(OpCodes.Call, getUtf8));
        instrList.Add(new Instruction(OpCodes.Ldloc, locBytes));
        instrList.Add(new Instruction(OpCodes.Callvirt, getStringMethod));
        instrList.Add(OpCodes.Ret.ToInstruction());

        body.OptimizeBranches();

        // Mark decoder itself as skip-protect
        _ctx.SkipStringProtect.Add(decodeMethod);

        return (decoderType, decodeMethod);
    }

    private static int _nameCounter;
    private static string ConfName(int length)
    {
        var sb = new System.Text.StringBuilder();
        int n = _nameCounter++;
        for (int i = 0; i < length; i++)
        {
            sb.Append(ConfChars[n % ConfChars.Length]);
            n = (n + 1) / ConfChars.Length + i;
        }
        return sb.ToString();
    }
}
