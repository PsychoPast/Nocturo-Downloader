using System.Collections.Generic;
using Nocturo.Common.Utilities;
using Nocturo.Downloader.Enums;

namespace Nocturo.Downloader.Models
{
    public class InstallationSettings
    {
        public string InstallPath { get; internal set; }

        public uint MaxBytesPerSec { get; init; }

        [CustomLoggerName("ParallelFilesCount")]
        public int Concurrency { get; init; }

        [CustomLoggerName("InstallTags")]
        public InstallTagCollection VersionInstallTags { get; init; }

        public HashSet<string> FilesToIgnore { get; init; }

        [CustomLoggerName("IgnoreSaveTheWorldContent")]
        public bool IgnoreSTWFiles { get; init; }

        [CustomLoggerName("IgnoreLanguagePacks")]
        public bool IgnoreLanguagePakFiles { get; init; }

        public bool EnableChunkCaching { get; init; }

        public override string ToString() => this.GenerateLogString();
    }
}