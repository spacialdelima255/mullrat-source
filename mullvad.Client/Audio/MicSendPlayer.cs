using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

namespace mullvad.Client.Audio
{
    // Plays the server's microphone stream (44100 Hz / 2ch / 16-bit PCM) through
    // the local speakers using winmm waveOut. Buffers rotate in a fixed pool and
    // WHDR_DONE is polled — no window and no callback required.
    internal sealed class MicSendPlayer : IDisposable
    {
        private const int SampleRate  = 44100;
        private const int Channels    = 2;
        private const int BitsPerSmp  = 16;
        private const int BufferCount = 8;
        private const int BufferSize  = 44100 * 4 / 8; // ~125 ms per buffer

        private IntPtr _hWaveOut;
        private readonly ConcurrentQueue<byte[]> _queue = new ConcurrentQueue<byte[]>();
        private Thread _thread;
        private volatile bool _playing;
        private GCHandle[]   _pins;
        private byte[][]     _bufs;
        private WAVHDR[]     _hdrs;

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEFORMATEX
        {
            public short wFormatTag, nChannels;
            public uint nSamplesPerSec, nAvgBytesPerSec;
            public short nBlockAlign, wBitsPerSample, cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVHDR
        {
            public IntPtr lpData;
            public uint dwBufferLength, dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags, dwLoops;
            public IntPtr lpNext, reserved;
        }

        const uint WAVE_MAPPER   = 0xFFFFFFFF;
        const int  WHDR_DONE     = 0x00000001;
        const uint MM_WOM_CLOSE  = 0x3C;
        const uint CALLBACK_NULL = 0;

        [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr h, uint uDeviceID, ref WAVEFORMATEX lpFormat, IntPtr dwCallback, IntPtr dwInstance, uint fdwOpen);
        [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr h, ref WAVHDR ph, uint size);
        [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr h, ref WAVHDR ph, uint size);
        [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr h, ref WAVHDR ph, uint size);
        [DllImport("winmm.dll")] static extern int waveOutGetErrorText(int err, System.Text.StringBuilder buf, int size);

        public string LastError { get; private set; } = "";

        public bool Start()
        {
            if (_playing) return true;

            var fmt = new WAVEFORMATEX
            {
                wFormatTag     = 1, // PCM
                nChannels      = Channels,
                nSamplesPerSec = SampleRate,
                wBitsPerSample = BitsPerSmp,
                nBlockAlign    = (short)(Channels * BitsPerSmp / 8),
                nAvgBytesPerSec = (uint)(SampleRate * Channels * BitsPerSmp / 8),
                cbSize         = 0,
            };

            int err = waveOutOpen(out _hWaveOut, WAVE_MAPPER, ref fmt, IntPtr.Zero, IntPtr.Zero, CALLBACK_NULL);
            if (err != 0)
            {
                var sb = new System.Text.StringBuilder(256);
                waveOutGetErrorText(err, sb, 256);
                LastError = sb.ToString();
                return false;
            }

            _pins = new GCHandle[BufferCount];
            _bufs = new byte[BufferCount][];
            _hdrs = new WAVHDR[BufferCount];
            for (int i = 0; i < BufferCount; i++)
            {
                _bufs[i] = new byte[BufferSize];
                _pins[i] = GCHandle.Alloc(_bufs[i], GCHandleType.Pinned);
                _hdrs[i].dwBufferLength = 0;
            }

            _playing = true;
            _thread  = new Thread(PlayLoop) { IsBackground = true, Name = "MicSendPlayer" };
            _thread.Start();
            return true;
        }

        public void Enqueue(byte[] data)
        {
            if (_playing && data != null && data.Length > 0)
                _queue.Enqueue(data);
        }

        public void Stop()
        {
            _playing = false;
            _thread?.Join(2000);
            _thread = null;

            if (_hWaveOut != IntPtr.Zero)
            {
                waveOutReset(_hWaveOut);
                if (_hdrs != null)
                {
                    for (int i = 0; i < BufferCount; i++)
                    {
                        try { waveOutUnprepareHeader(_hWaveOut, ref _hdrs[i], (uint)Marshal.SizeOf<WAVHDR>()); } catch { }
                        if (_pins[i].IsAllocated) _pins[i].Free();
                    }
                }
                waveOutClose(_hWaveOut);
                _hWaveOut = IntPtr.Zero;
            }

            while (_queue.TryDequeue(out _)) { }
        }

        private void PlayLoop()
        {
            uint whdrSize = (uint)Marshal.SizeOf(typeof(WAVHDR));
            int next = 0;

            // Prime: wait until we have a few buffers queued to reduce stutter
            while (_playing && _queue.Count < 4) Thread.Sleep(15);

            while (_playing)
            {
                byte[] chunk;
                if (!_queue.TryDequeue(out chunk) || chunk == null)
                {
                    Thread.Sleep(15);
                    continue;
                }

                // Wait for the next buffer slot to finish playing
                while (_playing && (_hdrs[next].dwFlags & WHDR_DONE) == 0)
                    Thread.Sleep(10);
                if (!_playing) break;

                // Unprepare previous write (safe even if never written)
                try { waveOutUnprepareHeader(_hWaveOut, ref _hdrs[next], whdrSize); } catch { }

                int len = Math.Min(chunk.Length, BufferSize);
                Buffer.BlockCopy(chunk, 0, _bufs[next], 0, len);

                var hdr = _hdrs[next];
                hdr.lpData        = _pins[next].AddrOfPinnedObject();
                hdr.dwBufferLength = (uint)len;
                hdr.dwFlags       = 0;
                _hdrs[next] = hdr;

                if (waveOutPrepareHeader(_hWaveOut, ref _hdrs[next], whdrSize) == 0)
                    waveOutWrite(_hWaveOut, ref _hdrs[next], whdrSize);

                next = (next + 1) % BufferCount;
            }
        }

        public void Dispose()
        {
            Stop();
            while (_queue.TryDequeue(out _)) { }
        }
    }
}
