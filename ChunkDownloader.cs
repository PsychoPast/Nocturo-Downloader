using System;
using System.IO;
using System.Text;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Nocturo.Common.Utilities;
using Nocturo.Downloader.Models;
using System.Collections.Generic;
using Nocturo.Downloader.Services;

namespace Nocturo.Downloader
{
    internal class ChunkDownloader : IDisposable
    {
        internal EventHandler<BinaryReader> OnChunkDownloaded;

        private readonly string _baseThreadName;

        private readonly List<Chunk> _chunks;

        private readonly HttpClient _client;

        private readonly int _count;

        private readonly InstallationService _manager;

        private int _currentIndex;

        private bool _hasStarted;

        internal ChunkDownloader(List<Chunk> chunks, InstallationService manager)
        {
            _chunks = chunks;
            _count = chunks.Count;
            _client = new();
            _manager = manager;
            _baseThreadName = Thread.CurrentThread.Name;
        }

        public void Dispose() 
            => _client.Dispose();

        public async Task StartDownload()
        {
            if (_hasStarted)
                throw new InvalidOperationException("Download has already started.");

            _hasStarted = true;

            if (OnChunkDownloaded == null)
                throw new NullReferenceException("OnChunkDownloaded event must be bound.");

            // we loop though the chunks, download them and raise the event until we reach the end of the list or the user cancels the download
            while (_currentIndex < _count)
            {
                // if the user cancels the download, we interrupt the chunks download and return the method
                if (_manager.CheckPauseOrCancel())
                    return;

                var chunk = _chunks[_currentIndex];

                // if caching is enabled and the chunk is cached, no need to download it again
                if (_manager.CachedChunks.ContainsKey(chunk.Guid))
                {
                    Logger.LogWarning("ChunkDownloader", $"Cache already contains chunk {chunk.Guid}, skipping download...");
                    _currentIndex++;
                    continue;
                }

                long start = -1;
                long end = -1;
                HttpResponseMessage response = null;
                var retries = 1;
                {
                    // we try 10 times to download the chunk, if it fails we forcibly pauses the download until the user resumes it
                    do
                    {
                        HttpRequestMessage request = new(HttpMethod.Get, chunk.DownloadUrl);

                        try
                        {
                            start = Environment.TickCount64;

                            // context switching being expensive, by setting ConfigureAwait to false, we're instructing the code to continue its execution on whatever _client.SendAsync was completed on instead of switching back to the caller context
                            response = await _client.SendAsync(request).ConfigureAwait(false);
                            end = Environment.TickCount64;
                            break;
                        }
                        catch (Exception e)
                        {
                            Logger.LogWarning("ChunkDownloader",
                                $"[For {_baseThreadName}]: Failed to download chunk {request.RequestUri}. Reason = {e.Message}  Retry Count = {retries}");

                            // if the chunk stream object that holds the current instance returns prematurely, _client will be disposed therefore we're forced to interrupt the chunks download process
                            if (e is TaskCanceledException or ObjectDisposedException)
                            {
                                Logger.LogError("ChunkDownloader",
                                    $"[For {_baseThreadName}]: Chunk Stream associated with the current downloader has ended prematurely for some reason. This shouldn't have happened.");

                                return;
                            }

                            request.Dispose();
                        }
                    }
                    while (retries++ < 10);

                    if (response == null)
                    {
                        //_manager.Pause(true);
                        Logger.LogError("ChunkDownloader",
                            "Failed to download chunk. Download will be paused now to avoid further errors but the pending chunks will be installed");

                        // we continue the loop without incrementing the index so next time the download resumes, it tries to download the same chunk
                        continue;
                    }
                }

                var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false));

                // we calculate the download speed in kb/s 
                //_manager.DownloadSpeed = (int)((stream.Length >> 10) / ((end - start) / 1000));
                OnChunkDownloaded?.Invoke(this, new(stream, Encoding.UTF8, true));
                Logger.LogInfo("ChunkDownloader", $"[Base Thread {_baseThreadName}]: {_count - _currentIndex++ - 1} chunk(s) remaining to download");
            }
        }
    }
}