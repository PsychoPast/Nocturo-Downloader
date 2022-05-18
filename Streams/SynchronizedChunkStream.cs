using System;
using System.IO;
using Ionic.Zlib;
using System.Linq;
using System.Threading;
using Nocturo.Common.Utilities;

namespace Nocturo.Downloader.Streams
{
    internal class SynchronizedChunkStream : Stream
    {
        private readonly Stream _inStream;

        private readonly bool _isZlib;

        private readonly long _length;

        private readonly ConcurrentSortedDictionary<long, int> _waitingList;

        private readonly ZlibStream _zlStream;

        internal SynchronizedChunkStream(Stream stream, long chunkTotalSize)
        {
            _inStream = stream ?? throw new ArgumentNullException(nameof(stream));

            if (chunkTotalSize <= 0)
                throw new ArgumentException("chunk size cannot be null or negative", nameof(chunkTotalSize));

            // if the stream is ZlibStream, it's better to store a ref to it than cast _inStream everytime
            if (stream is ZlibStream zlib)
            {
                _zlStream = zlib;
                _isZlib = true;
            }

            // chunkTotalSize is the decompressed size of the chunk
            _length = chunkTotalSize;
            _waitingList = new ConcurrentSortedDictionary<long, int>();
        }

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        public bool HasReachedEnd => Position == _length;

        /// <inheritdoc />
        public override long Length => _length;

        /// <inheritdoc />
        public override long Position
        {
            get
            {
                lock (_inStream)
                    return _isZlib ? _zlStream.TotalOut : _inStream.Position;
            }

            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Flush() 
            => throw new NotImplementedException();

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) 
            => throw new NotImplementedException();

        public int Read(byte[] buffer, int chunkSize, long chunkOffset)
        {
            if (chunkSize <= 0)
                new ArgumentException($"Requested size {chunkSize} is null or negative", nameof(chunkSize)).LogErrorBeforeThrowing("ChunkStream");
            

            if (chunkSize > _length)
                new ArgumentException($"Requested size {chunkSize} is bigger than stream length {_length}", nameof(chunkSize)).LogErrorBeforeThrowing("ChunkStream");
            

            if (chunkOffset < Position)
                new ArgumentException($"Tried to read stream for chunk with offset {chunkOffset} when the current read position {Position} is bigger than it", nameof(chunkOffset)).LogErrorBeforeThrowing("ChunkStream");

            // most of the time, this will be the case
            if (chunkOffset == Position)
            {
                lock (_inStream)
                    return _inStream.Read(buffer, 0, chunkSize);
            }

            // if not (it's not an issue, it means one thread was faster than the other one, we wait)
            _waitingList.Add(chunkOffset, chunkSize);
            return WaitRead(buffer, chunkOffset);
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) 
            => throw new NotImplementedException();

        /// <inheritdoc />
        public override void SetLength(long value) 
            => throw new NotImplementedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) 
            => throw new NotImplementedException();

        private int WaitRead(byte[] buffer, long chunkOffset)
        {
            // we wait until the first element (chunk offset) in the waiting list matches the current Position
            long currentOffset;
            while ((currentOffset = _waitingList.Keys.First()) != Position && currentOffset != chunkOffset)
                Thread.SpinWait(1);

            _waitingList.Remove(currentOffset);
            return _inStream.Read(buffer, 0, _waitingList[currentOffset]);
        }
    }
}