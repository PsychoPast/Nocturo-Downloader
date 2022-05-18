namespace Nocturo.Downloader.Models
{
    public class ChunkInstalledEventArgs
    {
        public int ChunkSize { get; init; }

        public int RemainingChunksCount { get; init; }
    }
}