using UnityEngine;

namespace GameSubtitles
{
    /// <summary>
    /// Optional character-name styling passed to
    /// <see cref="ISubtitleRenderer.Render"/> for the first line of every page.
    /// </summary>
    public struct CharacterContext
    {
        /// <summary>The character name to display (e.g. "Aria").</summary>
        public string Name;

        /// <summary>
        /// Colour for the name. <c>null</c> means the renderer's default text colour is used.
        /// </summary>
        public Color? Color;

        /// <summary>
        /// Colour for the subtitle body text on all lines.
        /// <c>null</c> means the renderer's default text colour is used.
        /// </summary>
        public Color? LineColor;
    }

    /// <summary>
    /// Interface that a subtitle renderer must implement.
    ///
    ///   MeasureLineWidth(text, useCharacterNameFont) -> float
    ///   GetContainerWidth()                          -> float
    ///   Render(lines, charCtx)       -> void
    ///   Clear()                      -> void
    ///
    /// Implement this interface on any MonoBehaviour (or plain C# class) and pass it
    /// to <see cref="SubtitlePlayer.Initialize"/> to plug it in.
    /// </summary>
    public interface ISubtitleRenderer
    {
        /// <summary>
        /// Returns the rendered width of <paramref name="text"/> in the renderer's font.
        /// Called frequently during layout — keep implementations fast.
        /// Trailing spaces must count: the player measures "Name: " to reserve room for the
        /// character-name prefix. TextMeshPro's preferred width leaves them out.
        /// </summary>
        /// <param name="text">Text to measure.</param>
        /// <param name="useCharacterNameFont">
        /// When <c>true</c>, measure with the character-name font; otherwise the subtitle font.
        /// </param>
        float MeasureLineWidth(string text, bool useCharacterNameFont = false);

        /// <summary>
        /// Returns the maximum line width available to the renderer (pixels at canvas scale).
        /// </summary>
        float GetContainerWidth();

        /// <summary>
        /// Display the given lines. <paramref name="lines"/> is one page from WrapAndPaginate;
        /// typically 1–3 strings, already hyphen-resolved and ellipsis-appended where needed.
        /// When <paramref name="characterContext"/> is non-null, the renderer should prepend
        /// "Name: " (styled per the context) to the first line.
        /// </summary>
        void Render(string[] lines, CharacterContext? characterContext = null);

        /// <summary>
        /// Remove all displayed content (called between pages and on stop/reset).
        /// </summary>
        void Clear();
    }
}
