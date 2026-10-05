namespace Server.Api.Diagnostics
{
    internal sealed class ResponseCaptureStream : Stream
    {
        private readonly Stream _inner;
        private readonly MemoryStream _captured = new();
        private readonly int _limit;

        public ResponseCaptureStream(Stream inner, int limit)
        {
            _inner = inner;
            _limit = limit;
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public string ReadCaptured()
        {
            return System.Text.Encoding.UTF8.GetString(_captured.GetBuffer(), 0, (int)_captured.Length);
        }

        public override void Flush()
        {
            _inner.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _inner.FlushAsync(cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Capture(new ReadOnlySpan<byte>(buffer, offset, count));
            _inner.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Capture(new ReadOnlySpan<byte>(buffer, offset, count));

            return _inner.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Capture(buffer.Span);

            return _inner.WriteAsync(buffer, cancellationToken);
        }

        private void Capture(ReadOnlySpan<byte> data)
        {
            var free = _limit - (int)_captured.Length;

            if (free <= 0)
                return;

            _captured.Write(free < data.Length ? data.Slice(0, free) : data);
        }
    }
}
