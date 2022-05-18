using System;
using System.IO;
using Nocturo.Common.Utilities;
using Nocturo.Downloader.Enums;
using Nocturo.Downloader.Models;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Nocturo.Common.Exceptions.Common;
using Nocturo.Downloader.Services;

namespace Nocturo.Downloader.Streams
{
    internal abstract class ChunkStreamBase : Stream
    {
        protected const int Timeout = 15000;

        protected byte[] SharedBuffer = new byte[1024 << 10];

        protected Chunk CurrentChunk;

        protected readonly InstallationService State;

        private readonly List<Chunk> _chunks;

        private readonly ChunkDownloader _downloader;

        private readonly ConcurrentQueue<BinaryReader> _chunksStream;

        private bool _disposed;

        private int _chunkIndex;

        private long _inPos;

        protected ChunkStreamBase(List<Chunk> chunks, InstallationService state)
        {
            _chunks = chunks;
            _chunksStream = new();
            State = state;
            _downloader = new(chunks, state);
            _downloader.OnChunkDownloaded += (_, s) =>
            {
                _chunksStream.Enqueue(s);
            };

            Logger.LogInfo("ChunkStream", $"Chunk Downloader has started with {_chunks.Count} chunk(s) to download");
            // we don't need to await here. We keep adding the chunk streams in the queue WHILE parsing the available ones in parallel
            _ = _downloader.StartDownload();
        }

        /// <summary>
        /// Gets whether the overriden stream support chunk caching.
        /// </summary>
        public abstract bool HasChunkCacheSupport { get; }

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => _chunks.Count;

        /// <inheritdoc />
        public override long Position
        {
            get => _chunkIndex;
            set => throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override void Flush()
            => throw new NotImplementedException();

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
        {
            var newChunk = CurrentChunk == null || _inPos == CurrentChunk?.Size;

            if (CurrentChunk != null && newChunk)
            {
                _chunkIndex++;
                _inPos = 0;
                State.ChunksInstalled++;
                //State.FireChunkInstalledEvent((int)CurrentChunk.Size);
            }

            if (_chunkIndex == _chunks.Count)
                return 0;

            CurrentChunk = _chunks[_chunkIndex];

            if (newChunk)
                LoadChunk();

            unsafe
            {
                fixed (byte* pBuffer = buffer)
                {
                    fixed (byte* pShared = SharedBuffer)
                    {
                        // after doing some benchmarking, it turns out that MemoryCopy is the fastest among the available methods (Array.Copy, Buffer.BlockCopy, Marshal.Copy and Buffer.MemoryCopy)
                        var availableSize = CurrentChunk.Size - _inPos;
                        var inCount = availableSize >= count ? count : availableSize;
                        Buffer.MemoryCopy(pShared + _inPos, pBuffer, buffer.Length, inCount);
                        _inPos += inCount;
                        return (int)inCount;
                    }
                }
            }
        }

        protected BinaryReader DequeueWithTimeout()
        {
            // there's no way it will overflow unless someone has left his pc on for more than 292471209 years
            var startTime = Environment.TickCount64;
            BinaryReader reader;
            while (!_chunksStream.TryDequeue(out reader))
            {
                // this is not a very accurate timeout but if it works it works xd
                if (Environment.TickCount64 - startTime < Timeout)
                    continue;

                Logger.LogWarning("ChunkStream", "Stream has timeout therefore has been aborted so was the file installation. If the underlying stream is CachedChunkStream, the download will be aborted too");
                throw new TimeoutException("Chunk couldn't be downloaded");
            }

            return reader;
        }

        protected static void AssertChunkValidity(long streamLength, in FChunkHeaderMinimal chunkHeader)
        {
            var headerSize = chunkHeader.HeaderSize;
            long fileSize = headerSize + chunkHeader.DataSizeCompressed;
            if (fileSize > streamLength)
               new ChunkLoadException($"The stream length {streamLength} doesn't match the expected file size {fileSize}",
                                             headerSize).LogErrorBeforeThrowing("ChunkStream");

            if ((chunkHeader.StoredAs & EChunkStorageFlags.Encrypted) == EChunkStorageFlags.None)
                return;

            new ChunkLoadException($"The storage type '{EChunkStorageFlags.Encrypted}' of the chunk is not supported.", headerSize).LogErrorBeforeThrowing("ChunkStream");
        }

        protected abstract void LoadChunk();

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                SharedBuffer = null;
                _downloader.Dispose();
                base.Dispose(true);
            }

            _disposed = true;
        }
    }
}