using System;

namespace Nocturo.Downloader.Enums
{
    /// <summary>
    /// Declares flags for chunk headers which specify storage types.
    /// </summary>
    [Flags]
    internal enum EChunkStorageFlags : byte
    {
        None = 0x00,

        /// <summary>
        /// Flag for compressed data.
        /// </summary>
        Compressed = 0x01,

        /// <summary>
        /// Flag for encrypted. If also compressed, decrypt first. Encryption will ruin compressibility.
        /// </summary>
        Encrypted = 0x02
    }
}