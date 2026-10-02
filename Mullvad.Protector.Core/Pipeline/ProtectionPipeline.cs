using System.IO;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using Mullvad.Protector.Core.Analyzer;
using Mullvad.Protector.Core.ControlFlow;
using Mullvad.Protector.Core.Metadata;
using Mullvad.Protector.Core.Models;
using Mullvad.Protector.Core.Renaming;
using Mullvad.Protector.Core.Strings;
using Mullvad.Protector.Core.Virtualization;

namespace Mullvad.Protector.Core.Pipeline;

public sealed class ProtectionPipeline
{
    public static ProtectionResult Protect(string inputPath, ProtectionMode mode)
    {
        string dir  = Path.GetDirectoryName(inputPath) ?? ".";
        string name = Path.GetFileNameWithoutExtension(inputPath);
        string ext  = Path.GetExtension(inputPath);

        var log     = new List<string>();
        Action<string> logger = s => log.Add(s);

        // .NET 5+ apps: the EXE is a native apphost stub; the managed IL lives in the companion DLL.
        // Detect this and redirect so dnlib sees the actual managed assembly.
        string? apphostExePath = null;
        if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) && !IsManagedPE(inputPath))
        {
            string companionDll = Path.Combine(dir, name + ".dll");
            if (File.Exists(companionDll))
            {
                logger($"[*] Apphost wrapper detected — targeting {Path.GetFileName(companionDll)}");
                apphostExePath = inputPath;
                inputPath = companionDll;
                ext  = ".dll";
                mode = ProtectionMode.Strong;
            }
            else
            {
                logger("[!] Not a managed PE and no companion DLL found.");
                return new ProtectionResult(false, log, null,
                    "Not a managed PE — .NET apphost detected but companion DLL not found alongside the EXE.");
            }
        }

        string output = Path.Combine(dir, $"{name}.protected{ext}");

        logger("[*] Loading assembly...");

        ModuleDefMD module;
        try
        {
            var ctx2 = new ModuleContext();
            var opts = new ModuleCreationOptions(ctx2)
            {
                TryToLoadPdbFromDisk = false
            };
            module = ModuleDefMD.Load(inputPath, opts);
        }
        catch (Exception ex)
        {
            return new ProtectionResult(false, log, null,
                $"Failed to load assembly: {ex.Message}");
        }

        logger("[+] Assembly loaded");

        var ctx = new ProtectionContext
        {
            Module     = module,
            InputPath  = inputPath,
            OutputPath = output,
            Mode       = mode,
            Log        = logger,
        };

        // ── Analysis ────────────────────────────────────────────────────────────
        logger("[*] Analyzing...");
        try
        {
            new AssemblyAnalyzer(ctx).Analyze();
        }
        catch (Exception ex)
        {
            return new ProtectionResult(false, log, null,
                $"Analysis failed: {ex.Message}");
        }

        // Use file extension (after any apphost redirect) to select the writer —
        // NOT module.Kind.  The companion DLL of a .NET 8 WinForms app has
        // ModuleKind.Windows, but it is still a managed DLL: NativeWrite on it
        // can produce subtly wrong method-body RVAs for modified IL, causing
        // InvalidProgramException when the JIT tries to verify the method bodies.
        int isExe = ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        logger($"[+] Types analyzed     ({ctx.RenamableTypes.Count} renamable)");
        logger($"[+] Methods analyzed   ({ctx.RenamableMethods.Count} renamable)");
        logger($"[+] Resources analyzed ({module.Resources.Count} resources)");

        // ── Obfuscation Pass ────────────────────────────────────────────────────
        logger("[*] Applying obfuscation...");

        try
        {
            logger("[+] Symbol renaming");
            new SymbolRenamer(ctx).Apply();
        }
        catch (Exception ex) { logger($"[!] Renaming: {ex.Message}"); }

        try
        {
            logger("[+] String protection");
            new StringProtector(ctx).Apply();
        }
        catch (Exception ex) { logger($"[!] Strings: {ex.Message}"); }

        try
        {
            logger("[+] Virtualization");
            new VirtualMethodProtector(ctx).Apply();
        }
        catch (Exception ex) { logger($"[!] Virtualization: {ex.Message}"); }

        try
        {
            logger("[+] Control-flow protection");
            new ControlFlowTransformer(ctx).Apply();
        }
        catch (Exception ex) { logger($"[!] CF: {ex.Message}"); }

        try
        {
            logger("[+] Metadata cleanup");
            new MetadataCleaner(ctx).Apply();
        }
        catch (Exception ex) { logger($"[!] Metadata: {ex.Message}"); }

        // ── Write Output ────────────────────────────────────────────────────────
        logger("[*] Validating...");
        try
        {
            WriteModule(module, output, isExe == 1);
        }
        catch (Exception ex)
        {
            return new ProtectionResult(false, log, null,
                $"Write failed: {ex.Message}");
        }

        // Quick validation: load the protected assembly and check it parses
        try
        {
            using var verify = ModuleDefMD.Load(output);
            if (verify.EntryPoint is null && isExe == 1)
                logger("[!] Warning: entry point not found in protected EXE");
            else
                logger("[+] Assembly validation passed");
        }
        catch (Exception ex)
        {
            return new ProtectionResult(false, log, output,
                $"Validation failed: {ex.Message}");
        }

        // If we redirected from an apphost EXE, copy the native wrapper so the result is runnable.
        if (apphostExePath != null)
        {
            string exeOutput = Path.Combine(dir, $"{name}.protected.exe");
            File.Copy(apphostExePath, exeOutput, overwrite: true);
            logger($"[+] EXE wrapper copied  → {Path.GetFileName(exeOutput)}");
            output = exeOutput; // surface the EXE path as the primary output
        }

        logger("[+] Protection complete");
        return new ProtectionResult(true, log, output, null);
    }

    // Returns true if the file is a managed PE (.NET data directory RVA ≠ 0).
    private static bool IsManagedPE(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            if (br.ReadUInt16() != 0x5A4D) return false; // no MZ

            fs.Seek(0x3C, SeekOrigin.Begin);
            int peOffset = br.ReadInt32();

            fs.Seek(peOffset, SeekOrigin.Begin);
            if (br.ReadUInt32() != 0x00004550) return false; // no PE\0\0

            // Skip COFF header to optional header
            fs.Seek(peOffset + 24, SeekOrigin.Begin);
            ushort magic = br.ReadUInt16(); // 0x10B = PE32, 0x20B = PE32+

            // Data directories start at: optional header base + (PE32→96 | PE32+→112)
            int dataDirStart = magic == 0x20B ? 112 : 96;
            fs.Seek(peOffset + 24 + dataDirStart + 14 * 8, SeekOrigin.Begin);

            return br.ReadUInt32() != 0; // COM+ 2.0 data directory RVA
        }
        catch { return false; }
    }

    private static void WriteModule(ModuleDefMD module, string outputPath, bool isExe)
    {
        // All inputs that reach this point are pure managed PEs — the apphost-redirect
        // check above screens out non-managed EXEs before we ever load the module.
        // NativeModuleWriter is for C++/CLI mixed-mode assemblies only; using it on a
        // pure managed PE with modified IL bodies (stubs, injected types, string decoder)
        // produces wrong method-body RVAs → InvalidProgramException at JIT time.
        // Always use the managed writer regardless of .exe vs .dll extension.
        _ = isExe; // kept in signature for the entry-point validation check at the call site
        var opts = new ModuleWriterOptions(module)
        {
            Logger = DummyLogger.NoThrowInstance,
            MetadataOptions =
            {
                Flags = MetadataFlags.PreserveAll
            },
        };
        module.Write(outputPath, opts);
    }
}

public sealed record ProtectionResult(
    bool Success,
    IReadOnlyList<string> Log,
    string? OutputPath,
    string? ErrorMessage);
