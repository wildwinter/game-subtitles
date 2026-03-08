namespace GameSubtitles
{
    /// <summary>
    /// Interface that a subtitle renderer must implement.
    ///
    ///   MeasureLineWidth(text) -> float
    ///   GetContainerWidth()    -> float
    ///   Render(lines)          -> void
    ///   Clear()                -> void
    ///
    /// Implement this interface on any MonoBehaviour (or plain C# class) and pass it
    /// to <see cref="SubtitlePlayer.Initialize"/> to plug it in.
    /// </summary>
    public interface ISubtitleRenderer
    {
        /// <summary>
        /// Returns the rendered width of <paramref name="text"/> in the renderer's current font.
        /// Called frequently during layout — keep implementations fast.
        /// </summary>
        float MeasureLineWidth(string text);

        /// <summary>
        /// Returns the maximum line width available to the renderer (pixels at canvas scale).
        /// </summary>
        float GetContainerWidth();

        /// <summary>
        /// Display the given lines. <paramref name="lines"/> is one page from WrapAndPaginate;
        /// typically 1–3 strings, already hyphen-resolved and ellipsis-appended where needed.
        /// </summary>
        void Render(string[] lines);

        /// <summary>
        /// Remove all displayed content (called between pages and on stop/reset).
        /// </summary>
        void Clear();
    }
}
