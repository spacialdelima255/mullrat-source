using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using Microsoft.Win32;

namespace mullvad.Module.RemoteScripting
{
    public sealed class RemoteScriptingModule
    {
        public static string ModuleId => "mullvad.scripting";

        private static readonly string BaseDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "mullvad", "scripting");

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "info":            return GetInfo();
                    case "run":             return Run(payload);
                    case "setup_python":    return SetupPython(payload);
                    case "setup_compiler":  return SetupCompiler();
                    default:                return Err("Unknown action: " + action);
                }
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // ── Info ─────────────────────────────────────────────────────────────────

        private static string GetInfo()
        {
            var sb = new StringBuilder(1024);
            sb.Append('{');

            string psVer = GetPowerShellVersion();
            sb.Append("\"powershell\":").Append(Json(psVer));

            var pythons = FindPythonInstalls();
            string? embeddedPy = FindEmbeddedPython();
            if (embeddedPy != null) pythons.Insert(0, ("Embedded " + GetPythonVersion(embeddedPy), embeddedPy));

            sb.Append(",\"python\":[");
            for (int i = 0; i < pythons.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"version\":").Append(Json(pythons[i].ver))
                  .Append(",\"path\":").Append(Json(pythons[i].path)).Append('}');
            }
            sb.Append(']');

            string? cpp = FindCompiler("g++") ?? FindCompiler("cl");
            string? cc  = FindCompiler("gcc") ?? FindCompiler("cl");
            sb.Append(",\"cpp\":").Append(cpp != null ? Json(cpp) : "null");
            sb.Append(",\"cc\":").Append(cc != null ? Json(cc) : "null");

            string? rustc = FindCompiler("rustc");
            sb.Append(",\"rustc\":").Append(rustc != null ? Json(rustc) : "null");

            sb.Append(",\"embedded_python\":").Append(embeddedPy != null ? Json(embeddedPy) : "null");

            sb.Append('}');
            return sb.ToString();
        }

        // ── Run ──────────────────────────────────────────────────────────────────

        private static string Run(string payload)
        {
            string lang       = ExtractString(payload, "lang")        ?? "";
            string code       = ExtractString(payload, "code")        ?? "";
            string pythonPath = ExtractString(payload, "python_path") ?? "";

            Directory.CreateDirectory(BaseDir);
            string tempBase = Path.Combine(BaseDir, "run_" + Guid.NewGuid().ToString("N").Substring(0, 8));

            try
            {
                switch (lang.ToLowerInvariant())
                {
                    case "powershell": return RunPowerShell(code);
                    case "python":     return RunPython(code, pythonPath, tempBase);
                    case "cpp":        return RunCompiled(code, tempBase, ".cpp", FindCompiler("g++") ?? FindCompiler("cl"), true);
                    case "c":          return RunCompiled(code, tempBase, ".c",   FindCompiler("gcc") ?? FindCompiler("cl"), false);
                    case "rust":       return RunRust(code, tempBase);
                    default:           return Err("Unsupported language: " + lang);
                }
            }
            finally
            {
                TryDelete(tempBase + ".*");
            }
        }

        private static string RunPowerShell(string code)
        {
            string wrapped = "$ProgressPreference='SilentlyContinue'\n" + code;
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(wrapped));
            var result = Exec("powershell.exe", $"-NoProfile -NoLogo -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}", 30000);
            return CleanPowerShellResult(result);
        }

        private static string CleanPowerShellResult(string json)
        {
            string? stderr = ExtractString(json, "stderr");
            if (stderr != null && stderr.Contains("#< CLIXML"))
            {
                stderr = StripCliXml(stderr);
                string? stdout = ExtractString(json, "stdout");
                string? ecStr  = ExtractString(json, "exit_code");
                string? durStr = ExtractString(json, "duration_ms");
                int ec = 0; if (ecStr != null) int.TryParse(ecStr, out ec);
                long dur = 0; if (durStr != null) long.TryParse(durStr, out dur);
                return Result(ec, stdout ?? "", stderr, dur);
            }
            return json;
        }

        private static string StripCliXml(string stderr)
        {
            if (string.IsNullOrEmpty(stderr)) return "";
            int idx = stderr.IndexOf("#< CLIXML", StringComparison.Ordinal);
            if (idx < 0) return stderr;
            string before = stderr.Substring(0, idx).TrimEnd();
            int end = stderr.IndexOf("</Objs>", idx, StringComparison.Ordinal);
            string after = end >= 0 && end + 7 < stderr.Length ? stderr.Substring(end + 7).TrimStart() : "";
            string cleaned = (before + "\n" + after).Trim();
            return cleaned;
        }

        private static string RunPython(string code, string pythonPath, string tempBase)
        {
            if (string.IsNullOrEmpty(pythonPath))
            {
                pythonPath = FindEmbeddedPython();
                if (pythonPath == null)
                {
                    var installs = FindPythonInstalls();
                    pythonPath = installs.Count > 0 ? installs[0].path : null;
                }
            }
            if (pythonPath == null)
                return Err("No Python found. Use setup_python to install an embedded copy.");

            string src = tempBase + ".py";
            File.WriteAllText(src, code, Encoding.UTF8);
            return Exec(pythonPath, $"\"{src}\"", 30000);
        }

        private static string RunCompiled(string code, string tempBase, string ext, string? compiler, bool isCpp)
        {
            if (compiler == null)
                return Err("No C/C++ compiler found. Use setup_compiler to install a portable one.");

            string src = tempBase + ext;
            string exe = tempBase + ".exe";
            File.WriteAllText(src, code, Encoding.UTF8);

            string compilerName = Path.GetFileNameWithoutExtension(compiler).ToLowerInvariant();
            string args;
            if (compilerName == "cl")
                args = $"/nologo /EHsc /Fe:\"{exe}\" \"{src}\"";
            else
                args = $"-o \"{exe}\" \"{src}\"";

            var compileResult = Exec(compiler, args, 30000);
            if (!File.Exists(exe))
                return compileResult;

            var runResult = Exec(exe, "", 30000);

            string compErr = ExtractString(compileResult, "stderr") ?? "";
            string runOut  = ExtractString(runResult, "stdout") ?? "";
            string runErr  = ExtractString(runResult, "stderr") ?? "";

            int exitCode = 0;
            string? ecStr = ExtractString(runResult, "exit_code");
            if (ecStr != null) int.TryParse(ecStr, out exitCode);

            long dur = 0;
            string? durStr = ExtractString(runResult, "duration_ms");
            if (durStr != null) long.TryParse(durStr, out dur);

            var sb = new StringBuilder();
            if (compErr.Length > 0) sb.Append("[compiler] ").Append(compErr).Append('\n');
            sb.Append(runOut);

            return Result(exitCode, sb.ToString(), runErr, dur);
        }

        private static string RunRust(string code, string tempBase)
        {
            string? rustc = FindCompiler("rustc");
            if (rustc == null)
                return Err("rustc not found. Install Rust from rustup.rs.");

            string src = tempBase + ".rs";
            string exe = tempBase + ".exe";
            File.WriteAllText(src, code, Encoding.UTF8);

            var compileResult = Exec(rustc, $"\"{src}\" -o \"{exe}\"", 60000);
            if (!File.Exists(exe))
                return compileResult;

            var runResult = Exec(exe, "", 30000);

            string compErr = ExtractString(compileResult, "stderr") ?? "";
            string runOut  = ExtractString(runResult, "stdout") ?? "";
            string runErr  = ExtractString(runResult, "stderr") ?? "";
            int exitCode = 0;
            string? ecStr = ExtractString(runResult, "exit_code");
            if (ecStr != null) int.TryParse(ecStr, out exitCode);
            long dur = 0;
            string? durStr = ExtractString(runResult, "duration_ms");
            if (durStr != null) long.TryParse(durStr, out dur);

            var sb = new StringBuilder();
            if (compErr.Length > 0) sb.Append("[compiler] ").Append(compErr).Append('\n');
            sb.Append(runOut);

            return Result(exitCode, sb.ToString(), runErr, dur);
        }

        // ── Setup Python ─────────────────────────────────────────────────────────

        private static string SetupPython(string payload)
        {
            string version = ExtractString(payload, "version") ?? "3.12.7";
            string arch = IntPtr.Size == 8 ? "amd64" : "win32";
            string url = $"https://www.python.org/ftp/python/{version}/python-{version}-embed-{arch}.zip";

            string destDir = Path.Combine(BaseDir, "python-" + version);
            string pythonExe = Path.Combine(destDir, "python.exe");

            if (File.Exists(pythonExe))
                return "{\"success\":true,\"path\":" + Json(pythonExe) + ",\"version\":" + Json(version) + "}";

            Directory.CreateDirectory(destDir);
            string zipPath = Path.Combine(BaseDir, "python-" + version + ".zip");

            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                    wc.DownloadFile(url, zipPath);

                ZipFile.ExtractToDirectory(zipPath, destDir);

                string[] pthFiles = Directory.GetFiles(destDir, "python*._pth");
                foreach (var pth in pthFiles)
                {
                    string content = File.ReadAllText(pth);
                    content = content.Replace("#import site", "import site");
                    File.WriteAllText(pth, content);
                }

                return "{\"success\":true,\"path\":" + Json(pythonExe) + ",\"version\":" + Json(version) + "}";
            }
            catch (Exception ex)
            {
                return Err("Failed to download Python: " + ex.Message);
            }
            finally
            {
                TryDeleteFile(zipPath);
            }
        }

        // ── Setup Compiler (portable w64devkit) ──────────────────────────────────

        private static string SetupCompiler()
        {
            string destDir = Path.Combine(BaseDir, "w64devkit");
            string gccExe  = Path.Combine(destDir, "bin", "gcc.exe");

            if (File.Exists(gccExe))
                return "{\"success\":true,\"path\":" + Json(destDir) + "}";

            Directory.CreateDirectory(BaseDir);
            string zipPath = Path.Combine(BaseDir, "w64devkit.zip");

            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                string downloadUrl = GetW64DevkitUrl();
                if (downloadUrl == null)
                    return Err("Could not find w64devkit download URL from GitHub.");

                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "mullvad-scripting");
                    wc.DownloadFile(downloadUrl, zipPath);
                }

                ZipFile.ExtractToDirectory(zipPath, BaseDir);

                if (!File.Exists(gccExe))
                    return Err("Extraction completed but gcc.exe not found.");

                return "{\"success\":true,\"path\":" + Json(destDir) + "}";
            }
            catch (Exception ex)
            {
                return Err("Failed to set up compiler: " + ex.Message);
            }
            finally
            {
                TryDeleteFile(zipPath);
            }
        }

        private static string? GetW64DevkitUrl()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "mullvad-scripting");
                    string json = wc.DownloadString("https://api.github.com/repos/skeeto/w64devkit/releases/latest");
                    string arch = IntPtr.Size == 8 ? "x64" : "i686";
                    return FindAssetUrl(json, arch);
                }
            }
            catch { return null; }
        }

        private static string? FindAssetUrl(string json, string arch)
        {
            string marker = "browser_download_url";
            int pos = 0;
            while (true)
            {
                int idx = json.IndexOf(marker, pos, StringComparison.Ordinal);
                if (idx < 0) return null;
                int urlStart = json.IndexOf("https://", idx, StringComparison.Ordinal);
                if (urlStart < 0) return null;
                int urlEnd = json.IndexOf('"', urlStart);
                if (urlEnd < 0) return null;
                string url = json.Substring(urlStart, urlEnd - urlStart);
                if (url.Contains(arch) && url.EndsWith(".zip") && !url.Contains("mini"))
                    return url;
                pos = urlEnd;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static string? FindCompiler(string name)
        {
            // check embedded w64devkit first (gcc, g++ only)
            if (name == "gcc" || name == "g++")
            {
                string embedded = Path.Combine(BaseDir, "w64devkit", "bin", name + ".exe");
                if (File.Exists(embedded)) return embedded;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = name,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                };
                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return null;
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(5000);
                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    string line = lines.Length > 0 ? lines[0].Trim() : "";
                    return line.Length > 0 && File.Exists(line) ? line : null;
                }
            }
            catch { return null; }
        }

        private static string Exec(string exe, string args, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = exe,
                    Arguments              = args,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    WorkingDirectory       = BaseDir,
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return Err("Failed to start process");
                    string stdout = proc.StandardOutput.ReadToEnd();
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(timeoutMs);
                    if (!proc.HasExited) { try { proc.Kill(); } catch { } }
                    sw.Stop();
                    return Result(proc.ExitCode, stdout, stderr, sw.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                return Err(ex.Message);
            }
        }

        private static string Result(int exitCode, string stdout, string stderr, long durationMs = 0)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"exit_code\":").Append(exitCode)
              .Append(",\"stdout\":").Append(Json(stdout))
              .Append(",\"stderr\":").Append(Json(stderr))
              .Append(",\"duration_ms\":").Append(durationMs)
              .Append('}');
            return sb.ToString();
        }

        private static string GetPowerShellVersion()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -NoLogo -Command \"$PSVersionTable.PSVersion.ToString()\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                };
                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return "unknown";
                    string ver = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit(5000);
                    return ver.Length > 0 ? ver : "unknown";
                }
            }
            catch { return "unknown"; }
        }

        private static List<(string ver, string path)> FindPythonInstalls()
        {
            var result = new List<(string, string)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string[] roots = {
                @"SOFTWARE\Python\PythonCore",
                @"SOFTWARE\WOW6432Node\Python\PythonCore",
            };
            foreach (var root in roots)
            {
                foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
                {
                    try
                    {
                        using (var key = hive.OpenSubKey(root))
                        {
                            if (key == null) continue;
                            foreach (var ver in key.GetSubKeyNames())
                            {
                                using (var installKey = key.OpenSubKey(ver + @"\InstallPath"))
                                {
                                    if (installKey == null) continue;
                                    string? dir = installKey.GetValue("")?.ToString()
                                               ?? installKey.GetValue("ExecutablePath")?.ToString();
                                    if (dir == null) continue;
                                    string exe = dir.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                                        ? dir
                                        : Path.Combine(dir, "python.exe");
                                    if (File.Exists(exe) && seen.Add(exe))
                                        result.Add((ver, exe));
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            try
            {
                string? pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (pathEnv != null)
                {
                    foreach (var dir in pathEnv.Split(';'))
                    {
                        string exe = Path.Combine(dir.Trim(), "python.exe");
                        if (File.Exists(exe) && seen.Add(exe))
                            result.Add((GetPythonVersion(exe), exe));
                    }
                }
            }
            catch { }

            return result;
        }

        private static string? FindEmbeddedPython()
        {
            try
            {
                if (!Directory.Exists(BaseDir)) return null;
                foreach (var dir in Directory.GetDirectories(BaseDir, "python-*"))
                {
                    string exe = Path.Combine(dir, "python.exe");
                    if (File.Exists(exe)) return exe;
                }
            }
            catch { }
            return null;
        }

        private static string GetPythonVersion(string pythonExe)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = "--version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return "?";
                    string ver = proc.StandardOutput.ReadToEnd().Trim();
                    if (string.IsNullOrEmpty(ver)) ver = proc.StandardError.ReadToEnd().Trim();
                    proc.WaitForExit(5000);
                    return ver.Replace("Python ", "");
                }
            }
            catch { return "?"; }
        }

        private static void TryDelete(string pattern)
        {
            try
            {
                string? dir = Path.GetDirectoryName(pattern);
                if (dir == null) return;
                string name = Path.GetFileName(pattern).Replace(".*", "");
                foreach (var f in Directory.GetFiles(dir, name + "*"))
                    TryDeleteFile(f);
            }
            catch { }
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static string Err(string msg)
            => "{\"success\":false,\"error\":" + Json(msg) + "}";

        private static string Json(string? s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                           .Replace("\r", "\\r").Replace("\n", "\\n")
                           .Replace("\t", "\\t") + "\"";
        }

        private static string? ExtractString(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            string needle = "\"" + key + "\"";
            int idx = json.IndexOf(needle, StringComparison.Ordinal);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx + needle.Length);
            if (colon < 0) return null;
            int s = colon + 1;
            while (s < json.Length && (json[s] == ' ' || json[s] == '\t')) s++;

            if (s < json.Length && (char.IsDigit(json[s]) || json[s] == '-'))
            {
                int e = s;
                if (json[e] == '-') e++;
                while (e < json.Length && char.IsDigit(json[e])) e++;
                return json.Substring(s, e - s);
            }

            if (s >= json.Length || json[s] != '"') return null;
            s++;
            var sb = new StringBuilder();
            for (int i = s; i < json.Length; i++)
            {
                if (json[i] == '\\' && i + 1 < json.Length)
                {
                    char c = json[++i];
                    switch (c)
                    {
                        case '"':  sb.Append('"');  break;
                        case '\\': sb.Append('\\'); break;
                        case 'n':  sb.Append('\n'); break;
                        case 'r':  sb.Append('\r'); break;
                        case 't':  sb.Append('\t'); break;
                        default:   sb.Append(c); break;
                    }
                }
                else if (json[i] == '"') break;
                else sb.Append(json[i]);
            }
            return sb.ToString();
        }
    }
}
