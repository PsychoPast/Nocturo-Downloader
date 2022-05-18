using System.IO;
using Nocturo.Downloader.Enums;
using Nocturo.Common.Exceptions.Common;
using Nocturo.Common.Utilities;

namespace Nocturo.Downloader.Models
{
    // We are skipping the Guid, HashType, RollingHash and SHAHash
    internal readonly ref struct FChunkHeaderMinimal
    {
        private const uint CHUNK_HEADER_MAGIC = 0xB1FE3AA2;

        internal EChunkVersion Version { get; }

        internal uint HeaderSize { get; }

        internal uint DataSizeCompressed { get; }

        internal uint DataSizeUncompressed { get; }

        internal EChunkStorageFlags StoredAs { get; }

        internal FChunkHeaderMinimal(BinaryReader reader)
        {
            var magic = reader.ReadUInt32();
            if (magic != CHUNK_HEADER_MAGIC)
                new ChunkLoadException($"Magic mismatch. Expected {CHUNK_HEADER_MAGIC}, got {magic}.", reader.BaseStream.Position - sizeof(uint))
                   .LogErrorBeforeThrowing("ChunkStream");

            Version = (EChunkVersion)reader.ReadUInt32();
            HeaderSize = reader.ReadUInt32();
            DataSizeCompressed = reader.ReadUInt32();
            reader.BaseStream.Position += 4 * sizeof(uint) + sizeof(ulong); // sizeof(Guid) + sizeof(RollingHash)
            StoredAs = (EChunkStorageFlags)reader.ReadByte();
            if (Version >= EChunkVersion.StoresShaAndHashType)
                reader.BaseStream.Position += 20 + sizeof(byte); // sizeof(SHAHash) + sizeof(HashType)

            DataSizeUncompressed = Version >= EChunkVersion.StoresDataSizeUncompressed ? reader.ReadUInt32() : 1024 << 10; // default is 1MiB (1048576 bytes)
        }
    }
}