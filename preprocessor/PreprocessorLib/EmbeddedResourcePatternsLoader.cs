using NHyphenator.Loaders;
using System.Reflection;

namespace GameSubtitles.Lib;

/// <summary>
/// Loads TeX hyphenation pattern files from embedded resources within this assembly.
/// </summary>
internal sealed class EmbeddedResourcePatternsLoader : IHyphenatePatternsLoader
{
    private readonly string _resourceBaseName;
    private readonly Assembly _assembly;

    /// <param name="resourceBaseName">
    /// Fully-qualified embedded resource name without extension,
    /// e.g. "GameSubtitles.Lib.Dictionaries.hyph-fr"
    /// </param>
    public EmbeddedResourcePatternsLoader(string resourceBaseName)
    {
        _resourceBaseName = resourceBaseName;
        _assembly = typeof(EmbeddedResourcePatternsLoader).Assembly;
    }

    public string LoadPatterns()
    {
        var raw = ReadResource($"{_resourceBaseName}.pat.txt")
            ?? throw new InvalidOperationException(
                $"Embedded pattern resource not found: {_resourceBaseName}.pat.txt");

        // NHyphenator cannot handle TeX patterns that contain apostrophe characters
        // — either curly (U+2018/U+2019) or plain ASCII (U+0027).  These patterns
        // exist in several language files (e.g. French) to handle contractions
        // such as "aujourd'hui" and do not affect hyphenation of regular words.
        // Filtering them out lets all other patterns work correctly.
        bool hasApostrophe = raw.Contains('\u2018') || raw.Contains('\u2019') || raw.Contains('\'');
        if (!hasApostrophe)
            return raw;

        var lines = raw.Split('\n');
        var filtered = lines.Where(l =>
            !l.Contains('\u2018') && !l.Contains('\u2019') && !l.Contains('\''));
        return string.Join('\n', filtered);
    }

    public string LoadExceptions()
        => ReadResource($"{_resourceBaseName}.hyp.txt") ?? string.Empty;

    private string? ReadResource(string name)
    {
        using var stream = _assembly.GetManifestResourceStream(name);
        if (stream is null) return null;
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
