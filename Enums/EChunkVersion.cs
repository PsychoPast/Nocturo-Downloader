namespace Nocturo.Downloader.Enums
{
    /// <summary>
    /// Always after the latest version, signifies the latest version plus 1 to allow initialization simplicity.
    /// </summary>
    internal enum EChunkVersion : uint
    {
        Invalid = 0,

        Original,

        StoresShaAndHashType,

        StoresDataSizeUncompressed,

        // Always after the latest version, signifies the latest version plus 1 to allow initialization simplicity.
        LatestPlusOne,

        Latest = LatestPlusOne - 1
    }
}