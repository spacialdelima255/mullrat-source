using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Dependencies.General
{
    public static class ProcessWindows
    {
        public class ProcInfo
        {
            public string Name { get; set; }

            public string Pid { get; set; }

            public string Path { get; set; }

            public string Memory { get; set; }
        }

        private static readonly Lazy<List<ProcInfo>> _procInfos = new Lazy<List<ProcInfo>>(BuildCache, isThreadSafe: true);

        public static List<ProcInfo> GetProcInfos()
        {
            return new List<ProcInfo>(_procInfos.Value);
        }

        public static List<string> FindFolder(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return new List<string>();
            }
            ConcurrentDictionary<string, byte> concurrentDictionary = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            SearchNearby(folderName, isDirectory: true, concurrentDictionary);
            return new List<string>(concurrentDictionary.Keys);
        }

        public static List<string> FindFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return new List<string>();
            }
            ConcurrentDictionary<string, byte> concurrentDictionary = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            SearchNearby(fileName, isDirectory: false, concurrentDictionary);
            return new List<string>(concurrentDictionary.Keys);
        }

        private static void SearchNearby(string target, bool isDirectory, ConcurrentDictionary<string, byte> found, int maxUp = 3)
        {
            if (string.IsNullOrEmpty(target))
            {
                return;
            }
            List<ProcInfo> value = _procInfos.Value;
            if (value == null || value.Count == 0)
            {
                return;
            }
            string t = target.Trim();
            Parallel.ForEach(value, delegate (ProcInfo proc)
            {
                try
                {
                    string path = proc.Path;
                    if (!string.IsNullOrEmpty(path))
                    {
                        string directoryName = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(directoryName))
                        {
                            for (int i = 0; i < maxUp; i++)
                            {
                                if (string.IsNullOrEmpty(directoryName))
                                {
                                    break;
                                }
                                string path2 = Path.Combine(directoryName, t);
                                if (isDirectory)
                                {
                                    if (Directory.Exists(path2))
                                    {
                                        try
                                        {
                                            found.TryAdd(Path.GetFullPath(path2), 0);
                                        }
                                        catch
                                        {
                                        }
                                    }
                                }
                                else if (File.Exists(path2))
                                {
                                    try
                                    {
                                        found.TryAdd(Path.GetFullPath(path2), 0);
                                    }
                                    catch
                                    {
                                    }
                                }
                                directoryName = Path.GetDirectoryName(directoryName);
                            }
                        }
                    }
                }
                catch
                {
                }
            });
        }

        private static List<ProcInfo> BuildCache()
        {
            ConcurrentDictionary<string, ProcInfo> result = new ConcurrentDictionary<string, ProcInfo>(StringComparer.OrdinalIgnoreCase);
            uint num = 4096u;
            uint[] pids = new uint[num];
            if (!NativeMethods.EnumProcesses(pids, num * 4, out var lpcbNeeded))
            {
                num = 65536u;
                pids = new uint[num];
                if (!NativeMethods.EnumProcesses(pids, num * 4, out lpcbNeeded))
                {
                    return new List<ProcInfo>();
                }
            }
            int num2 = (int)(lpcbNeeded / 4);
            if (num2 <= 0)
            {
                return new List<ProcInfo>();
            }
            ThreadLocal<StringBuilder> sbLocal = new ThreadLocal<StringBuilder>(() => new StringBuilder(1024));
            Parallel.For(0, num2, delegate (int i)
            {
                uint num3 = pids[i];
                if (num3 == 0 || num3 == 4)
                {
                    return;
                }
                IntPtr intPtr = IntPtr.Zero;
                try
                {
                    intPtr = NativeMethods.OpenProcess(5136u, bInheritHandle: false, (int)num3);
                    if (!(intPtr == IntPtr.Zero))
                    {
                        string text = null;
                        try
                        {
                            StringBuilder value = sbLocal.Value;
                            value.Clear();
                            uint lpdwSize = (uint)value.Capacity;
                            if (NativeMethods.QueryFullProcessImageName(intPtr, 0, value, ref lpdwSize))
                            {
                                text = value.ToString(0, (int)lpdwSize);
                            }
                        }
                        catch
                        {
                        }
                        string text2 = null;
                        if (!string.IsNullOrEmpty(text))
                        {
                            try
                            {
                                text2 = Path.GetFileNameWithoutExtension(text);
                            }
                            catch
                            {
                                text2 = text;
                            }
                        }
                        else
                        {
                            text2 = "";
                        }
                        string text3 = "";
                        try
                        {
                            NativeMethods.PROCESS_MEMORY_COUNTERS_EX ppsmemCounters = new NativeMethods.PROCESS_MEMORY_COUNTERS_EX
                            {
                                cb = (uint)Marshal.SizeOf(typeof(NativeMethods.PROCESS_MEMORY_COUNTERS_EX))
                            };
                            if (NativeMethods.GetProcessMemoryInfo(intPtr, out ppsmemCounters, ppsmemCounters.cb))
                            {
                                text3 = FormatBytes((long)ppsmemCounters.WorkingSetSize.ToUInt64());
                            }
                        }
                        catch
                        {
                        }
                        ProcInfo procInfo = new ProcInfo
                        {
                            Name = SafeString(text2),
                            Pid = num3.ToString(),
                            Path = SafeString(text),
                            Memory = (text3 ?? "")
                        };
                        string key = procInfo.Path + "|" + procInfo.Pid;
                        result.TryAdd(key, procInfo);
                    }
                }
                catch
                {
                }
                finally
                {
                    try
                    {
                        if (intPtr != IntPtr.Zero)
                        {
                            NativeMethods.CloseHandle(intPtr);
                        }
                    }
                    catch
                    {
                    }
                }
            });
            sbLocal.Dispose();
            return new List<ProcInfo>(result.Values);
        }

        private static string SafeString(string s)
        {
            if (!string.IsNullOrEmpty(s))
            {
                return s;
            }
            return "";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1073741824)
            {
                return ((double)bytes / 1073741824.0).ToString("0.##") + " GB";
            }
            if (bytes >= 1048576)
            {
                return ((double)bytes / 1048576.0).ToString("0.##") + " MB";
            }
            if (bytes >= 1024)
            {
                return ((double)bytes / 1024.0).ToString("0.##") + " KB";
            }
            return bytes + " B";
        }
    }
}
