using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace mullvad.Module.RemoteDesktop
{
    public sealed class RemoteDesktopClientModule
    {
        public static string ModuleId => "mullvad.remotedesktop";

        private static volatile bool _running;
        private static volatile int  _targetFps = 30;
        private static volatile int  _quality   = 70;
        private static volatile int  _cursorOn;
        private static Thread        _captureThread;

        private static readonly SemaphoreSlim _readySig  = new SemaphoreSlim(0, 1);
        private static byte[]                 _frameData;
        private static int                    _frameW, _frameH;
        private static bool                   _frameIsSame;
        private static readonly object        _frameLock = new object();

        private static int _capX, _capY, _capW, _capH;

        private static IntPtr _lastCursorHandle;
        private static int    _hotspotX, _hotspotY;

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "start_stream": return StartStream(payload);
                    case "get_frame":    return GetFrame(payload);
                    case "stop_stream":  return StopStream();
                    default:             return Err("unknown action: " + action);
                }
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        private static string StartStream(string payload)
        {
            if (_running)
            {
                _running = false;
                try { if (_readySig.CurrentCount == 0) _readySig.Release(); } catch { }
                _captureThread?.Join(1000);
            }

            _targetFps = Clamp(ParseInt(payload, "fps", 30), 1, 60);
            _quality   = Clamp(ParseInt(payload, "quality", 70), 10, 100);
            _cursorOn  = Clamp(ParseInt(payload, "cursor", 0), 0, 1);
            int monitorIdx = Clamp(ParseInt(payload, "monitor", 0), 0, 15);

            var monitors = GetMonitorBounds();
            if (monitorIdx < monitors.Count)
            {
                _capX = monitors[monitorIdx][0]; _capY = monitors[monitorIdx][1];
                _capW = monitors[monitorIdx][2]; _capH = monitors[monitorIdx][3];
            }
            else
            {
                _capX = 0; _capY = 0;
                _capW = GetSystemMetrics(0); _capH = GetSystemMetrics(1);
            }

            lock (_frameLock) { _frameData = null; _frameIsSame = false; }
            _lastCursorHandle = IntPtr.Zero;
            _hotspotX = 0; _hotspotY = 0;

            _running = true;
            _captureThread = new Thread(CaptureLoop)
            {
                IsBackground = true,
                Name         = "RdpCapture",
                Priority     = ThreadPriority.AboveNormal,
            };
            _captureThread.Start();

            return "{\"status\":\"ok\",\"width\":" + _capW + ",\"height\":" + _capH
                 + ",\"x\":" + _capX + ",\"y\":" + _capY + "}";
        }

        private static string StopStream()
        {
            _running = false;
            try { if (_readySig.CurrentCount == 0) _readySig.Release(); } catch { }
            return "{\"status\":\"ok\"}";
        }

        private static string GetFrame(string payload)
        {
            if (!string.IsNullOrEmpty(payload))
                ProcessInput(payload);

            if (!_running)
                return "{\"ok\":false,\"reason\":\"not_streaming\"}";

            int waitMs = (3000 / Math.Max(1, _targetFps)) + 50;
            bool got   = _readySig.Wait(waitMs);
            if (!got || !_running)
                return "{\"ok\":false}";

            byte[] frame; int w, h; bool same;
            lock (_frameLock)
            {
                frame = _frameData;
                w = _frameW; h = _frameH;
                same = _frameIsSame;
            }

            if (same)          return "{\"ok\":true,\"same\":true}";
            if (frame == null) return "{\"ok\":false}";

            return "{\"ok\":true,\"w\":" + w + ",\"h\":" + h
                 + ",\"data\":\"" + Convert.ToBase64String(frame) + "\"}";
        }

        private static void CaptureLoop()
        {
            var jpegCodec = GetJpegCodec();
            var encParams = new EncoderParameters(1);
            Bitmap bmp = null;
            int    lastW = 0, lastH = 0;
            ulong  lastHash = 0;
            int    keyCountdown = 0;

            long tickHz       = Stopwatch.Frequency;
            int  lastFps      = _targetFps;
            long tickPerFrame = tickHz / Math.Max(1, lastFps);
            long nextTick     = Stopwatch.GetTimestamp();

            while (_running)
            {
                try
                {
                    int x = _capX, y = _capY, w = _capW, h = _capH;
                    if (w <= 0) { w = GetSystemMetrics(0); h = GetSystemMetrics(1); }

                    if (bmp == null || w != lastW || h != lastH)
                    {
                        bmp?.Dispose();
                        bmp   = new Bitmap(w, h, PixelFormat.Format32bppRgb);
                        lastW = w; lastH = h;
                        lastHash = 0; keyCountdown = 0;
                    }

                    IntPtr hdcScreen = GetDC(IntPtr.Zero);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        IntPtr hdcBmp = g.GetHdc();
                        try
                        {
                            BitBlt(hdcBmp, 0, 0, w, h, hdcScreen, x, y, SRCCOPY);
                            if (_cursorOn == 1) OverlayCursor(hdcBmp, x, y);
                        }
                        finally { g.ReleaseHdc(hdcBmp); }
                    }
                    ReleaseDC(IntPtr.Zero, hdcScreen);

                    ulong hash;
                    unsafe
                    {
                        var bd = bmp.LockBits(new Rectangle(0, 0, w, h),
                            ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
                        hash = FastHash((byte*)bd.Scan0, bd.Stride, h);
                        bmp.UnlockBits(bd);
                    }

                    if (hash == lastHash && keyCountdown > 0)
                    {
                        keyCountdown--;
                        lock (_frameLock)
                        {
                            _frameIsSame = true;
                            _frameW = w; _frameH = h;
                        }
                    }
                    else
                    {
                        encParams.Param[0] = new EncoderParameter(
                            System.Drawing.Imaging.Encoder.Quality, (long)_quality);

                        byte[] encoded;
                        using (var ms = new MemoryStream(Math.Max(4096, w * h / 8)))
                        {
                            bmp.Save(ms, jpegCodec, encParams);
                            encoded = ms.ToArray();
                        }

                        lock (_frameLock)
                        {
                            _frameData   = encoded;
                            _frameW      = w;
                            _frameH      = h;
                            _frameIsSame = false;
                        }
                        lastHash     = hash;
                        keyCountdown = 15;
                    }

                    if (_readySig.CurrentCount == 0)
                        _readySig.Release();
                }
                catch { }

                int curFps = _targetFps;
                if (curFps != lastFps)
                {
                    lastFps = curFps;
                    tickPerFrame = tickHz / Math.Max(1, curFps);
                }
                nextTick += tickPerFrame;
                long now = Stopwatch.GetTimestamp();
                long diff = nextTick - now;
                if (diff > 0)
                {
                    int ms = (int)(diff * 1000 / tickHz);
                    if (ms > 0) Thread.Sleep(ms);
                }
                else if (-diff > tickPerFrame * 2)
                {
                    nextTick = now;
                }
            }

            bmp?.Dispose();
        }

        private static void OverlayCursor(IntPtr hdc, int originX, int originY)
        {
            var ci = new CURSORINFO { cbSize = Marshal.SizeOf(typeof(CURSORINFO)) };
            if (!GetCursorInfo(ref ci)) return;
            if ((ci.flags & CURSOR_SHOWING) == 0 || ci.hCursor == IntPtr.Zero) return;

            if (_lastCursorHandle != ci.hCursor)
            {
                _lastCursorHandle = ci.hCursor;
                if (GetIconInfo(ci.hCursor, out var ii))
                {
                    _hotspotX = ii.xHotspot;
                    _hotspotY = ii.yHotspot;
                    if (ii.hbmMask  != IntPtr.Zero) DeleteObject(ii.hbmMask);
                    if (ii.hbmColor != IntPtr.Zero) DeleteObject(ii.hbmColor);
                }
            }

            int cx = ci.ptScreenPos.x - originX - _hotspotX;
            int cy = ci.ptScreenPos.y - originY - _hotspotY;
            DrawIconEx(hdc, cx, cy, ci.hCursor, 0, 0, 0, IntPtr.Zero, DI_NORMAL);
        }

        private static void ProcessInput(string payload)
        {
            try
            {
                int newFps = ParseInt(payload, "fps", -1);
                if (newFps >= 1 && newFps <= 60) _targetFps = newFps;
                int newQ = ParseInt(payload, "quality", -1);
                if (newQ >= 10 && newQ <= 100) _quality = newQ;
                int newC = ParseInt(payload, "cursor", -1);
                if (newC == 0 || newC == 1) _cursorOn = newC;

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
                    DispatchInputEvent(payload.Substring(start, i - start));
                }
            }
            catch { }
        }

        private static void DispatchInputEvent(string ev)
        {
            var t = GetStr(ev, "t");
            if (t == null) return;

            switch (t)
            {
                case "mm":
                {
                    int x = ParseInt(ev, "x", int.MinValue);
                    int y = ParseInt(ev, "y", int.MinValue);
                    if (x != int.MinValue && y != int.MinValue)
                        SendMouseAbsolute(x, y, MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK, 0);
                    break;
                }
                case "ml":
                    SendMouseButton(ParseInt(ev, "d", 0) == 1 ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP);
                    break;
                case "mr":
                    SendMouseButton(ParseInt(ev, "d", 0) == 1 ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP);
                    break;
                case "mb":
                    SendMouseButton(ParseInt(ev, "d", 0) == 1 ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP);
                    break;
                case "mw":
                    SendMouseButton(MOUSEEVENTF_WHEEL, ParseInt(ev, "delta", 0));
                    break;
                case "kd":
                {
                    int vk = ParseInt(ev, "vk", 0);
                    if (vk > 0) SendKey((ushort)vk, false);
                    break;
                }
                case "ku":
                {
                    int vk = ParseInt(ev, "vk", 0);
                    if (vk > 0) SendKey((ushort)vk, true);
                    break;
                }
            }
        }

        private static void SendMouseAbsolute(int virtX, int virtY, uint flags, int mouseData)
        {
            int vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int vw = Math.Max(1, GetSystemMetrics(SM_CXVIRTUALSCREEN));
            int vh = Math.Max(1, GetSystemMetrics(SM_CYVIRTUALSCREEN));
            int absX = (int)(((long)(virtX - vx) * 65535) / vw);
            int absY = (int)(((long)(virtY - vy) * 65535) / vh);

            var inp = new INPUT { type = INPUT_MOUSE };
            inp.U.mi = new MOUSEINPUT
            {
                dx = absX, dy = absY,
                mouseData = (uint)mouseData,
                dwFlags   = flags,
                time      = 0,
                dwExtraInfo = IntPtr.Zero,
            };
            SendInput(1, new[] { inp }, Marshal.SizeOf(typeof(INPUT)));
        }

        private static void SendMouseButton(uint flags, int mouseData = 0)
        {
            var inp = new INPUT { type = INPUT_MOUSE };
            inp.U.mi = new MOUSEINPUT
            {
                dx = 0, dy = 0,
                mouseData = (uint)mouseData,
                dwFlags   = flags,
                time      = 0,
                dwExtraInfo = IntPtr.Zero,
            };
            SendInput(1, new[] { inp }, Marshal.SizeOf(typeof(INPUT)));
        }

        private static void SendKey(ushort vk, bool up)
        {
            var inp = new INPUT { type = INPUT_KEYBOARD };
            inp.U.ki = new KEYBDINPUT
            {
                wVk = vk, wScan = 0,
                dwFlags = up ? KEYEVENTF_KEYUP : 0u,
                time = 0, dwExtraInfo = IntPtr.Zero,
            };
            SendInput(1, new[] { inp }, Marshal.SizeOf(typeof(INPUT)));
        }

        delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct MONITORINFO { public uint cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

        [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
        [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private static System.Collections.Generic.List<int[]> GetMonitorBounds()
        {
            var list = new System.Collections.Generic.List<int[]>();
            var cb = new MonitorEnumProc((IntPtr hMon, IntPtr hdc, ref RECT rect, IntPtr data) =>
            {
                var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO)) };
                if (GetMonitorInfo(hMon, ref info))
                    list.Add(new int[] { info.rcMonitor.Left, info.rcMonitor.Top,
                        info.rcMonitor.Right - info.rcMonitor.Left,
                        info.rcMonitor.Bottom - info.rcMonitor.Top });
                return true;
            });
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, cb, IntPtr.Zero);
            return list;
        }

        [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int nIndex);
        [DllImport("user32.dll")] static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        [DllImport("user32.dll")] static extern bool GetCursorInfo(ref CURSORINFO pci);
        [DllImport("user32.dll")] static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);
        [DllImport("user32.dll")] static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyHeight, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, uint diFlags);

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int x, y; }

        [StructLayout(LayoutKind.Sequential)]
        struct CURSORINFO { public int cbSize; public int flags; public IntPtr hCursor; public POINT ptScreenPos; }

        [StructLayout(LayoutKind.Sequential)]
        struct ICONINFO { public bool fIcon; public int xHotspot, yHotspot; public IntPtr hbmMask, hbmColor; }

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint type; public InputUnion U; }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT    mi;
            [FieldOffset(0)] public KEYBDINPUT    ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT { public uint uMsg; public ushort wParamL, wParamH; }

        const uint SRCCOPY = 0x00CC0020;

        const uint INPUT_MOUSE    = 0;
        const uint INPUT_KEYBOARD = 1;

        const uint MOUSEEVENTF_MOVE        = 0x0001;
        const uint MOUSEEVENTF_LEFTDOWN    = 0x0002;
        const uint MOUSEEVENTF_LEFTUP      = 0x0004;
        const uint MOUSEEVENTF_RIGHTDOWN   = 0x0008;
        const uint MOUSEEVENTF_RIGHTUP     = 0x0010;
        const uint MOUSEEVENTF_MIDDLEDOWN  = 0x0020;
        const uint MOUSEEVENTF_MIDDLEUP    = 0x0040;
        const uint MOUSEEVENTF_WHEEL       = 0x0800;
        const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
        const uint MOUSEEVENTF_ABSOLUTE    = 0x8000;

        const uint KEYEVENTF_KEYUP = 0x0002;

        const int  SM_XVIRTUALSCREEN  = 76;
        const int  SM_YVIRTUALSCREEN  = 77;
        const int  SM_CXVIRTUALSCREEN = 78;
        const int  SM_CYVIRTUALSCREEN = 79;

        const int  CURSOR_SHOWING = 0x00000001;
        const uint DI_NORMAL      = 0x0003;

        private static ImageCodecInfo GetJpegCodec()
        {
            foreach (var c in ImageCodecInfo.GetImageEncoders())
                if (c.FormatID == ImageFormat.Jpeg.Guid) return c;
            return null;
        }

        private static unsafe ulong FastHash(byte* scan0, int stride, int height)
        {
            const ulong FNV_OFFSET = 14695981039346656037UL;
            const ulong FNV_PRIME  = 1099511628211UL;

            ulong h = FNV_OFFSET;
            int rowStep = Math.Max(1, height / 24);
            int cellsPerRow = stride / 8;
            int colStep = Math.Max(1, cellsPerRow / 48);
            for (int y = 0; y < height; y += rowStep)
            {
                ulong* row = (ulong*)(scan0 + y * stride);
                for (int x = 0; x < cellsPerRow; x += colStep)
                    h = (h ^ row[x]) * FNV_PRIME;
            }
            return h;
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        private static int ParseInt(string json, string key, int def)
        {
            if (string.IsNullOrEmpty(json)) return def;
            var k    = "\"" + key + "\"";
            int idx  = json.IndexOf(k);
            if (idx < 0) return def;
            int colon = json.IndexOf(':', idx + k.Length);
            if (colon < 0) return def;
            int s = colon + 1;
            while (s < json.Length && json[s] == ' ') s++;
            bool neg = s < json.Length && json[s] == '-';
            if (neg) s++;
            int e = s;
            while (e < json.Length && char.IsDigit(json[e])) e++;
            if (e == s) return def;
            return int.TryParse(json.Substring(s, e - s), out int v) ? (neg ? -v : v) : def;
        }

        private static string GetStr(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var k    = "\"" + key + "\"";
            int idx  = json.IndexOf(k);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx + k.Length);
            if (colon < 0) return null;
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
                if (c == '"') sb.Append("\\\"");
                else if (c == '\\') sb.Append("\\\\");
                else sb.Append(c);
            }
            sb.Append('"');
            return "{\"error\":" + sb + "}";
        }
    }
}
