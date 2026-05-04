using Xunit;
using GameSubtitles.CLI;
using GameSubtitles.CLI.Formatters;
using GameSubtitles.Lib;

namespace Preprocessor.Tests;

/// <summary>
/// Integration tests for each formatter: reads fixture, processes, verifies output.
/// </summary>
public class FormatterTests : IDisposable
{
    private readonly string _tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly SubtitlePreprocessor _preprocessor = new();
    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public FormatterTests() => Directory.CreateDirectory(_tmpDir);

    public void Dispose() => Directory.Delete(_tmpDir, recursive: true);

    private FileProcessor MakeProcessor(string? field = null, string? lang = "en_US")
        => new(_preprocessor, lang, field);

    // ── PO ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Po_ProcessesMsgstr()
    {
        var input  = Path.Combine(FixturesDir, "sample.po");
        var output = Path.Combine(_tmpDir, "out.po");
        var result = MakeProcessor().ProcessFile(input, output);

        Assert.False(result.HasErrors, string.Join(", ", result.Errors));
        Assert.Equal(2, result.ProcessedCount); // two non-empty msgstrs
        var content = File.ReadAllText(output);
        Assert.Contains('\u00AD', content);
    }

    [Fact]
    public void Po_WarnsWhenFieldSupplied()
    {
        var input  = Path.Combine(FixturesDir, "sample.po");
        var output = Path.Combine(_tmpDir, "out-warn.po");
        var result = MakeProcessor(field: "msgstr").ProcessFile(input, output);

        Assert.True(result.HasWarnings);
        Assert.Contains(result.Warnings, w => w.Contains("not applicable"));
    }

    // ── CSV ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Csv_ProcessesTargetField()
    {
        var input  = Path.Combine(FixturesDir, "sample.csv");
        var output = Path.Combine(_tmpDir, "out.csv");
        var result = MakeProcessor(field: "subtitle").ProcessFile(input, output);

        Assert.False(result.HasErrors, string.Join(", ", result.Errors));
        Assert.Equal(2, result.ProcessedCount); // rows 1 and 2 non-empty (row 3 is empty)
        var content = File.ReadAllText(output);
        Assert.Contains('\u00AD', content);
    }

    [Fact]
    public void Csv_ErrorsOnMissingField()
    {
        var input  = Path.Combine(FixturesDir, "sample.csv");
        var output = Path.Combine(_tmpDir, "err.csv");
        var result = MakeProcessor(field: "nonexistent").ProcessFile(input, output);

        Assert.True(result.HasErrors);
    }

    [Fact]
    public void Csv_ErrorsWhenNoFieldProvided()
    {
        var input  = Path.Combine(FixturesDir, "sample.csv");
        var output = Path.Combine(_tmpDir, "err2.csv");
        var result = MakeProcessor(field: null).ProcessFile(input, output);

        Assert.True(result.HasErrors);
    }

    [Fact]
    public void Csv_NoteColumnUntouched()
    {
        var input  = Path.Combine(FixturesDir, "sample.csv");
        var output = Path.Combine(_tmpDir, "note.csv");
        MakeProcessor(field: "subtitle").ProcessFile(input, output);

        var content = File.ReadAllText(output);
        Assert.Contains("keep", content);
    }

    // ── JSON ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Json_ProcessesArrayField()
    {
        var input  = Path.Combine(FixturesDir, "sample.json");
        var output = Path.Combine(_tmpDir, "out.json");
        var result = MakeProcessor(field: "subtitle").ProcessFile(input, output);

        Assert.False(result.HasErrors, string.Join(", ", result.Errors));
        Assert.Equal(2, result.ProcessedCount); // entries 1 and 2 non-empty (entry 3 is empty)
        var content = File.ReadAllText(output);
        Assert.Contains('\u00AD', content);
    }

    [Fact]
    public void Json_CaseInsensitiveFieldMatch()
    {
        var input  = Path.Combine(FixturesDir, "sample.json");
        var output = Path.Combine(_tmpDir, "ci.json");
        var result = MakeProcessor(field: "SUBTITLE").ProcessFile(input, output);

        Assert.False(result.HasErrors, string.Join(", ", result.Errors));
    }

    [Fact]
    public void Json_ErrorsOnNonArray()
    {
        var input = Path.Combine(_tmpDir, "obj.json");
        File.WriteAllText(input, "{\"subtitle\":\"hello\"}");
        var output = Path.Combine(_tmpDir, "obj-out.json");
        var result = MakeProcessor(field: "subtitle").ProcessFile(input, output);

        Assert.True(result.HasErrors);
    }

    // ── Ink JSON ──────────────────────────────────────────────────────────────

    [Fact]
    public void InkJson_ProcessesNarrativeText()
    {
        var input  = Path.Combine(FixturesDir, "sample.ink.json");
        var output = Path.Combine(_tmpDir, "out.ink.json");
        var result = MakeProcessor().ProcessFile(input, output);

        Assert.False(result.HasErrors, string.Join(", ", result.Errors));
        Assert.True(result.ProcessedCount > 0);
        var content = File.ReadAllText(output);
        Assert.Contains('­', content);
    }

    [Fact]
    public void InkJson_DoesNotHyphenateTagContent()
    {
        var input  = Path.Combine(FixturesDir, "sample.ink.json");
        var output = Path.Combine(_tmpDir, "tag.ink.json");
        MakeProcessor().ProcessFile(input, output);

        // "ws:final" is inside a "#"/"/#" tag block — must pass through unchanged.
        var content = File.ReadAllText(output);
        Assert.Contains("\"^ws:final\"", content);
    }

    [Fact]
    public void InkJson_DoesNotHyphenateStringComparisons()
    {
        var input  = Path.Combine(FixturesDir, "sample.ink.json");
        var output = Path.Combine(_tmpDir, "cmp.ink.json");
        MakeProcessor().ProcessFile(input, output);

        // "^internationalization" is in a str/.../str == /ev block — must not be modified.
        // Ordinal comparison required: CurrentCulture ignores soft hyphens (U+00AD).
        var content = File.ReadAllText(output);
        Assert.Contains("\"^internationalization\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void InkJson_ProcessesChoiceText()
    {
        var input  = Path.Combine(FixturesDir, "sample.ink.json");
        var output = Path.Combine(_tmpDir, "choice.ink.json");
        var result = MakeProcessor().ProcessFile(input, output);
        Assert.False(result.HasErrors, string.Join(", ", result.Errors));

        // Choice text "^Internationalization choice text" is in a str.../str /ev {"*":...}
        // block and must have soft hyphens inserted.
        // Use Ordinal comparison so soft hyphens (U+00AD) are not ignored by culture rules.
        var content = File.ReadAllText(output);
        Assert.DoesNotContain("\"^Internationalization choice text\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void InkJson_RejectsNonInkJson()
    {
        // sample.json is a subtitle array, not compiled Ink — expect an error.
        var input  = Path.Combine(FixturesDir, "sample.json");
        var output = Path.Combine(_tmpDir, "rejected.json");
        var result = MakeProcessor().ProcessFile(input, output);

        Assert.True(result.HasErrors);
    }
}
