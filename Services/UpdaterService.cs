using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nocturo.Downloader.Services;

namespace Nocturo.Downloader.Services
{
    public class UpdaterService : InstallationService
    {
        public event FileUpdatedEventHandler OnFileUpdated;
        public override void Initialize() => throw new NotImplementedException();
        public override void Pause() => throw new NotImplementedException();
        public override Task Start() => throw new NotImplementedException();
        public override void Stop() => throw new NotImplementedException();

        /// <inheritdoc />
        public override void Resume() => throw new NotImplementedException();

        /// <inheritdoc />
        internal override int ChunksInstalled { get; set; }
    }
}