using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using mullvad.Network;
using mullvad.Protocol;
using mullvad.Theme;

namespace mullvad.Forms
{
    // Streams the server operator's microphone to a client so they hear it
    // locally.  Capture is WASAPI (default capture device), converted to
    // 44100 Hz / stereo / 16-bit PCM and shipped as MicSendData packets.
    internal sealed class MicrophoneSendForm : Form
    {
        private readonly ClientHandler _handler;

        private ToolStrip        _toolbar   = null!;
        private ToolStripButton  _btnStart  = null!;
        private ToolStripButton  _btnStop   = null!;
        private ToolStripComboBox _cmbDevice = null!;
        private ToolStripStatusLabel _lblStatus = null!;

        private MicSendCapture? _capture;

        public MicrophoneSendForm(ClientHandler handler)
        {
            _handler = handler;
            Build();

            handler.Disconnected += OnClientDisconnected;
            FormClosed           += (_, _) => { handler.Disconnected -= OnClientDisconnected; StopCapture(); };
        }

        private void Build()
        {
            SuspendLayout();

            Text          = "Remote Microphone Send  —  " + _handler.Info.Computer;
            ClientSize    = new Size(560, 220);
            StartPosition = FormStartPosition.CenterParent;
            BackColor     = ThemeManager.ControlColor;
            ForeColor     = ThemeManager.ForeColor;

            var iconBmp = IconLoader.Load("transmit_blue.png") as Bitmap;
            if (iconBmp is not null) try { Icon = Icon.FromHandle(iconBmp.GetHicon()); } catch { }

            _toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };

            _btnStart  = new ToolStripButton("Start")         { DisplayStyle = ToolStripItemDisplayStyle.Text };
            _btnStop   = new ToolStripButton("Stop")         { DisplayStyle = ToolStripItemDisplayStyle.Text, Enabled = false };
            _cmbDevice = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };

            _btnStart.Click += BtnStart_Click;
            _btnStop.Click  += (_, _) => StopCapture();

            _toolbar.Items.AddRange(new ToolStripItem[]
            {
                _btnStart, _btnStop, new ToolStripSeparator(),
                new ToolStripLabel("Microphone:"), _cmbDevice,
            });

            _lblStatus = new ToolStripStatusLabel("Idle — press Start to talk.") { Spring = true };

            var status = new StatusStrip();
            status.Items.Add(_lblStatus);

            var lblInfo = new Label
            {
                Text = "Streams your microphone to the client's speakers.\nThey will hear everything you say until you press Stop.",
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Segoe UI", 9F),
            };

            Controls.Add(lblInfo);
            Controls.Add(_toolbar);
            Controls.Add(status);

            Load  += (_, _) => _ = EnumerateDevicesAsync();
            ResumeLayout(false);
        }

        private async Task EnumerateDevicesAsync()
        {
            _cmbDevice.ComboBox?.Items.Clear();
            var names = await Task.Run(() => MicSendCapture.ListDevices());
            foreach (var n in names)
                _cmbDevice.ComboBox?.Items.Add(n);
            if ((_cmbDevice.ComboBox?.Items.Count ?? 0) > 0) _cmbDevice.SelectedIndex = 0;
            else _cmbDevice.ComboBox?.Items.Add("Default Microphone");
            _cmbDevice.SelectedIndex = 0;
        }

        private async void BtnStart_Click(object? sender, EventArgs e)
        {
            if (_capture != null) return;

            int device = Math.Max(_cmbDevice.SelectedIndex, 0);

            _capture = new MicSendCapture(device, chunk =>
            {
                try
                {
                    _ = _handler.SendAsync(Packet.Create(PacketType.MicSendData, new
                    {
                        data = Convert.ToBase64String(chunk),
                    }));
                }
                catch { }
            }, err => BeginInvoke(() => _lblStatus.Text = "Error: " + err));

            if (!_capture.Start())
            {
                _lblStatus.Text = "Error: " + MicSendCapture.LastStartError;
                _capture = null;
                return;
            }

            // Tell the client to open its speakers
            await _handler.SendAsync(Packet.Create(PacketType.MicSendStart));
            _lblStatus.Text = $"Sending mic to {_handler.Info.Computer}…";
            _btnStart.Enabled = false;
            _btnStop.Enabled  = true;
        }

        private void StopCapture()
        {
            if (_capture == null) { _btnStart.Enabled = true; _btnStop.Enabled = false; return; }

            try
            {
                _capture.Stop();
                _ = _handler.SendAsync(Packet.Create(PacketType.MicSendStop));
            }
            catch { }
            _capture = null;

            _lblStatus.Text   = "Idle — press Start to talk.";
            _btnStart.Enabled = true;
            _btnStop.Enabled  = false;
        }

        private void OnClientDisconnected(ClientHandler _)
        {
            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                StopCapture();
                _lblStatus.Text = "Client disconnected.";
                _btnStart.Enabled = false;
            });
        }
    }

    // WASAPI microphone capture → 44100/2ch/16-bit PCM chunks (same wire format
    // as the client's own mic module so MicSendPlayer can play it back).
    internal sealed class MicSendCapture
    {
        private const int OUT_SAMPLE_RATE = 44100;
        private const int OUT_CHANNELS    = 2;
        private const int OUT_BITS        = 16;

        private static string _lastStartError = "";
        public static string LastStartError => _lastStartError;

        private readonly int _deviceIndex;
        private readonly Action<byte[]> _onChunk;
        private readonly Action<string> _onError;

        private Thread? _thread;
        private volatile bool _running;

        public MicSendCapture(int deviceIndex, Action<byte[]> onChunk, Action<string> onError)
        {
            _deviceIndex = deviceIndex;
            _onChunk     = onChunk;
            _onError     = onError;
        }

        public bool Start()
        {
            _running = true;
            _lastStartError = "";
            _thread = new Thread(CaptureLoop) { IsBackground = true, Name = "MicSendCapture" };
            _thread.Start();
            Thread.Sleep(300); // give the loop a moment to fail fast on missing mic
            if (!_running && _lastStartError.Length > 0)
                return false;
            return true;
        }

        public void Stop() => _running = false;

        public static List<string> ListDevices()
        {
            var names = new List<string>();
            try
            {
                var devEnumType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
                if (devEnumType != null)
                {
                    var devEnum = (IMMDeviceEnumerator)Activator.CreateInstance(devEnumType);
                    devEnum.EnumAudioEndpoints(1, 1, out IMMDeviceCollection col); // eCapture, ACTIVE
                    col.GetCount(out uint count);
                    for (uint i = 0; i < count; i++)
                    {
                        col.Item(i, out IMMDevice dev);
                        names.Add(GetDeviceName(dev));
                        Marshal.ReleaseComObject(dev);
                    }
                    Marshal.ReleaseComObject(col);
                    Marshal.ReleaseComObject(devEnum);
                }
            }
            catch { }
            if (names.Count == 0) names.Add("Default Microphone");
            return names;
        }

        private static string GetDeviceName(IMMDevice dev)
        {
            try
            {
                dev.OpenPropertyStore(0, out IPropertyStore store);
                var key = new PROPERTYKEY { fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), pid = 14 };
                store.GetValue(ref key, out PROPVARIANT pv);
                string name = pv.vt == 31 ? Marshal.PtrToStringUni(pv.p) ?? "Microphone" : "Microphone";
                PropVariantClear(ref pv);
                Marshal.ReleaseComObject(store);
                return name;
            }
            catch { return "Microphone"; }
        }

        private void CaptureLoop()
        {
            try
            {
                var devEnumType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
                if (devEnumType == null) throw new Exception("WASAPI unavailable");
                var devEnum = (IMMDeviceEnumerator)Activator.CreateInstance(devEnumType);

                IMMDevice device;
                try
                {
                    devEnum.EnumAudioEndpoints(1, 1, out IMMDeviceCollection col);
                    col.GetCount(out uint count);
                    uint idx = (uint)_deviceIndex;
                    if (idx < count) col.Item(idx, out device);
                    else             col.Item(0, out device);
                    Marshal.ReleaseComObject(col);
                }
                catch
                {
                    devEnum.GetDefaultAudioEndpoint(1, 0, out device); // eCapture, eCommunications
                }
                Marshal.ReleaseComObject(devEnum);

                device.Activate(new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), 0, IntPtr.Zero, out IAudioClient client);
                Marshal.ReleaseComObject(device);

                client.GetMixFormat(out IntPtr pFmt);
                client.Initialize(0 /*SHARED*/, 0, 2000000 /*200ms*/, 0, pFmt, Guid.Empty);

                client.GetService(new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), out IAudioCaptureClient captureClient);
                client.Start();

                var wfx      = (WAVEFORMATEX)Marshal.PtrToStructure(pFmt, typeof(WAVEFORMATEX))!;
                int srcRate  = (int)wfx.nSamplesPerSec;
                int srcCh    = (int)wfx.nChannels;
                short srcFmt = wfx.wFormatTag;

                if (srcFmt == unchecked((short)0xFFFE))
                {
                    var ext = (WAVEFORMATEXTENSIBLE)Marshal.PtrToStructure(pFmt, typeof(WAVEFORMATEXTENSIBLE))!;
                    byte[] sub = ext.SubFormat.ToByteArray();
                    srcFmt = (short)(sub[0] | (sub[1] << 8));
                }
                CoTaskMemFree(pFmt);

                while (_running)
                {
                    captureClient.GetNextPacketSize(out uint packetSize);
                    var raw = new List<byte>(4096);

                    while (packetSize > 0 && _running)
                    {
                        captureClient.GetBuffer(out IntPtr dataPtr, out uint frames, out uint flags, out ulong _, out ulong _);
                        int byteCount = (int)frames * srcCh * (wfx.wBitsPerSample / 8);
                        if ((flags & 2) == 0 && byteCount > 0) // not silent
                        {
                            byte[] buf = new byte[byteCount];
                            Marshal.Copy(dataPtr, buf, 0, byteCount);
                            raw.AddRange(buf);
                        }
                        captureClient.ReleaseBuffer(frames);
                        captureClient.GetNextPacketSize(out packetSize);
                    }

                    if (raw.Count > 0)
                    {
                        byte[] pcm = ConvertToInt16Stereo44100(raw.ToArray(), srcFmt, srcRate, srcCh, wfx.wBitsPerSample);
                        if (pcm.Length > 0)
                        {
                            try { _onChunk(pcm); }
                            catch (Exception ex) { _onError?.Invoke(ex.Message); }
                        }
                    }

                    Thread.Sleep(25);
                }

                client.Stop();
                Marshal.ReleaseComObject(captureClient);
                Marshal.ReleaseComObject(client);
            }
            catch (Exception ex)
            {
                _lastStartError = ex.Message;
                _running = false;
                _onError?.Invoke(ex.Message);
            }
        }

        private static byte[] ConvertToInt16Stereo44100(byte[] src, short fmt, int srcRate, int srcCh, short srcBits)
        {
            int bytesPerSample = srcBits / 8;
            int srcFrames      = src.Length / (srcCh * bytesPerSample);
            if (srcFrames == 0) return Array.Empty<byte>();

            double ratio  = (double)OUT_SAMPLE_RATE / srcRate;
            int outFrames = (int)(srcFrames * ratio) + 1;
            var dst       = new byte[outFrames * OUT_CHANNELS * 2];
            int written   = 0;

            for (int i = 0; i < outFrames; i++)
            {
                double srcPos = i / ratio;
                int s0        = (int)srcPos;
                if (s0 >= srcFrames) break;

                int offset0 = s0 * srcCh * bytesPerSample;
                short left  = ReadSample(src, offset0, fmt, srcBits);
                short right = srcCh > 1 ? ReadSample(src, offset0 + bytesPerSample, fmt, srcBits) : left;

                dst[written++] = (byte)(left  & 0xFF);
                dst[written++] = (byte)(left  >> 8);
                dst[written++] = (byte)(right & 0xFF);
                dst[written++] = (byte)(right >> 8);
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

        // ── WASAPI COM interfaces (same pattern as the mic client module) ────

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint dwStateMask, out IMMDeviceCollection ppDevices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IMMDeviceCollection
        {
            [PreserveSig] int GetCount(out uint pcDevices);
            [PreserveSig] int Item(uint nDevice, out IMMDevice ppDevice);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IMMDevice
        {
            [PreserveSig] int Activate(Guid iid, uint dwClsCtx, IntPtr pActivationParams, out IAudioClient ppInterface);
            [PreserveSig] int OpenPropertyStore(uint stgmAccess, out IPropertyStore ppProperties);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
            [PreserveSig] int GetState(out uint pdwState);
        }

        [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioClient
        {
            [PreserveSig] int Initialize(int ShareMode, uint StreamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr pFormat, Guid AudioSessionGuid);
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
            [PreserveSig] int GetService(Guid riid, out IAudioCaptureClient ppv);
        }

        [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioCaptureClient
        {
            [PreserveSig] int GetBuffer(out IntPtr ppData, out uint pNumFramesAvailable, out uint pdwFlags, out ulong pu64DevicePosition, out ulong pu64QPCPosition);
            [PreserveSig] int ReleaseBuffer(uint NumFramesRead);
            [PreserveSig] int GetNextPacketSize(out uint pNumFramesInNextPacket);
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IPropertyStore
        {
            [PreserveSig] int GetCount(out uint cProps);
            [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
            [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROPERTYKEY { public Guid fmtid; public uint pid; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROPVARIANT { public short vt; public short r1, r2, r3; public IntPtr p; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WAVEFORMATEX
        {
            public short wFormatTag; public short nChannels;
            public uint nSamplesPerSec, nAvgBytesPerSec;
            public short nBlockAlign, wBitsPerSample, cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WAVEFORMATEXTENSIBLE
        {
            public short wFormatTag, nChannels; public uint nSamplesPerSec, nAvgBytesPerSec;
            public short nBlockAlign, wBitsPerSample, cbSize, wValidBitsPerSample;
            public uint dwChannelMask; public Guid SubFormat;
        }

        [DllImport("ole32.dll")] private static extern void PropVariantClear(ref PROPVARIANT pvar);
        [DllImport("ole32.dll")] private static extern void CoTaskMemFree(IntPtr ptr);
    }
}
