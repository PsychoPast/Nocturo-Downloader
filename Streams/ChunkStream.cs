using System;
using Ionic.Zlib;
using Nocturo.Common.Utilities;
using Nocturo.Downloader.Enums;
using Nocturo.Downloader.Models;
using System.Collections.Generic;
using Nocturo.Downloader.Services;

namespace Nocturo.Downloader.Streams
{
    internal sealed class ChunkStream :ChunkStreamBase
    {
        public ChunkStream(List<Chunk> chunks, InstallationService state)
            : base(chunks, state)
                 => Logger.LogInfo("ChunkStream", "Using non-cacheable stream, chunks order isn't necessary and download won't be aborted if a file fails to install");

        public override bool HasChunkCacheSupport => false;

        protected override void LoadChunk()
        {
            var guid = CurrentChunk.Guid;
            var size = (int)CurrentChunk.Size;
            Logger.LogInfo("ChunkStream", $"Loading chunk '{guid}' with hash {CurrentChunk.Hash} and size {size}");
            Logger.LogInfo("ChunkStream", $"Attempting to dequeue the reader for chunk '{guid}'. Timeout: {Timeout} ms");
            var reader = DequeueWithTimeout();
            Logger.LogInfo("ChunkStream", $"Successfully dequeued the reader for chunk '{guid}'");
            FChunkHeaderMinimal chunkHeader = new(reader);
            Logger.LogInfo("ChunkStream", $"Parsed header, validating chunk '{guid}'");
            AssertChunkValidity(reader.BaseStream.Length, in chunkHeader);
            var junkSize = CurrentChunk.Offset;
            var compressed = (chunkHeader.StoredAs & EChunkStorageFlags.Compressed) != EChunkStorageFlags.None;
            if (compressed)
            {
                Logger.LogInfo("ChunkStream", $"Chunk '{guid}' is zlib compressed");
                Span<byte> junk = new(new byte[junkSize]);
                using ZlibStream zlibStream = new(reader.BaseStream, CompressionMode.Decompress);
                {
                    // Seeking into the stream would break the decompression algorithm. We are forced to read the unwanted data
                    zlibStream.Read(junk);
                    zlibStream.Read(SharedBuffer, 0, size);
                }
            }
            else
            {
                Logger.LogInfo("ChunkStream", $"Chunk '{guid}' is not zlib compressed");
                reader.BaseStream.Position += junkSize;
                reader.Read(SharedBuffer, 0, size);
            }

            reader.BaseStream.Dispose();
            reader.Dispose();
        }
    }
}