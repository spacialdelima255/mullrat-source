using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace mullvad.Module.RemoteAudio
{
    public sealed class RemoteAudioClientModule
    {
        public static string ModuleId => "mullvad.remoteaudio";

        private static volatile bool  _running;
        private static Thread          _captureThread;
        private static readonly Queue<byte[]> _audioQueue = new Queue<byte[]>(32);
        private static readonly object _queueLock = new object();

        // Format sent to server (always int16 stereo 44100)
        private const int OUT_RATE     = 44100;
        private const int OUT_CHANNELS = 2;
        private const int OUT_BITS     = 16;

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "devices":      return ListDevices();
                    case "start_stream": return StartStream(payload);
                    case "get_audio":    return GetAudio();
                    case "stop_stream":  return StopStream();
                    default:             return Err("unknown action: " + action);
                }
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // ── Device enumeration (WASAPI render endpoints for loopback) ─────────

        private static string ListDevices()
        {
            var devices = new List<string>();
            try
            {
                var enumType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
                if (enumType == null) return FallbackListDevices();
                var enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(enumType);

                IMMDeviceCollection collection;
                // eRender = 0, DEVICE_STATE_ACTIVE = 1
                if (enumerator.EnumAudioEndpoints(0, 1, out collection) != 0) return FallbackListDevices();

                uint count;
                collection.GetCount(out count);

                for (uint i = 0; i < count; i++)
                {
                    IMMDevice device;
                    if (collection.Item(i, out device) != 0) continue;
                    try
                    {
                        IPropertyStore props;
                        if (device.OpenPropertyStore(0 /* STGM_READ */, out props) == 0)
                        {
                            // PKEY_Device_FriendlyName = {A45C254E-DF1C-4EFD-8020-67D146A850E0}, pid=14
                            var pkey = new PROPERTYKEY { fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), pid = 14 };
                            PropVariant pv = new PropVariant();
                            string name = "Audio Output " + i;
                            if (props.GetValue(ref pkey, out pv) == 0 && pv.vt == 31 /*VT_LPWSTR*/ && pv.p != IntPtr.Zero)
                                name = Marshal.PtrToStringUni(pv.p) ?? name;
                            PropVariantClear(ref pv);
                            devices.Add(name);
                            Marshal.ReleaseComObject(props);
                        }
                    }
                    finally { Marshal.ReleaseComObject(device); }
                }
                Marshal.ReleaseComObject(collection);
                Marshal.ReleaseComObject(enumerator);
            }
            catch { }

            if (devices.Count == 0) return FallbackListDevices();

            var sb = new StringBuilder("[");
            for (int i = 0; i < devices.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"id\":").Append(i).Append(",\"name\":\"").Append(EscJson(devices[i])).Append("\"}");
            }
            sb.Append(']');
            return "{\"ok\":true,\"devices\":" + sb + "}";
        }

        private static string FallbackListDevices()
        {
            return "{\"ok\":true,\"devices\":[{\"id\":0,\"name\":\"Default System Audio\"}]}";
        }

        // ── Stream control ────────────────────────────────────────────────────

        private static string StartStream(string payload)
        {
            if (_running) StopStream();

            int deviceIdx = ParseInt(payload, "device", 0);
            _running = true;
            lock (_queueLock) _audioQueue.Clear();

            _captureThread = new Thread(() => CaptureLoop(deviceIdx))
            {
                IsBackground = true,
                Name         = "WasapiCapture",
            };
            _captureThread.Start();

            return "{\"ok\":true,\"sampleRate\":" + OUT_RATE + ",\"channels\":" + OUT_CHANNELS + ",\"bits\":" + OUT_BITS + "}";
        }

        private static string StopStream()
        {
            _running = false;
            _captureThread?.Join(2000);
            lock (_queueLock) _audioQueue.Clear();
            return "{\"ok\":true}";
        }

        // ── WASAPI loopback capture thread ────────────────────────────────────

        private static void CaptureLoop(int deviceIdx)
        {
            IAudioClient audioClient = null;
            IAudioCaptureClient captureClient = null;
            IntPtr pMixFmt = IntPtr.Zero;

            try
            {
                var enumType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
                if (enumType == null) return;
                var enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(enumType);

                IMMDevice device;
                // Try to get device by index; fall back to default
                IMMDeviceCollection collection;
                if (enumerator.EnumAudioEndpoints(0, 1, out collection) == 0)
                {
                    uint count; collection.GetCount(out count);
                    if (deviceIdx < (int)count)
                        collection.Item((uint)deviceIdx, out device);
                    else
                        enumerator.GetDefaultAudioEndpoint(0, 0, out device);
                    Marshal.ReleaseComObject(collection);
                }
                else
                {
                    enumerator.GetDefaultAudioEndpoint(0, 0, out device);
                }
                Marshal.ReleaseComObject(enumerator);

                var IID_IAudioClient = new Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");
                object audioClientObj;
                if (device.Activate(ref IID_IAudioClient, 1 /*CLSCTX_ALL*/, IntPtr.Zero, out audioClientObj) != 0)
                {
                    Marshal.ReleaseComObject(device);
                    return;
                }
                Marshal.ReleaseComObject(device);
                audioClient = (IAudioClient)audioClientObj;

                // Get mix format
                audioClient.GetMixFormat(out pMixFmt);

                // Read format fields from pointer
                ushort srcChannels      = (ushort)Marshal.ReadInt16(pMixFmt, 2);
                uint   srcSampleRate    = (uint)Marshal.ReadInt32(pMixFmt, 4);
                ushort srcBlockAlign    = (ushort)Marshal.ReadInt16(pMixFmt, 12);
                ushort srcBitsPerSample = (ushort)Marshal.ReadInt16(pMixFmt, 14);
                ushort srcFormatTag     = (ushort)Marshal.ReadInt16(pMixFmt, 0);
                bool   isFloat          = srcFormatTag == 3 || srcFormatTag == 0xFFFE;

                // Initialize for loopback capture using the device's native format
                const uint AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
                int hr = audioClient.Initialize(0 /*SHARED*/, AUDCLNT_STREAMFLAGS_LOOPBACK,
                    2000000 /*200ms*/, 0, pMixFmt, IntPtr.Zero);
                if (hr != 0) return;

                var IID_IAudioCaptureClient = new Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317");
                object captureObj;
                audioClient.GetService(ref IID_IAudioCaptureClient, out captureObj);
                captureClient = (IAudioCaptureClient)captureObj;

                audioClient.Start();

                while (_running)
                {
                    uint packetSize;
                    captureClient.GetNextPacketSize(out packetSize);

                    while (packetSize > 0 && _running)
                    {
                        IntPtr pData;
                        uint numFrames, flags;
                        ulong pos, qpc;
                        captureClient.GetBuffer(out pData, out numFrames, out flags, out pos, out qpc);

                        const uint AUDCLNT_BUFFERFLAGS_SILENT = 0x2;
                        if ((flags & AUDCLNT_BUFFERFLAGS_SILENT) == 0 && numFrames > 0)
                        {
                            int srcBytes = (int)numFrames * srcBlockAlign;
                            var raw = new byte[srcBytes];
                            Marshal.Copy(pData, raw, 0, srcBytes);

                            // Convert to int16 stereo 44100
                            var pcm16 = ConvertToInt16Stereo44100(raw,
                                (int)numFrames, srcChannels, (int)srcSampleRate, srcBitsPerSample, isFloat);

                            lock (_queueLock)
                            {
                                if (_audioQueue.Count < 16)
                                    _audioQueue.Enqueue(pcm16);
                            }
                        }

                        captureClient.ReleaseBuffer(numFrames);
                        captureClient.GetNextPacketSize(out packetSize);
                    }

                    Thread.Sleep(10);
                }

                audioClient.Stop();
            }
            catch { }
            finally
            {
                if (pMixFmt != IntPtr.Zero) CoTaskMemFree(pMixFmt);
                if (captureClient != null) Marshal.ReleaseComObject(captureClient);
                if (audioClient   != null) Marshal.ReleaseComObject(audioClient);
            }
        }

        private static byte[] ConvertToInt16Stereo44100(byte[] src, int numFrames,
            int srcChannels, int srcSampleRate, int srcBitsPerSample, bool srcIsFloat)
        {
            // Convert each source frame to int16 stereo (2ch)
            // Simple nearest-neighbour resample when rates differ

            double ratio = (double)srcSampleRate / OUT_RATE;
            int outFrames = (int)(numFrames / ratio);
            if (outFrames < 1) outFrames = 1;

            var dst = new byte[outFrames * OUT_CHANNELS * 2];
            int di = 0;
            int srcBytesPerSample = srcBitsPerSample / 8;
            int srcBytesPerFrame  = srcChannels * srcBytesPerSample;

            for (int of = 0; of < outFrames; of++)
            {
                int sf = (int)(of * ratio);
                if (sf >= numFrames) sf = numFrames - 1;

                // Extract left and right samples from source frame
                short left = 0, right = 0;
                int srcOff = sf * srcBytesPerFrame;

                if (srcIsFloat && srcBitsPerSample == 32)
                {
                    float fl = BitConverter.ToSingle(src, srcOff);
                    left  = (short)Math.Max(-32768, Math.Min(32767, (int)(fl * 32767f)));
                    if (srcChannels >= 2)
                    {
                        float fr = BitConverter.ToSingle(src, srcOff + 4);
                        right = (short)Math.Max(-32768, Math.Min(32767, (int)(fr * 32767f)));
                    }
                    else right = left;
                }
                else if (srcBitsPerSample == 16)
                {
                    left  = (short)(src[srcOff] | (src[srcOff + 1] << 8));
                    right = srcChannels >= 2 ? (short)(src[srcOff + 2] | (src[srcOff + 3] << 8)) : left;
                }
                else if (srcBitsPerSample == 32 && !srcIsFloat)
                {
                    int il = src[srcOff] | (src[srcOff+1]<<8) | (src[srcOff+2]<<16) | (src[srcOff+3]<<24);
                    left  = (short)(il >> 16);
                    if (srcChannels >= 2) {
                        int ir = src[srcOff+4] | (src[srcOff+5]<<8) | (src[srcOff+6]<<16) | (src[srcOff+7]<<24);
                        right = (short)(ir >> 16);
                    } else right = left;
                }
                else left = right = 0;

                dst[di++] = (byte)(left  & 0xFF); dst[di++] = (byte)((left  >> 8) & 0xFF);
                dst[di++] = (byte)(right & 0xFF); dst[di++] = (byte)((right >> 8) & 0xFF);
            }
            return dst;
        }

        // ── Get audio chunk ───────────────────────────────────────────────────

        private static string GetAudio()
        {
            if (!_running) return "{\"ok\":false,\"reason\":\"not_streaming\"}";

            // Try immediately, then wait one capture interval (10ms) and try once more.
            // Keeping this short prevents thread-pool threads from piling up when called
            // rapidly from the server's stream loop.
            byte[] chunk;
            lock (_queueLock)
                chunk = _audioQueue.Count > 0 ? _audioQueue.Dequeue() : null;
            if (chunk != null)
                return "{\"ok\":true,\"data\":\"" + Convert.ToBase64String(chunk) + "\"}";

            Thread.Sleep(12);

            lock (_queueLock)
                chunk = _audioQueue.Count > 0 ? _audioQueue.Dequeue() : null;
            if (chunk != null)
                return "{\"ok\":true,\"data\":\"" + Convert.ToBase64String(chunk) + "\"}";

            return "{\"ok\":false}";
        }

        // ── WASAPI COM interfaces ─────────────────────────────────────────────

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator {
            int EnumAudioEndpoints(int dataFlow, int dwStateMask,
                [MarshalAs(UnmanagedType.Interface)] out IMMDeviceCollection ppDevices);
            int GetDefaultAudioEndpoint(int dataFlow, int role,
                [MarshalAs(UnmanagedType.Interface)] out IMMDevice ppEndpoint);
            int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId,
                [MarshalAs(UnmanagedType.Interface)] out IMMDevice ppDevice);
            int RegisterEndpointNotificationCallback(IntPtr pNotify);
            int UnregisterEndpointNotificationCallback(IntPtr pNotify);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceCollection {
            int GetCount(out uint pcDevices);
            int Item(uint nDevice, [MarshalAs(UnmanagedType.Interface)] out IMMDevice ppDevice);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice {
            int Activate(ref Guid riid, int dwClsCtx, IntPtr pActivationParams,
                [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
            int OpenPropertyStore(int stgmAccess,
                [MarshalAs(UnmanagedType.Interface)] out IPropertyStore ppProperties);
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
            int GetState(out int pdwState);
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore {
            int GetCount(out uint cProps);
            int GetAt(uint iProp, out PROPERTYKEY pkey);
            int GetValue(ref PROPERTYKEY key, out PropVariant pv);
            int SetValue(ref PROPERTYKEY key, ref PropVariant propvar);
            int Commit();
        }

        [ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioClient {
            int Initialize(int ShareMode, uint StreamFlags, long hnsBufferDuration,
                long hnsPeriodicity, IntPtr pFormat, IntPtr AudioSessionGuid);
            int GetBufferSize(out uint pNumBufferFrames);
            int GetStreamLatency(out long phnsLatency);
            int GetCurrentPadding(out uint pNumPaddingFrames);
            int IsFormatSupported(int ShareMode, IntPtr pFormat, out IntPtr ppClosestMatch);
            int GetMixFormat(out IntPtr ppDeviceFormat);
            int GetDevicePeriod(out long phnsDefaultDevicePeriod, out long phnsMinimumDevicePeriod);
            int Start();
            int Stop();
            int Reset();
            int SetEventHandle(IntPtr eventHandle);
            int GetService(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        }

        [ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioCaptureClient {
            int GetBuffer(out IntPtr ppData, out uint pNumFramesAvailable,
                out uint pdwFlags, out ulong pu64DevicePosition, out ulong pu64QPCPosition);
            int ReleaseBuffer(uint NumFramesRead);
            int GetNextPacketSize(out uint pNumFramesInNextPacket);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PROPERTYKEY { public Guid fmtid; public int pid; }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct PropVariant {
            public ushort vt;
            private ushort r1, r2, r3;
            public IntPtr p;
        }

        [DllImport("ole32.dll")] static extern void PropVariantClear(ref PropVariant pvar);
        [DllImport("ole32.dll")] static extern void CoTaskMemFree(IntPtr pv);

        // ── Helpers ───────────────────────────────────────────────────────────

        private static int ParseInt(string json, string key, int def)
        {
            if (string.IsNullOrEmpty(json)) return def;
            var k   = "\"" + key + "\"";
            int idx = json.IndexOf(k);
            if (idx < 0) return def;
            int c = json.IndexOf(':', idx + k.Length);
            if (c < 0) return def;
            int s = c + 1;
            while (s < json.Length && json[s] == ' ') s++;
            bool neg = s < json.Length && json[s] == '-'; if (neg) s++;
            int e = s; while (e < json.Length && char.IsDigit(json[e])) e++;
            return e == s ? def : int.TryParse(json.Substring(s, e - s), out int v) ? (neg ? -v : v) : def;
        }

        private static string EscJson(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) { if (c == '"') sb.Append("\\\""); else if (c == '\\') sb.Append("\\\\"); else sb.Append(c); }
            return sb.ToString();
        }

        private static string Err(string msg)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in msg) { if (c == '"') sb.Append("\\\""); else if (c == '\\') sb.Append("\\\\"); else sb.Append(c); }
            sb.Append('"');
            return "{\"error\":" + sb + "}";
        }
    }
}
