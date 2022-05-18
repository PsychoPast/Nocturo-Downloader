using Ionic.Zlib;
using Nocturo.Common.Utilities;
using Nocturo.Downloader.Enums;
using Nocturo.Downloader.Models;
using System.Collections.Generic;
using Nocturo.Downloader.Services;

namespace Nocturo.Downloader.Streams
{
    internal sealed class CachedChunkStream : ChunkStreamBase
    {
        public CachedChunkStream(List<Chunk> chunks, InstallationService state)
            : base(chunks, state) 
            => Logger.LogInfo("ChunkStream", "Using cacheable stream, chunks order is strictly respected and the download will be aborted if one file fails to install");

        public override bool HasChunkCacheSupport => true;

        protected override void LoadChunk()
        {
            var guid = CurrentChunk.Guid;
            var size = (int)CurrentChunk.Size;
            Logger.LogInfo("ChunkStream", $"Loading chunk '{guid}' with hash {CurrentChunk.Hash} and size {size}");
            if (State.CachedChunks.TryGetValue(guid, out var syncStream))
            {
                Logger.LogInfo("ChunkStream", $"Loaded chunk '{guid}' from cache");
                syncStream.Read(SharedBuffer, size, CurrentChunk.Offset);
                if (!syncStream.HasReachedEnd) 
                    return;
                
                Logger.LogInfo("ChunkStream", $"Removed chunk '{guid}' from cache and disposed the associated stream");
                syncStream.Dispose();
                State.CachedChunks.TryRemove(guid, out _);

                return;
            }

            Logger.LogInfo("ChunkStream", $"Attempting to dequeue the reader for chunk '{guid}'. Timeout: {Timeout} ms");
            var reader = DequeueWithTimeout();
            Logger.LogInfo("ChunkStream", $"Successfully dequeued the reader for chunk '{guid}'");
            FChunkHeaderMinimal chunkHeader = new(reader);
            Logger.LogInfo("ChunkStream", $"Parsed header, validating chunk '{guid}'");
            AssertChunkValidity(reader.BaseStream.Length, in chunkHeader);
            var compressedDataSize = chunkHeader.DataSizeCompressed;
            var compressed = (chunkHeader.StoredAs & EChunkStorageFlags.Compressed) != EChunkStorageFlags.None;
            if (compressed)
            {
               /* PSA: Chunks are Zlib compressed. The Zlib Header is 0x78 0x9C. Let's break it down
                  78 is 0111 1000 and 9C is 1001 1100
                  The header's fields are:
               CMF:
                  CM (compression method): low nibble of 0x78 -> 1000 -> 0x8 (deflate)
                  CINFO (compression info): high nibble of 0x78 -> 0111 -> 0x7 ( (log2(lz77 window size: 32000) - 8) = (15-8 = 7))
               FLG:
                  FCHECK: bit 0-4 of 0x9C -> 11100 -> 0x1C (((0x78 << 8) | 0x9C) % 31 == 0)
                  FDICT (preset dictionary): bit 5 of 0x9c -> 0 (no DICT dictionary identifier present)
                  FLEVEL (compression level): bit 6-7 of 0x9c -> 10 -> 0x02 (default compression algorithm)
                  */
               Logger.LogInfo("ChunkStream", $"Chunk '{guid}' is zlib compressed");
               var zlibStream = new ZlibStream(reader.BaseStream, CompressionMode.Decompress);
               zlibStream.Read(SharedBuffer, 0, size);
               if (zlibStream.TotalIn != compressedDataSize)
               {
                   State.CachedChunks.TryAdd(guid, new(zlibStream, chunkHeader.DataSizeUncompressed));
                   Logger.LogInfo("ChunkStream", $"Added chunk '{guid}' to cache");
               }
               else
                   zlibStream.Dispose();
            }
            else
            {
                Logger.LogInfo("ChunkStream", $"Chunk '{guid}' is not zlib compressed");
                reader.Read(SharedBuffer, 0, size);
                if (reader.BaseStream.Position != compressedDataSize)
                {
                    State.CachedChunks.TryAdd(guid, new(reader.BaseStream, chunkHeader.DataSizeUncompressed));
                    Logger.LogInfo("ChunkStream", $"Added chunk '{guid}' to cache");
                }
                else
                    reader.BaseStream.Dispose();
            }

            reader.Dispose();
        }
    }
}