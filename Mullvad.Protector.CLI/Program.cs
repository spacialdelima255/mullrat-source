using Mullvad.Protector.Core.Models;
using Mullvad.Protector.Core.Pipeline;

const string VERSION = "1.0.0";
const string HEADER = $"Mullvad Protector v{VERSION}";
const string DIVIDER = "────────────────────────────────";

if (args.Length == 0)
{
    PrintUsage();
    return 0;
}

int exitCode = 0;

foreach (var inputPath in args)
{
    if (!File.Exists(inputPath))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"[!] File not found: {inputPath}");
        Console.ResetColor();
        exitCode = 1;
        continue;
    }

    string ext  = Path.GetExtension(inputPath).ToLowerInvariant();
    string name = Path.GetFileName(inputPath);
    bool   isDll = ext == ".dll";
    var    mode = isDll ? ProtectionMode.Strong : ProtectionMode.Standard;

    Console.WriteLine();
    Console.WriteLine(HEADER);
    Console.WriteLine(DIVIDER);
    Print($"[] Input: {name}");

    var result = ProtectionPipeline.Protect(inputPath, mode);

    foreach (var line in result.Log)
        Print(line);

    if (!result.Success)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[!] Protection failed: {result.ErrorMessage}");
        Console.ResetColor();
        exitCode = 1;
        continue;
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"Output: {Path.GetFileName(result.OutputPath)}");
    Console.ResetColor();
    Console.WriteLine();
}

return exitCode;

static void Print(string line)
{
    if (line.StartsWith("[+]"))
        Console.ForegroundColor = ConsoleColor.Green;
    else if (line.StartsWith("[*]"))
        Console.ForegroundColor = ConsoleColor.Cyan;
    else if (line.StartsWith("[!]"))
        Console.ForegroundColor = ConsoleColor.Yellow;
    else
        Console.ResetColor();

    Console.WriteLine(line);
    Console.ResetColor();
}

static void PrintUsage()
{
    Console.WriteLine($"{HEADER}");
    Console.WriteLine(DIVIDER);
    Console.WriteLine("Usage: mullvad-protector <input.exe|dll> [input2.dll ...]");
    Console.WriteLine();
    Console.WriteLine("  EXE → Standard protection (renaming, strings, integrity)");
    Console.WriteLine("  DLL → Strong protection   (all passes + CF + constants)");
    Console.WriteLine();
    Console.WriteLine("Output is written to <name>.protected.<ext> in the same directory.");
}
