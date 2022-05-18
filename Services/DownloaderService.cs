using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nocturo.Common.Utilities;
using Nocturo.Downloader.Enums;
using Nocturo.Downloader.Models;
using Nocturo.Downloader.Streams;
using UEManifestReader.Objects;

namespace Nocturo.Downloader.Services
{
    public class DownloaderService : InstallationService
    {
        private readonly FManifest _manifest;

        private readonly string _installPath;

        private readonly uint _maxBps;

        private readonly List<Task> _downloadTasks;

        private readonly int _concurrency;

        private readonly InstallTagCollection _installTags;

        private readonly bool _ignoreStw;

        private readonly bool _ignoreLang;

        private readonly HashSet<string> _filesToSkip;

        private DownloadFile[] _downloadFiles;

        private bool _isRunning;

        private volatile int _filesInstalled;

        private volatile int _downloadSpeed;

        private int _chunksInstalled;

        internal DownloaderService(FManifest manifest, InstallationSettings settings)
        {
            if (manifest.FileList?.Count == 0)
            {
                new ArgumentException("Provided manifest is invalid").LogErrorBeforeThrowing("Downloader");
            }

            _manifest = manifest;
            _installPath = settings.InstallPath;
            _concurrency = settings.Concurrency;

            // max file count cannot be null or negative
            if (_concurrency <= 0)
                new ArgumentException("Provided concurrency value cannot be equal to zero", nameof(_concurrency))
                   .LogErrorBeforeThrowing("Downloader");

            _maxBps = settings.MaxBytesPerSec;
            _downloadTasks = new(_concurrency);
            _installTags = settings.VersionInstallTags;
            _ignoreLang = settings.IgnoreLanguagePakFiles;
            _ignoreStw = settings.IgnoreSTWFiles;
            _filesToSkip = settings.FilesToIgnore;
        }

        public event FileInstalledEventHandler OnFileInstalled;
        public long DownloadSize { get; private set; }

        public int FilesCount => _manifest.FileList.Count;

        public bool IsThrottled => _maxBps > 0;

        public int ChunksCount { get; private set; }

        public int Concurrency => _concurrency;

        public string GameVersion => _manifest.ManifestMeta.BuildVersion;

        public int DownloadSpeed
        {
            get => _downloadSpeed;
            internal set
            {
                if (value < 0)
                    throw new ArgumentException("Value must be positive.", nameof(value));

                Interlocked.Exchange(ref _downloadSpeed, value);
            }
        }

        internal override int ChunksInstalled
        {
            get => _chunksInstalled;
            set => Interlocked.Increment(ref _chunksInstalled);
        }

        internal int FilesInstalled
        {
            get => _filesInstalled;
            set => Interlocked.Increment(ref _filesInstalled);
        }



        public override void Initialize()
        {
            if(_isRunning)
                new InvalidOperationException("Cannot initialize a new downloader before since an instance is already running").LogErrorBeforeThrowing("Downloader");

            Logger.LogInfo("Downloader", $"Initializing...");

            // we generate the download files once
            if (_downloadFiles == null)
            {
                // counter for the installation chunks count
                var chunksCount = 0;
                var fileList = _manifest.FileList;
                var chunkList = _manifest.ChunkList;

                // we create a lookup since searching a dictionary is faster than searching a List
                var chunkInfoLookup = new Dictionary<string, FChunkInfo>(chunkList.Count);

                // unlike foreach() which calls internally GetEnumerator() on the IEnumerable, LINQ ForEach FOR loops through the private T[] field of the List<T> which is faster and cleaner
                chunkList.ForEach(chunkInfo =>
                {
                    // we create a lookup which is faster than calling the LINQ method .Where()
                    chunkInfoLookup.Add(chunkInfo.Guid, chunkInfo);
                });

                _downloadFiles = new DownloadFile[fileList.Count];
                int arrIndex = 0;

                // we iterate through the list of FFileManifest objects
                fileList.ForEach(file =>
                {
                    // if we should skip the file
                    if (Filter(file))
                        return;
                    
                    var count = file.ChunkParts.Count;
                    chunksCount += count;
                    var chunks = new List<Chunk>(count);
                    long fileSize = 0;
                    file.ChunkParts.ForEach(chunkPart =>
                    {
                        var chunkSize = chunkPart.Size;
                        fileSize += chunkSize;
                        var guid = chunkPart.Guid;
                        var chunkInfo = chunkInfoLookup[guid];
                        chunks.Add(new Chunk(guid, chunkInfo.Hash, chunkInfo.GroupNumber, chunkSize, chunkPart.Offset));
                    });

                    DownloadSize += fileSize;
                    _downloadFiles[arrIndex++] = new DownloadFile
                    {
                        Filename = file.Filename,
                        Size = fileSize,
                        Chunks = chunks
                    };
                });

                ChunksCount = chunksCount;
            }

            Logger.LogInfo("Downloader",
                $"Initialization successful. {{ Game Size = {DownloadSize.FormatSize()}, Chunks Count = {ChunksCount}, Concurrency = {_concurrency} }}");

            // to make it faster, we process the files in batches. The number of batches is based on the value of _concurrency
            var currentBatchIndex = 0;
            var interval = _downloadFiles.Length / _concurrency;

            // var for the while loop
            int i = 0;
            while (i < _concurrency)
            {
                var remainder = 0;

                // in that case, we have to add to the last file batch the remainder of the division
                if (i == _concurrency - 1)
                    remainder = _downloadFiles.Length % (int)_concurrency;

                var batchIndex = currentBatchIndex;

                int inI = i;
                _downloadTasks.Add(new Task(() =>
                    {
                        Thread.CurrentThread.Name = $"Game_Downloader_{inI}";
                        ProcessBatch(_downloadFiles[batchIndex..(batchIndex + interval + remainder)]);
                    },
                    TaskCreationOptions.LongRunning));

                currentBatchIndex += interval;
                i++;
            }
        }

        public override Task Start()
        {
            if (_downloadTasks.Count == 0 || _downloadTasks.Any(t => t.IsCompleted))
                new InvalidOperationException("Downloader should be initialized first").LogErrorBeforeThrowing("Downloader");

            if (_isRunning)
                new InvalidOperationException("Downloader is already running").LogErrorBeforeThrowing("Downloader");

            _isRunning = true;
            _downloadTasks.ForEach(t => t.Start());
            return Task.WhenAll(_downloadTasks).ContinueWith((_) => _isRunning = false);
        }
        
        public override void Pause() => IsPaused = true;

        public override void Stop() => IsCancelled = true;

        public override void Resume() => IsPaused = false;

        private void ProcessBatch(DownloadFile[] batch)
        {
            Logger.LogInfo("Downloader", $"Downloader thread has started with {batch.Length} files to install");
            
            // 4096 bytes (or 4 Kib) is the recommended buffer size
            const int bufferSize = 1024 << 2;
            var buffer = new byte[bufferSize];

            for (var i = 0; i < batch.Length; i++)
            {
                // if the user cancels, we return the method which implicitly completes the Task
                // the reason we have a check here is in case we pause the download due to the user having connectivity issues 
                // but pursue the installation of the pending chunks
                if (CheckPauseOrCancel())
                    return;

                var file = batch[i];
                var filename = file.Filename;
                var filePath = Path.Combine(_installPath, filename);
                var dir = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(dir))
                {
                    // File.Open doesn't create the subdirectories even if FileMode is set to FileMode.Create, therefore we have to do it ourselves.
                    Directory.CreateDirectory(dir);
                    Logger.LogInfo("Downloader", $"Created directory '{dir}'");
                }

                using BufferedStream s = new(File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.None), bufferSize);
                var chunks = file.Chunks;
                Logger.LogInfo("Downloader", $"Installing {filename} with {chunks.Count} chunks");
                if (chunks.Count > 0)
                {
                    var chunkStream = new CachedChunkStream(chunks, default);
                    using Stream chSt = IsThrottled ? new ThrottledStream(chunkStream, _maxBps) : chunkStream;
                    int bytesRead;
                    while ((bytesRead = chSt.Read(buffer)) != 0)
                    {
                        // we check at every iteration if the user has requested to pause/cancel the download
                        // if the user has connectivity issues, we install the pending chunks before pausing the procedure
                        if (CheckPauseOrCancel())
                            return;

                        s.Write(buffer, 0, bytesRead);
                    }
                }

                var remainingFiles = FilesCount - ++FilesInstalled;
                Logger.LogInfo("Downloader", $"Successfully installed {filename}. {remainingFiles} files remaining");
                OnFileInstalled?.Invoke(this, new FileInstalledEventArgs
                {
                    Filename = filename,
                    RemainingFilesCount = remainingFiles
                });
            }
        }

        private bool Filter(FFileManifest file)
        {
            if (_filesToSkip.Contains(file.Filename))
                return true;

            var installTags = file.InstallTags;
            if (installTags.Count == 0)
                return false;

            var type = _installTags.GetKeyFromSubValue(installTags[0]);
            if ((_ignoreLang && type == InstallTagType.LanguagePack) || (_ignoreStw && type == InstallTagType.STW))
            {
                return true;
            }

            return false;
        }
    }
}