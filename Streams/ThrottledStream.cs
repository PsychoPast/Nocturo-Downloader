using System;
using System.IO;

namespace Nocturo.Downloader.Streams
{
    public class ThrottledStream : Stream
    {
        private readonly Stream _inStream;

        private readonly uint _maxBps;

        public ThrottledStream(Stream inStream, uint maxBps)
        {
            _inStream = inStream;
            _maxBps = maxBps;
        }

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => _inStream.Length;

        /// <inheritdoc />
        public override long Position
        {
            get => _inStream.Position;
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Flush() 
            => throw new NotImplementedException();

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotImplementedException();

        /// <inheritdoc />
        public override void SetLength(long value)
            => throw new NotImplementedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotImplementedException();

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotImplementedException();
    }
}