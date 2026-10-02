using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace mullvad.Module.Clipboard
{
    public sealed class ClipboardClientModule
    {
        public static string ModuleId => "mullvad.clipboard";

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "poll": return Poll();
                    case "set":  return Set(payload);
                    default:     return Err("Unknown action: " + action);
                }
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // ── Poll ─────────────────────────────────────────────────────────────────

        private static string Poll()
        {
            string text = GetClipboardText();
            return "{\"text\":" + Json(text) + "}";
        }

        // ── Set ──────────────────────────────────────────────────────────────────

        private static string Set(string text)
        {
            SetClipboardText(text ?? "");
            return "{\"success\":true}";
        }

        // ── STA-thread clipboard helpers ──────────────────────────────────────────

        private static bool OpenClipboardWithRetry(int tries = 5)
        {
            for (int i = 0; i < tries; i++)
            {
                if (OpenClipboard(IntPtr.Zero)) return true;
                Thread.Sleep(20);
            }
            return false;
        }

        private static string GetClipboardText()
        {
            string result = "";
            var t = new Thread(() =>
            {
                try
                {
                    if (!IsClipboardFormatAvailable(CF_UNICODETEXT)) return;
                    if (!OpenClipboardWithRetry()) return;
                    try
                    {
                        IntPtr h = GetClipboardData(CF_UNICODETEXT);
                        if (h == IntPtr.Zero) return;
                        IntPtr p = GlobalLock(h);
                        if (p == IntPtr.Zero) return;
                        try { result = Marshal.PtrToStringUni(p) ?? ""; }
                        finally { GlobalUnlock(h); }
                    }
                    finally { CloseClipboard(); }
                }
                catch { }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join(2000);
            return result;
        }

        private static void SetClipboardText(string text)
        {
            var t = new Thread(() =>
            {
                try
                {
                    if (!OpenClipboardWithRetry()) return;
                    try
                    {
                        EmptyClipboard();
                        if (text.Length == 0) return;
                        int byteCount = (text.Length + 1) * 2;
                        IntPtr h = GlobalAlloc(GMEM_MOVEABLE, (uint)byteCount);
                        if (h == IntPtr.Zero) return;
                        IntPtr p = GlobalLock(h);
                        if (p == IntPtr.Zero) { GlobalFree(h); return; }
                        try
                        {
                            for (int i = 0; i < text.Length; i++)
                                Marshal.WriteInt16(p, i * 2, (short)text[i]);
                            Marshal.WriteInt16(p, text.Length * 2, 0);
                        }
                        finally { GlobalUnlock(h); }
                        SetClipboardData(CF_UNICODETEXT, h);
                    }
                    finally { CloseClipboard(); }
                }
                catch { }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join(2000);
        }

        // ── P/Invoke ──────────────────────────────────────────────────────────────

        [DllImport("user32.dll")] static extern bool OpenClipboard(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool CloseClipboard();
        [DllImport("user32.dll")] static extern bool EmptyClipboard();
        [DllImport("user32.dll")] static extern IntPtr GetClipboardData(uint uFormat);
        [DllImport("user32.dll")] static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
        [DllImport("user32.dll")] static extern bool IsClipboardFormatAvailable(uint format);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr hMem);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalAlloc(uint uFlags, uint dwBytes);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalFree(IntPtr hMem);

        const uint CF_UNICODETEXT = 13;
        const uint GMEM_MOVEABLE  = 0x0002;

        // ── JSON helpers ──────────────────────────────────────────────────────────

        static string Err(string msg)
            => "{\"error\":" + Json(msg) + "}";

        static string Json(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\")
                           .Replace("\"", "\\\"")
                           .Replace("\r", "\\r")
                           .Replace("\n", "\\n")
                           .Replace("\t", "\\t") + "\"";
        }
    }
}
