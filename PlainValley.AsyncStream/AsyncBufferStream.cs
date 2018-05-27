using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlainValley.AsyncStream
{
    public class AsyncBufferStream : Stream
    {
        private bool _disposeBufferStream;
        private long _readPosition;
        private BufferSourceStream _bufferStream;

        public AsyncBufferStream(BufferSourceStream bufferStream, bool disposeBufferStream = true)
        {
            _bufferStream = bufferStream;

            // In most use cases there will be only one AsyncBufferStream for
            // BufferSourceStream instance, so we dispose BufferSourceStream by default.
            _disposeBufferStream = disposeBufferStream;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Position
        {
            get => _readPosition;
            set => throw new NotSupportedException("Syncronous Position property is not available, use SetPosition instead");
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    _readPosition = offset;
                    break;
                case SeekOrigin.Current:
                    _readPosition += offset;
                    break;
                case SeekOrigin.End:
                    throw new NotSupportedException();
            }

            return _readPosition;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) 
            => _bufferStream.ReadAsync(buffer, offset, count, cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Use asyncronous version instead.");
        public override long Length => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (_disposeBufferStream)
            {
                _bufferStream.Dispose();
            }
        }
    }
}
