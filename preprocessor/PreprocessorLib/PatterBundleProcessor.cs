using System.Text;
using System.Text.Json;

namespace GameSubtitles.Lib;

/// <summary>
/// Inserts soft hyphens into a compiled Patter bundle (<c>.patterc</c>): the text of every line,
/// text line and choice, in every locale the bundle carries, each by its own language's rules.
/// </summary>
/// <remarks>
/// Every other byte of the bundle is left as it was, including <c>content.hash</c>, so a processed
/// bundle still passes Patter's staleness check. Character names and speaker labels are never touched,
/// and neither are <c>{...}</c> interpolation slots or the <c>&lt;b&gt;</c>, <c>&lt;i&gt;</c> and
/// <c>&lt;bi&gt;</c> formatting tags inside a string.
/// </remarks>
public sealed class PatterBundleProcessor
{
    private const string SchemaPrefix = "patter/bundle@";

    private static readonly string[] FormattingTags = ["<b>", "</b>", "<i>", "</i>", "<bi>", "</bi>"];

    private readonly SubtitlePreprocessor _preprocessor;

    /// <summary>Creates a processor that hyphenates with the given preprocessor.</summary>
    public PatterBundleProcessor(SubtitlePreprocessor preprocessor) => _preprocessor = preprocessor;

    /// <summary>
    /// Returns <c>true</c> if <paramref name="utf8Json"/> is a JSON object whose <c>schema</c> starts
    /// with <c>patter/bundle@</c>.
    /// </summary>
    public static bool IsPatterBundle(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            var reader = new Utf8JsonReader(utf8Json);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return false;
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                bool isSchema = reader.ValueTextEquals("schema");
                reader.Read();
                if (isSchema)
                    return reader.TokenType == JsonTokenType.String &&
                           reader.GetString()!.StartsWith(SchemaPrefix, StringComparison.Ordinal);
                reader.Skip();
            }
            return false;
        }
        catch (JsonException) { return false; }
    }

    /// <summary>Processes a bundle held as a string. See <see cref="Process(byte[])"/>.</summary>
    public PatterBundleResult Process(string json) => Process(Encoding.UTF8.GetBytes(json));

    /// <summary>
    /// Processes a bundle held as UTF-8 bytes and returns the processed bytes.
    /// </summary>
    /// <exception cref="FormatException">
    /// The input is not strict JSON, or not a Patter bundle.
    /// </exception>
    public PatterBundleResult Process(byte[] utf8Json)
    {
        var warnings = new List<string>();
        var bomLength = HasBom(utf8Json) ? 3 : 0;
        var json = utf8Json.AsMemory(bomLength);

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new FormatException($"JSON parse error: {ex.Message}", ex); }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("schema", out var schema) ||
                schema.ValueKind != JsonValueKind.String ||
                !schema.GetString()!.StartsWith(SchemaPrefix, StringComparison.Ordinal))
                throw new FormatException($"File is not a Patter bundle (no \"schema\": \"{SchemaPrefix}...\").");

            if (root.TryGetProperty("localisation", out var loc) && loc.ValueKind == JsonValueKind.Object &&
                loc.TryGetProperty("mode", out var mode) && mode.ValueKind == JsonValueKind.String &&
                mode.GetString() == "ids")
                return new PatterBundleResult(utf8Json, 0, warnings, idsOnly: true);

            var defaultLocale = root.TryGetProperty("locales", out var locales) &&
                                locales.ValueKind == JsonValueKind.Object &&
                                locales.TryGetProperty("default", out var def) &&
                                def.ValueKind == JsonValueKind.String
                ? def.GetString()
                : null;

            var shown = new HashSet<string>(StringComparer.Ordinal);
            var action = new HashSet<string>(StringComparer.Ordinal);
            if (root.TryGetProperty("scenes", out var scenes))
                CollectIds(scenes, shown, action);

            var edits = FindEdits(json.Span, defaultLocale, shown, action, warnings);
            if (edits.Count == 0)
                return new PatterBundleResult(utf8Json, 0, warnings, idsOnly: false);

            return new PatterBundleResult(Splice(utf8Json, bomLength, edits), edits.Count, warnings, idsOnly: false);
        }
    }

    /// <summary>
    /// Hyphenates one Patter string, leaving <c>{...}</c> slots, <c>{{</c> / <c>}}</c> escapes and
    /// formatting tags without soft hyphens.
    /// </summary>
    public string ProcessText(string text, string? languageCode)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var sb = new StringBuilder(text.Length + 16);
        int runStart = 0, i = 0;
        while (i < text.Length)
        {
            int protectedLength = ProtectedLengthAt(text, i);
            if (protectedLength == 0) { i++; continue; }

            if (i > runStart) sb.Append(_preprocessor.Process(text[runStart..i], languageCode));
            sb.Append(text, i, protectedLength);
            i += protectedLength;
            runStart = i;
        }
        if (runStart < text.Length) sb.Append(_preprocessor.Process(text[runStart..], languageCode));
        return sb.ToString();
    }

    // Length of the span at text[i] that must not be hyphenated, or 0. Braces follow Patter's own
    // tokeniser: "{{" and "}}" are escapes, and a "{" with a later "}" opens a span up to it.
    private static int ProtectedLengthAt(string text, int i)
    {
        char c = text[i];
        if ((c == '{' || c == '}') && i + 1 < text.Length && text[i + 1] == c) return 2;
        if (c == '{')
        {
            int close = text.IndexOf('}', i + 1);
            return close < 0 ? 0 : close - i + 1;
        }
        if (c == '<')
        {
            foreach (var tag in FormattingTags)
                if (string.CompareOrdinal(text, i, tag, 0, tag.Length) == 0) return tag.Length;
        }
        return 0;
    }

    // Walks the scene trees for the ids of line and text beats (prompts included). Game data and
    // extension values are the game's own and never hold beats, so the walk stays out of them.
    private static void CollectIds(JsonElement node, HashSet<string> shown, HashSet<string> action)
    {
        if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in node.EnumerateArray()) CollectIds(item, shown, action);
            return;
        }
        if (node.ValueKind != JsonValueKind.Object) return;

        if (node.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String &&
            kind.GetString() is "line" or "text" &&
            node.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
        {
            bool isAction = node.TryGetProperty("action", out var a) && a.ValueKind == JsonValueKind.True;
            (isAction ? action : shown).Add(id.GetString()!);
        }

        foreach (var prop in node.EnumerateObject())
        {
            if (prop.NameEquals("gameData") || prop.NameEquals("extensions")) continue;
            CollectIds(prop.Value, shown, action);
        }
    }

    private readonly record struct Edit(int Start, int Length, byte[] Replacement);

    // Finds every strings[locale][id] value to change, with its byte range in the input.
    private List<Edit> FindEdits(ReadOnlySpan<byte> json, string? defaultLocale,
        HashSet<string> shown, HashSet<string> action, List<string> warnings)
    {
        var edits = new List<Edit>();
        bool defaultSupported = SubtitlePreprocessor.IsLanguageSupported(defaultLocale);
        bool warnedDefault = false;

        var reader = new Utf8JsonReader(json);
        reader.Read(); // root object
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool isStrings = reader.ValueTextEquals("strings");
            reader.Read();
            if (!isStrings || reader.TokenType != JsonTokenType.StartObject) { reader.Skip(); continue; }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var locale = reader.GetString()!;
                reader.Read();
                if (reader.TokenType != JsonTokenType.StartObject) { reader.Skip(); continue; }

                bool localeSupported = SubtitlePreprocessor.IsLanguageSupported(locale);
                bool localeHasShown = false, localeHasAction = false;

                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    var id = reader.GetString()!;
                    reader.Read();
                    if (reader.TokenType != JsonTokenType.String) { reader.Skip(); continue; }

                    // Action text is never translated: every table holds the source, so it takes the
                    // default locale's rules.
                    string? language;
                    if (action.Contains(id))     { language = defaultSupported ? defaultLocale : null; localeHasAction = true; }
                    else if (shown.Contains(id)) { language = localeSupported ? locale : null; localeHasShown = true; }
                    else continue;
                    if (language is null) continue;

                    var text = reader.GetString()!;
                    var processed = ProcessText(text, language);
                    if (processed == text) continue;

                    int start = (int)reader.TokenStartIndex;
                    int length = reader.ValueSpan.Length + 2; // the raw string, quotes included
                    edits.Add(new Edit(start, length, EncodeJsonString(processed)));
                }

                if (localeHasShown && !localeSupported)
                    warnings.Add($"No hyphenation rules for locale '{locale}': its strings were left as they are.");
                if (localeHasAction && !defaultSupported && !warnedDefault)
                {
                    warnings.Add($"No hyphenation rules for the default locale '{defaultLocale}': action text was left as it is.");
                    warnedDefault = true;
                }
            }
        }
        return edits;
    }

    private static byte[] Splice(byte[] input, int offset, List<Edit> edits)
    {
        using var output = new MemoryStream(input.Length + edits.Count * 16);
        int pos = 0;
        foreach (var edit in edits)
        {
            output.Write(input, pos, offset + edit.Start - pos);
            output.Write(edit.Replacement);
            pos = offset + edit.Start + edit.Length;
        }
        output.Write(input, pos, input.Length - pos);
        return output.ToArray();
    }

    // Encodes a string exactly as JavaScript's JSON.stringify does, which is how Patter writes its
    // bundles, so a changed string differs from the original only by its soft hyphens.
    private static byte[] EncodeJsonString(string s)
    {
        var sb = new StringBuilder(s.Length + 2).Append('"');
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            switch (c)
            {
                case '"':  sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                        sb.Append(c).Append(s[++i]);
                    else if (char.IsSurrogate(c))
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else
                        sb.Append(c);
                    break;
            }
        }
        return Encoding.UTF8.GetBytes(sb.Append('"').ToString());
    }

    private static bool HasBom(byte[] b) => b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF;
}

/// <summary>The outcome of processing a Patter bundle.</summary>
public sealed class PatterBundleResult
{
    internal PatterBundleResult(byte[] utf8Json, int processedCount, IReadOnlyList<string> warnings, bool idsOnly)
    {
        Utf8Json = utf8Json;
        ProcessedCount = processedCount;
        Warnings = warnings;
        IdsOnly = idsOnly;
    }

    /// <summary>The processed bundle, as UTF-8 bytes.</summary>
    public byte[] Utf8Json { get; }

    /// <summary>The processed bundle, as a string.</summary>
    public string Json => Encoding.UTF8.GetString(Utf8Json);

    /// <summary>How many strings gained soft hyphens.</summary>
    public int ProcessedCount { get; }

    /// <summary>Locales left unprocessed because the preprocessor has no rules for them.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// <c>true</c> for a bundle in IDs-only localisation (<c>localisation.mode: "ids"</c>), which carries
    /// no text to process and is returned unchanged.
    /// </summary>
    public bool IdsOnly { get; }
}
