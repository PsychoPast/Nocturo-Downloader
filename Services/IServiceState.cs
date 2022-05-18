using System.Threading.Tasks;

namespace Nocturo.Downloader.Services
{
    public interface IServiceState
    {
       // bool IsPaused { get; protected set; }

        //public bool IsCancelled { get; protected set; }

        public void Initialize();

        public Task Start();

        public void Pause();

        public void Stop();

        public void Resume();
    }
}