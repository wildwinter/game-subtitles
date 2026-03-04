using NHyphenator;

namespace GameSubtitles.Lib;

/// <summary>
/// Internal engine that maps language codes to cached NHyphenator instances.
/// </summary>
internal sealed class HyphenationEngine
{
    // Maps normalised BCP 47 tag (lower-case, hyphen-separated) to
    // embedded resource base name (without extension).
    private static readonly Dictionary<string, string> LanguageMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"]       = "GameSubtitles.Lib.Dictionaries.hyph-en-us",
            ["en-us"]    = "GameSubtitles.Lib.Dictionaries.hyph-en-us",
            ["en-gb"]    = "GameSubtitles.Lib.Dictionaries.hyph-en-gb",
            ["fr"]       = "GameSubtitles.Lib.Dictionaries.hyph-fr",
            ["it"]       = "GameSubtitles.Lib.Dictionaries.hyph-it",
            ["de"]       = "GameSubtitles.Lib.Dictionaries.hyph-de-1996",
            ["de-1996"]  = "GameSubtitles.Lib.Dictionaries.hyph-de-1996",
            ["es"]       = "GameSubtitles.Lib.Dictionaries.hyph-es",
            ["ru"]       = "GameSubtitles.Lib.Dictionaries.hyph-ru",
            ["pl"]       = "GameSubtitles.Lib.Dictionaries.hyph-pl",
            ["pt"]       = "GameSubtitles.Lib.Dictionaries.hyph-pt",
            ["pt-br"]    = "GameSubtitles.Lib.Dictionaries.hyph-pt",
            ["nl"]       = "GameSubtitles.Lib.Dictionaries.hyph-nl",
            ["sv"]       = "GameSubtitles.Lib.Dictionaries.hyph-sv",
            ["nb"]       = "GameSubtitles.Lib.Dictionaries.hyph-nb",
            ["da"]       = "GameSubtitles.Lib.Dictionaries.hyph-da",
            ["fi"]       = "GameSubtitles.Lib.Dictionaries.hyph-fi",
            ["cs"]       = "GameSubtitles.Lib.Dictionaries.hyph-cs",
            ["sk"]       = "GameSubtitles.Lib.Dictionaries.hyph-sk",
            ["hu"]       = "GameSubtitles.Lib.Dictionaries.hyph-hu",
            ["tr"]       = "GameSubtitles.Lib.Dictionaries.hyph-tr",
            ["uk"]       = "GameSubtitles.Lib.Dictionaries.hyph-uk",
            ["hr"]       = "GameSubtitles.Lib.Dictionaries.hyph-hr",
            ["ro"]       = "GameSubtitles.Lib.Dictionaries.hyph-ro",
            ["bg"]       = "GameSubtitles.Lib.Dictionaries.hyph-bg",
        };

    private readonly Dictionary<string, Hyphenator> _cache = new();
    private readonly object _lock = new();

    /// <summary>
    /// Returns a cached <see cref="Hyphenator"/> for the given language code,
    /// or <c>null</c> if no patterns are available for that language.
    /// </summary>
    public Hyphenator? GetHyphenator(string? languageCode)
    {
        var tag = Normalise(languageCode);
        if (tag is null) return null;

        // Exact match, then base-language fallback
        if (!LanguageMap.TryGetValue(tag, out var resourceBase))
        {
            var dash = tag.IndexOf('-');
            if (dash <= 0 || !LanguageMap.TryGetValue(tag[..dash], out resourceBase))
                return null;
        }

        lock (_lock)
        {
            if (_cache.TryGetValue(tag, out var cached)) return cached;

            var loader = new EmbeddedResourcePatternsLoader(resourceBase);
            var hyphenator = new Hyphenator(
                loader,
                hyphenateSymbol: "\u00AD", // soft hyphen
                minWordLength: 6,
                minLetterCount: 2,
                hyphenateLastWord: true,
                sortPatterns: true);

            _cache[tag] = hyphenator;
            return hyphenator;
        }
    }

    /// <summary>All language tags with bundled patterns.</summary>
    public static IReadOnlyList<string> SupportedLanguages
        => [.. LanguageMap.Keys.Order()];

    /// <summary>
    /// Normalises a BCP 47 or ICU language code to lower-case hyphen-separated form.
    /// Returns <c>null</c> for null/empty input.
    /// </summary>
    private static string? Normalise(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return code.Replace('_', '-').Trim().ToLowerInvariant();
    }
}
