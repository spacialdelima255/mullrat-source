using System;
using System.Threading;

namespace mullvad.Utilities
{
    /// <summary>
    /// Fixed-capacity, thread-safe ring buffer of bytes.
    /// Write wraps around; when full, the oldest bytes are overwritten.
    /// </summary>
    public sealed class CircularBuffer
    {
        private readonly byte[]  _buf;
        private readonly int     _cap;
        private int  _head;
        private int  _tail;
        private int  _count;
        private readonly object  _lock = new object();

        public int Capacity => _cap;
        public int Count    { get { lock (_lock) return _count; } }

        public CircularBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _cap = capacity;
            _buf = new byte[capacity];
        }

        public int Write(byte[] data, int offset, int count)
        {
            lock (_lock)
            {
                int written = 0;
                for (int i = 0; i < count; i++)
                {
                    _buf[_tail] = data[offset + i];
                    _tail = (_tail + 1) % _cap;
                    if (_count == _cap)
                        _head = (_head + 1) % _cap; // overwrite oldest
                    else
                        _count++;
                    written++;
                }
                return written;
            }
        }

        public int Read(byte[] destination, int offset, int count)
        {
            lock (_lock)
            {
                int toRead = Math.Min(count, _count);
                for (int i = 0; i < toRead; i++)
                {
                    destination[offset + i] = _buf[_head];
                    _head = (_head + 1) % _cap;
                }
                _count -= toRead;
                return toRead;
            }
        }

        public void Clear()
        {
            lock (_lock) { _head = _tail = _count = 0; }
        }
    }
}
