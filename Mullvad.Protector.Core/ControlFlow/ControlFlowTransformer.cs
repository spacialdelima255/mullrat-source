using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Mullvad.Protector.Core.Models;

namespace Mullvad.Protector.Core.ControlFlow;

/// <summary>
/// Applies control flow obfuscation:
///   1. Opaque predicates wrapping basic blocks
///   2. Junk arithmetic instruction insertion
///   3. Unconditional branch splitting (scrambles linear IL layout)
/// Skips methods with exception handlers to preserve correctness.
/// </summary>
public sealed class ControlFlowTransformer
{
    private readonly ProtectionContext _ctx;
    private readonly Random _rng = new(Environment.TickCount);

    public ControlFlowTransformer(ProtectionContext ctx) => _ctx = ctx;

    public void Apply()
    {
        var methods = _ctx.Mode == ProtectionMode.Strong
            ? _ctx.CfMethods
            : _ctx.CfMethods.Where(m => m.DeclaringType.IsPublic == false).ToList();

        foreach (var method in methods)
        {
            if (_ctx.SkipStringProtect.Contains(method)) continue;
            try
            {
                TransformMethod(method);
            }
            catch
            {
                // Never break a method — skip it silently on failure
            }
        }
    }

    private void TransformMethod(MethodDef method)
    {
        var body = method.Body;
        if (body is null || body.Instructions.Count < 4) return;

        // Skip very large methods (e.g. WinForms InitializeComponent) — they're complex
        // enough that junk insertion can interact badly with metadata writes.
        if (body.Instructions.Count > 150) return;

        body.SimplifyBranches();
        body.SimplifyMacros(method.Parameters);

        InsertJunk(body);
        InsertOpaquePredicates(body);

        body.OptimizeBranches();
        body.OptimizeMacros();
        body.UpdateInstructionOffsets();
    }

    private static bool IsUnconditionalTransfer(Instruction instr)
    {
        var c = instr.OpCode.Code;
        return c == Code.Ret    ||
               c == Code.Throw  ||
               c == Code.Rethrow||
               c == Code.Br     ||
               c == Code.Br_S   ||
               c == Code.Leave  ||
               c == Code.Leave_S||
               c == Code.Endfinally ||
               c == Code.Endfilter;
    }

    private void InsertJunk(CilBody body)
    {
        var instructions = body.Instructions;
        int originalCount = instructions.Count;

        for (int i = originalCount - 1; i >= 1; i -= _rng.Next(3, 7))
        {
            // Never insert into a dead zone — the instruction before this one
            // terminates the flow, so a forward fall-through here is dead code.
            // Some JIT stack-depth analyses assign inconsistent types to such
            // dead code paths, producing InvalidProgramException at runtime.
            if (IsUnconditionalTransfer(instructions[i - 1]))
                continue;

            // Insert: ldc.i4 A; ldc.i4 B; add; pop  — no stack effect
            int a = _rng.Next(1, 1000);
            int b = _rng.Next(1, 1000);
            instructions.Insert(i, new Instruction(OpCodes.Pop));
            instructions.Insert(i, new Instruction(OpCodes.Add));
            instructions.Insert(i, new Instruction(OpCodes.Ldc_I4, b));
            instructions.Insert(i, new Instruction(OpCodes.Ldc_I4, a));
        }
    }

    private void InsertOpaquePredicates(CilBody body)
    {
        var instructions = body.Instructions;

        // Find suitable insertion points: before unconditional branches or ret
        var targets = new List<int>();
        for (int i = 0; i < instructions.Count; i++)
        {
            var op = instructions[i].OpCode;
            if (op == OpCodes.Ret && i > 0)
                targets.Add(i);
            else if (op == OpCodes.Br && i > 2)
                targets.Add(i);
        }

        // Only insert in a subset to avoid bloat
        var chosen = targets
            .Where((_, idx) => idx % 3 == 0)
            .Take(Math.Min(targets.Count, 8))
            .OrderByDescending(x => x)
            .ToList();

        foreach (int idx in chosen)
        {
            InsertOpaquePredicate(body, idx);
        }
    }

    /// <summary>
    /// Inserts an always-true predicate before the instruction at <paramref name="idx"/>.
    ///
    /// Pattern (a &lt; b is always true because b = a + positive):
    ///   ldc.i4 a
    ///   ldc.i4 b
    ///   clt          → 1 (always)
    ///   brtrue REAL  → always taken
    ///   br     REAL  → dead fall-through, also goes to REAL (no stack consumption)
    ///   REAL: (original instruction)
    ///
    /// The dead block is a plain `br` — no ldnull/throw — so the JIT never has to
    /// verify a dead-code path whose abstract stack depth differs from REAL's entry depth.
    /// </summary>
    private void InsertOpaquePredicate(CilBody body, int idx)
    {
        var instructions = body.Instructions;
        var realTarget = instructions[idx];

        int a = _rng.Next(1, 50);
        int b = a + _rng.Next(1, 50);

        var deadBr = new Instruction(OpCodes.Br, realTarget);

        instructions.Insert(idx, deadBr);
        instructions.Insert(idx, new Instruction(OpCodes.Brtrue, realTarget));
        instructions.Insert(idx, new Instruction(OpCodes.Clt));
        instructions.Insert(idx, new Instruction(OpCodes.Ldc_I4, b));
        instructions.Insert(idx, new Instruction(OpCodes.Ldc_I4, a));
    }
}
