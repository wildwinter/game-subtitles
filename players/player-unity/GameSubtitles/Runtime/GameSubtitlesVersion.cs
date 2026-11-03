namespace GameSubtitles
{
    /// <summary>
    /// Which Game Subtitles this is. A library that ships its own copy of Game Subtitles can read it
    /// at start-up to tell when the game is running a different copy.
    /// </summary>
    public static class GameSubtitlesVersion
    {
        /// <summary>This player's version, as in its package.json. Written by the build; never edit it by hand.</summary>
        public const string Current = "0.4.1";
    }
}
