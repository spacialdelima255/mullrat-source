using System.Threading.Channels;

namespace mullvad.Network
{
    // Bidirectional in-memory byte stream backed by two Channel<byte[]>.
    // Used to give virtual ClientHandlers a pipe they can read/write over.
    internal sealed class DuplexChannelStream : Stream
    {
        private readonly Channel<byte[]> _readCh;   // data arrives here for the reader
        private readonly Channel<byte[]> _writeCh;  // data written here by the writer

        private Memory<byte> _readRemainder;

        internal DuplexChannelStream(Channel<byte[]> readCh, Channel<byte[]> writeCh)
        {
            _readCh  = readCh;
            _writeCh = writeCh;
        }

        public override bool CanRead  => true;
        public override bool CanWrite => true;
        public override bool CanSeek  => false;

        // ── Read ──────────────────────────────────────────────────────────────

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            while (_readRemainder.IsEmpty)
            {
                if (!await _readCh.Reader.WaitToReadAsync(ct)) return 0; // channel closed = EOF
                if (_readCh.Reader.TryRead(out var chunk))
                    _readRemainder = chunk.AsMemory();
            }

            int take = Math.Min(count, _readRemainder.Length);
            _readRemainder.Span[..take].CopyTo(buffer.AsSpan(offset, take));
            _readRemainder = _readRemainder[take..];
            return take;
        }

        // ── Write ─────────────────────────────────────────────────────────────

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            var data = new byte[count];
            Buffer.BlockCopy(buffer, offset, data, 0, count);
            await _writeCh.Writer.WriteAsync(data, ct);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        {
            var data = buffer.ToArray();
            return _writeCh.Writer.WriteAsync(data, ct);
        }

        // ── Unsupported ───────────────────────────────────────────────────────

        public override void Flush() { }
        public override int  Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Length   => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        // ── Dispose ───────────────────────────────────────────────────────────

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _readCh.Writer.TryComplete();
                _writeCh.Writer.TryComplete();
            }
            base.Dispose(disposing);
        }
    }

    // Creates a connected pair of DuplexChannelStreams.
    // HandlerStream  → given to ClientHandler (reads incoming, writes outgoing)
    // RelayStream    → held by VpsOperatorClient (writes incoming, reads outgoing)
    internal sealed class VirtualClientPipe
    {
        private readonly Channel<byte[]> _vpsToHandler;    // relay writes, handler reads
        private readonly Channel<byte[]> _handlerToRelay;  // handler writes, relay reads

        public DuplexChannelStream HandlerStream { get; }
        public DuplexChannelStream RelayStream   { get; }

        public VirtualClientPipe()
        {
            _vpsToHandler   = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });
            _handlerToRelay = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleWriter = true,  SingleReader = true });

            HandlerStream = new DuplexChannelStream(_vpsToHandler,   _handlerToRelay);
            RelayStream   = new DuplexChannelStream(_handlerToRelay, _vpsToHandler);
        }

        // Push a raw serialized packet (wire bytes) into the handler's read side.
        public void FeedInbound(byte[] packetBytes) => _vpsToHandler.Writer.TryWrite(packetBytes);

        // Closes both channels — causes the handler's RunAsync to exit cleanly.
        public void Complete()
        {
            _vpsToHandler.Writer.TryComplete();
            _handlerToRelay.Writer.TryComplete();
        }
    }
}
