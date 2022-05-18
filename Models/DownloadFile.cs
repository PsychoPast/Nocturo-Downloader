using System.Collections.Generic;
using Nocturo.Common.Constants;

namespace Nocturo.Downloader.Models
{
    public class DownloadFile
    {
        public List<Chunk> Chunks { get; init; }

        public string Filename { get; init; }

        public long Size { get; init; }
    }

    public record Chunk(string Guid, string Hash, string GroupNumber, uint Size, uint Offset)
    {
        public string DownloadUrl => $"{Strings.Downloader.CHUNK_DOWNLOAD_BASE_URL}/{GroupNumber}/{Hash}_{Guid}.chunk";
    }
}