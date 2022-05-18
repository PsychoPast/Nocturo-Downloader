namespace Nocturo.Downloader.Models
{
    public class FileInstalledEventArgs
    {
        public string Filename { get; init; }

        public int RemainingFilesCount { get; init; }
    }
}