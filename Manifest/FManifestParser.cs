using System;
using System.Linq;
using System.Text.Json;
using UEManifestReader;
using System.Diagnostics;
using UEManifestReader.Enums;
using UEManifestReader.Objects;
using Nocturo.Common.Constants;
using Nocturo.Common.Utilities;
using System.Collections.Generic;
using Nocturo.Downloader.Utilities;
using Nocturo.Downloader.Manifest.Enums;

namespace Nocturo.Downloader.Manifest
{
    public class FManifestParser : IManifestReader, IDisposable
    {
        private readonly bool _excludeChunkInfos;
        private readonly bool _includeFileHashes;
        private byte[] _manifestData;
        private Dictionary<string, string> _hashesLookup;
        private Dictionary<string, string> _dataGroupLookup;
        private bool _disposed;

        public FManifestParser(byte[] manifestData, EParseMode parseMode = EParseMode.Default)
        {
            if (manifestData == null || manifestData.Length == 0) 
                new ArgumentException("Manifest Data buffer is null or empty", nameof(manifestData)).LogErrorBeforeThrowing("ManifestParser");

            _manifestData = manifestData;
            _hashesLookup = new();
            _dataGroupLookup = new();
            _includeFileHashes = parseMode > EParseMode.Default;
            _excludeChunkInfos = parseMode == EParseMode.GameVerify;

            Logger.LogInfo("ManifestParser", $"Parsing manifest in {parseMode} mode");

            Manifest = new()
            {
                ManifestMeta = new(),
                ChunkSubdir = EChunkSubdir.ChunksV3,
                BaseUrls = new()
                {
                    Strings.Downloader.CHUNK_DOWNLOAD_BASE_URL
                }
            };
        }

        public void ReadManifest()
        {
            Utf8JsonReader reader = new(new ReadOnlySpan<byte>(_manifestData, 0, _manifestData.Length), true, default);
            Stopwatch watch = new();
            watch.Start();
            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                    continue;

                switch (Util.GetAnsiStringFromByteSpan(reader.ValueSpan))
                {
                    case "BuildVersionString":
                        reader.Read();
                        Manifest.ManifestMeta.BuildVersion = Util.GetAnsiStringFromByteSpan(reader.ValueSpan);
                        break;

                    case "FileManifestList":
                        Manifest.FileList = FileManifestListBuilder.BuildFileList(ref reader, _includeFileHashes, _excludeChunkInfos);
                        break;

                    case "ChunkHashList":
                    {
                        if (!_excludeChunkInfos)
                        {
                            reader.Read();
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                            {
                                var guid = Util.GetAnsiStringFromByteSpan(reader.ValueSpan);
                                reader.Read();
                                // reversed due to the endianness
                                var hash = ParserHelper.HashBlobToHexString(reader.ValueSpan, sizeof(ulong), true);
                                _hashesLookup.Add(guid, hash);
                            }
                        }
                        else
                        {
                            do reader.Read();
                            while (reader.TokenType != JsonTokenType.EndObject);
                        }
                    }
                        break;

                    case "DataGroupList":
                    {
                        if (!_excludeChunkInfos)
                        {
                            reader.Read();
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                            {
                                var guid = Util.GetAnsiStringFromByteSpan(reader.ValueSpan);
                                reader.Read();

                                // we need to get rid of the trailing '0'
                                var dataGroup = Util.GetAnsiStringFromByteSpan(reader.ValueSpan, 1);
                                _dataGroupLookup.Add(guid, dataGroup);
                            }
                        }
                        else
                        {
                            do reader.Read();
                            while (reader.TokenType != JsonTokenType.EndObject);
                        }

                        if (!_excludeChunkInfos)
                        {
                            var guids = _hashesLookup.Keys;
                            var chunks = guids.Select(guid => new FChunkInfo(guid, _hashesLookup[guid], 
                                null, _dataGroupLookup[guid], null, null)).ToList();

                            Manifest.ChunkList = chunks;
                        }

                        watch.Stop();
                        var parsingTime = watch.Elapsed;
                        Logger.LogInfo("ManifestParser", $"Successfully parsed manifest for game version {Manifest.ManifestMeta.BuildVersion} in {Math.Round(parsingTime.TotalMilliseconds, 2)} ms");
                        return;
                    }
                }
            }
        }

        public void ReadManifest(ManifestStorage str) 
            => throw new NotImplementedException();

        /// <summary>
        /// Gets the parsed manifest object.
        /// </summary>
        public FManifest Manifest { get; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _manifestData = null;
                _dataGroupLookup = null;
                _hashesLookup = null;
            }
            _disposed = !_disposed;
        }
    }
}