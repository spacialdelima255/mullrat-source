using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Mullvad.Protector.Core.Models;

namespace Mullvad.Protector.Core.Constants;

/// <summary>
/// Replaces integer constants with arithmetic expressions that evaluate to the same value.
/// e.g., 42  → (87 ^ 65) ^ 20  (or any equivalent XOR/add/sub combination)
/// Makes static analysis and decompiler output harder to read.
/// </summary>
public sealed class ConstantProtector
{
    private readonly ProtectionContext _ctx;
    private readonly Random _rng = new(Environment.TickCount + 1);

    public ConstantProtector(ProtectionContext ctx) => _ctx = ctx;

    public void Apply()
    {
        if (_ctx.Mode == ProtectionMode.Standard) return;

        foreach (var method in _ctx.ConstantMethods)
        {
            if (_ctx.SkipStringProtect.Contains(method)) continue;
            try
            {
                TransformMethod(method);
            }
            catch { /* never break a method */ }
        }
    }

    private void TransformMethod(MethodDef method)
    {
        var body = method.Body;
        if (body is null) return;

        var instructions = body.Instructions;

        for (int i = instructions.Count - 1; i >= 0; i--)
        {
            var instr = instructions[i];
            if (!TryGetInt32Value(instr, out int value)) continue;

            // Only obfuscate non-trivial constants and skip 0/1 used by booleans
            if (value is 0 or 1 or -1) continue;

            // Replace with XOR pair: (value ^ mask) ^ mask == value
            int mask = _rng.Next(0x1000, 0x7FFFFFFF);
            int part = value ^ mask;

            // ldc.i4 part; ldc.i4 mask; xor
            instructions[i] = new Instruction(OpCodes.Ldc_I4, part);
            instructions.Insert(i + 1, new Instruction(OpCodes.Ldc_I4, mask));
            instructions.Insert(i + 2, new Instruction(OpCodes.Xor));
        }

        body.OptimizeBranches();
        body.OptimizeMacros();
    }

    private static bool TryGetInt32Value(Instruction instr, out int value)
    {
        value = 0;
        switch (instr.OpCode.Code)
        {
            case Code.Ldc_I4:
                value = (int)instr.Operand;
                return true;
            case Code.Ldc_I4_S:
                value = (sbyte)instr.Operand;
                return true;
            case Code.Ldc_I4_0: value = 0;  return true;
            case Code.Ldc_I4_1: value = 1;  return true;
            case Code.Ldc_I4_2: value = 2;  return true;
            case Code.Ldc_I4_3: value = 3;  return true;
            case Code.Ldc_I4_4: value = 4;  return true;
            case Code.Ldc_I4_5: value = 5;  return true;
            case Code.Ldc_I4_6: value = 6;  return true;
            case Code.Ldc_I4_7: value = 7;  return true;
            case Code.Ldc_I4_8: value = 8;  return true;
            case Code.Ldc_I4_M1: value = -1; return true;
        }
        return false;
    }
}
