using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Nocturo.Common.Utilities;
using Nocturo.Downloader.Models;
using Nocturo.Downloader.Streams;

namespace Nocturo.Downloader.Services
{
    public delegate void ChunkInstalledEventHandler(object sender, ChunkInstalledEventArgs args);

    public delegate void FileInstalledEventHandler(object sender, FileInstalledEventArgs args);

    public delegate void FileUpdatedEventHandler(object sender, FileUpdatedEventArgs args);

    public abstract class InstallationService : IServiceState
    {
        protected readonly AutoResetEvent Waiter = new(false);

        private readonly object _stateLock = new();

        private readonly object _threadCountLock = new();

        private bool _isCancelled;

        private bool _isPaused;

        private int _waitingThreads;

        public event ChunkInstalledEventHandler OnChunkInstalled;

        internal ConcurrentDictionary<string, SynchronizedChunkStream> CachedChunks { get; }

        public bool IsPaused
        {
            get
            {
                lock (_stateLock)
                    return _isPaused;
            }

            protected set
            {
                lock (_stateLock)
                {
                    _isPaused = value;

                    if (value)
                        return;

                    /*
                     * Why this extension method? Basically, we're facing a dilemma. If we want to use ManualResetEvent(Slim),
                     * we have to call Reset() after calling Set() in order to set the state as non-signaled (blocking the threads that will encounter Wait())
                     * However, calling Reset() right after calling Set() doesn't guarantee that all the threads have passed the Wait(). So we have to keep track of the WaitingThreads
                     * incrementing the value when a thread is waiting and decrementing it when it passes. Then we check when WaitingThreads = 0 and call the Reset() event. This is scuffed.
                     * This is why we're using AutoResetEvent. Unlike ManualResetEvent(Slim), it automatically calls Reset() after ensuring that a thread has passed the WaitOne.
                     * The downside is, unlike MRE(S), it doesn't allow all the threads to pass. Only one. So we have to call Set() x times where x is the waiting threads count
                     * We're passing _waitingThreads as a reference rather than a value in order to get the latest value in case a thread updates the value while the extension is executing
                     */
                    Waiter.SetAllThreads(in _waitingThreads);
                    WaitingThreads = 0;
                }
            }
        }

        public bool IsCancelled
        {
            get
            {
                lock (_stateLock)
                    return _isCancelled;
            }

            protected set
            {
                lock (_stateLock)
                {
                    _isCancelled = value;
                    IsPaused = false;
                }
            }
        }

        internal int WaitingThreads
        {
            get
            {
                lock (_threadCountLock)
                    return _waitingThreads;
            }

            set
            {
                lock (_threadCountLock)
                    _waitingThreads = value;
            }
        }

        internal abstract int ChunksInstalled { get; set; }

        internal bool CheckPauseOrCancel()
        {
            if (IsPaused)
            {
                ++WaitingThreads;
                Waiter.WaitOne();
            }

            return IsCancelled;
        }

        public abstract void Initialize();

        public abstract Task Start();

        public abstract void Pause();

        public abstract void Stop();

        public abstract void Resume();
    }
}