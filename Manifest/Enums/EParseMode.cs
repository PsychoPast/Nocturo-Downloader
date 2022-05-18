namespace Nocturo.Downloader.Manifest.Enums
{
    public enum EParseMode
    {
        /// <summary>
        /// Parse mode for old game versions (ignore files hash).
        /// </summary>
        Default,

        /// <summary>
        /// Parse mode for latest game version (includes files hash).
        /// </summary>
        GameLatestVersion,

        /// <summary>
        /// Parse mode for verifying the game files integrity (ignores chunk parts, chunks hash and chunks data group).
        /// </summary>
        GameVerify,

        /// <summary>
        /// Parse mode for updating the game.
        /// </summary>
        GameUpdate = GameLatestVersion,

        /// <summary>
        /// Parse mode for Epic Games Launcher Content.
        /// </summary>
        EpicLauncherContent = GameLatestVersion
    }
}