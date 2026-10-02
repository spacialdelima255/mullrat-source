namespace mullvad.Modules;

internal static class ModuleLoader
{
    private static readonly object _initLock = new();
    private static bool _initialized;

    public static string ModulesDir
    {
        get
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Modules");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                File.SetAttributes(dir, FileAttributes.Hidden | FileAttributes.Directory);
            }
            return dir;
        }
    }

    // Encrypt any plain .dll files found in the modules dir, then delete the originals.
    // Call once on server startup.  Encryption is best-effort; raw .dll files still work.
    public static void Initialize()
    {
        lock (_initLock)
        {
            if (_initialized) return;
            _initialized = true;
        }

        var dir = ModulesDir;
        foreach (var dll in Directory.GetFiles(dir, "*.dll"))
        {
            try
            {
                var enc   = Path.ChangeExtension(dll, ".enc");
                var bytes = File.ReadAllBytes(dll);
                var encrypted = ModuleEncryption.Encrypt(bytes);
                File.WriteAllBytes(enc, encrypted);
                try { File.Delete(dll); } catch { /* can't delete — leave dll alongside enc, both work */ }
            }
            catch
            {
                /* Encryption failed for this file — raw .dll fallback still works */
            }
        }
    }

    // Returns raw DLL bytes ready to deliver to the client.
    // Looks for an encrypted .enc first, then falls back to a plain .dll.
    public static byte[]? GetModuleBytes(string moduleFileName)
    {
        var dir = ModulesDir;

        // 1. Encrypted copy (normal path after first run)
        var enc = Path.Combine(dir, moduleFileName + ".enc");
        if (File.Exists(enc))
        {
            try { return ModuleEncryption.Decrypt(File.ReadAllBytes(enc)); }
            catch { /* corrupt/wrong key — fall through to dll */ }
        }

        // 2. Raw dll (right after build, before encryption, or if encryption failed)
        var dll = Path.Combine(dir, moduleFileName + ".dll");
        if (File.Exists(dll))
        {
            try { return File.ReadAllBytes(dll); }
            catch { }
        }

        return null;
    }

    public static IEnumerable<string> GetAvailableModules()
        => Directory.GetFiles(ModulesDir, "*.enc")
                    .Select(Path.GetFileNameWithoutExtension)!;
}
