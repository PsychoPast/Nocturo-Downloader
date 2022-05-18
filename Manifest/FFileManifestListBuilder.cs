using System.Text.Json;
using UEManifestReader.Objects;
using System.Collections.Generic;
using Nocturo.Downloader.Utilities;
using System.Runtime.CompilerServices;

namespace Nocturo.Downloader.Manifest
{
    internal class FileManifestListBuilder
    {
        private FileManifestListBuilder(ref Utf8JsonReader reader, bool includeHashes, bool excludeChunkInfos, out List<FFileManifest> fileList)
        {
            fileList = new();
            ReadOrSkip fileHashAc = includeHashes ? ReadFileHash : Read;
            ReadOrSkip chunkInfAc = !excludeChunkInfos ? ReadChunkInf : SkipArray;
            ReadOrSkip installTagsAc = includeHashes ? ReadInstallTags : SkipArray;
            reader.Read();
            do
            {
                FFileManifest file = new();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName)
                        continue;

                    switch (Util.GetAnsiStringFromByteSpan(reader.ValueSpan))
                    {
                        case "Filename":
                            reader.Read();
                            file.Filename = Util.GetAnsiStringFromByteSpan(reader.ValueSpan);
                            break;

                        case "FileHash":
                            fileHashAc(ref reader, file);
                            break;

                        case "FileChunkParts":
                            chunkInfAc(ref reader, file);
                            break;

                        case "InstallTags":
                            installTagsAc(ref reader, file);
                            break;
                    }
                }

                fileList.Add(file);
            }
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray);
        }

        private delegate void ReadOrSkip(ref Utf8JsonReader reader, FFileManifest file);

        internal static List<FFileManifest> BuildFileList(ref Utf8JsonReader reader, bool includeHashes, bool excludeChunkInfos)
        {
            _ = new FileManifestListBuilder(ref reader, includeHashes, excludeChunkInfos, out var fileList);
            return fileList;
        }

        #region  workaround for not having the possibility to pass Utf8JsonReader as lambda parameter

        private static void ReadFileHash(ref Utf8JsonReader reader, FFileManifest file)
        {
            reader.Read();
            file.FileHash = ParserHelper.HashBlobToHexString(reader.ValueSpan, 20, false); // 20 is the size of SHA1
        }

        private static void ReadChunkInf(ref Utf8JsonReader reader, FFileManifest file)
        {
            List<FChunkPart> chunkParts = new();
            string guid = null;
            var offset = uint.MaxValue;
            var size = uint.MaxValue;
            reader.Read();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    switch (Util.GetAnsiStringFromByteSpan(reader.ValueSpan))
                    {
                        case "Guid":
                            reader.Read();
                            guid = Util.GetAnsiStringFromByteSpan(reader.ValueSpan);
                            break;

                        case "Offset":
                            reader.Read();
                            offset = ParserHelper.FromStringBlob<uint>(reader.ValueSpan);
                            break;

                        case "Size":
                            reader.Read();
                            size = ParserHelper.FromStringBlob<uint>(reader.ValueSpan);
                            break;
                    }
                }

                chunkParts.Add(new(guid, offset, size));
            }

            file.ChunkParts = chunkParts;
        }

        private static void ReadInstallTags(ref Utf8JsonReader reader, FFileManifest file)
        {
            reader.Read(); // [
            reader.Read();
            file.InstallTags = new()
            {
                Util.GetAnsiStringFromByteSpan(reader.ValueSpan)
            };
            reader.Read(); // ]
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Read(ref Utf8JsonReader reader, FFileManifest file)
            => reader.Read();

        private static void SkipArray(ref Utf8JsonReader reader, FFileManifest file)
        {
            do reader.Read();
            while (reader.TokenType != JsonTokenType.EndArray);
        }

        #endregion
    }
}
