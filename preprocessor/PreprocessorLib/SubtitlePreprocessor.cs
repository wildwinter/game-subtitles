namespace GameSubtitles.Lib;

/// <summary>
/// Inserts soft hyphen characters (U+00AD) at safe word-wrap points in
/// localised subtitle strings.
/// </summary>
public sealed class SubtitlePreprocessor
{
    private readonly HyphenationEngine _engine = new();

    /// <summary>
    /// Processes a single subtitle string, inserting U+00AD at hyphenation points.
    /// </summary>
    /// <param name="text">The localised string to process.</param>
    /// <param name="languageCode">
    /// BCP 47 or ICU language code (e.g. "en_GB", "fr_FR", "de-DE").
    /// Null or unrecognised codes return the original string unchanged.
    /// </param>
    /// <returns>
    /// The string with U+00AD inserted at safe hyphenation points, or the
    /// original string if no patterns are available for the language.
    /// </returns>
    public string Process(string text, string? languageCode = null)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var hyphenator = _engine.GetHyphenator(languageCode);
        if (hyphenator is null) return text;

        return hyphenator.HyphenateText(text);
    }

    /// <summary>
    /// Language codes with bundled hyphenation patterns.
    /// </summary>
    public static IReadOnlyList<string> SupportedLanguages
        => HyphenationEngine.SupportedLanguages;
}
