using System.Text;
using System.Text.Json;
using Xunit;
using GameSubtitles.CLI;
using GameSubtitles.Lib;

namespace Preprocessor.Tests;

/// <summary>
/// Compiled Patter bundles (.patterc): which strings gain soft hyphens, which never do, and that
/// nothing else in the file changes.
/// </summary>
public class PatterBundleTests : IDisposable
{
    private const char SoftHyphen = '­';

    private readonly string _tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly SubtitlePreprocessor _preprocessor = new();
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.patterc");

    public PatterBundleTests() => Directory.CreateDirectory(_tmpDir);

    public void Dispose() => Directory.Delete(_tmpDir, recursive: true);

    private (string OutputPath, GameSubtitles.CLI.Formatters.ProcessingResult Result) ProcessFixture(
        string? lang = null, string? field = null)
    {
        var output = Path.Combine(_tmpDir, "out.patterc");
        var result = new FileProcessor(_preprocessor, lang, field).ProcessFile(FixturePath, output);
        return (output, result);
    }

    private static Dictionary<string, Dictionary<string, string>> Strings(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
        return doc.RootElement.GetProperty("strings").EnumerateObject().ToDictionary(
            locale => locale.Name,
            locale => locale.Value.EnumerateObject().ToDictionary(s => s.Name, s => s.Value.GetString()!));
    }

    private static string Strip(string s) => s.Replace(SoftHyphen.ToString(), "");

    // ── What gains soft hyphens ───────────────────────────────────────────────

    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    public void HyphenatesLinesTextAndChoicesInEachLocale(string locale)
    {
        var (output, result) = ProcessFixture();
        Assert.False(result.HasErrors, string.Join(", ", result.Errors));

        var before = Strings(FixturePath)[locale];
        var after = Strings(output)[locale];
        string[] shown =
        [
            "L_intro",       // a line
            "T_action",      // action text
            "T_shown",       // a text line in a text scene
            "C_line",        // an option's line prompt
            "C_text",        // an option's text prompt
            "L_noprompt",    // the first line of an option without a prompt
            "L_after_line", "L_after_text",
        ];
        foreach (var id in shown)
        {
            Assert.Contains(SoftHyphen, after[id]);
            Assert.Equal(before[id], Strip(after[id]));
        }
    }

    [Fact]
    public void HyphenatesEachLocaleByItsOwnLanguage()
    {
        var (output, _) = ProcessFixture();
        var strings = Strings(output);

        Assert.Equal(_preprocessor.Process(Strings(FixturePath)["fr"]["C_line"], "fr"), strings["fr"]["C_line"]);
        Assert.Equal(_preprocessor.Process(Strings(FixturePath)["en"]["C_line"], "en"), strings["en"]["C_line"]);
    }

    [Fact]
    public void HyphenatesActionTextByTheDefaultLocaleInEveryTable()
    {
        var (output, _) = ProcessFixture();
        var strings = Strings(output);
        var expected = _preprocessor.Process(Strings(FixturePath)["en"]["T_action"], "en");

        Assert.Equal(expected, strings["en"]["T_action"]);
        Assert.Equal(expected, strings["fr"]["T_action"]);
        Assert.Equal(expected, strings["ja"]["T_action"]); // ja has no rules of its own
    }

    [Fact]
    public void HyphenatesClosedCaptionCues()
    {
        var (output, _) = ProcessFixture();
        var intro = Strings(output)["en"]["L_intro"];

        Assert.StartsWith("[", intro);
        Assert.Contains(SoftHyphen, intro[..intro.IndexOf(']')]);
    }

    // ── What never does ───────────────────────────────────────────────────────

    [Fact]
    public void LeavesNamesAndUnreferencedStringsAlone()
    {
        var (output, _) = ProcessFixture();
        var before = Strings(FixturePath);
        var after = Strings(output);

        foreach (var locale in new[] { "en", "fr" })
        {
            foreach (var key in new[] { "cast:TAM", "qualifier:vo", "speaker:TAM:vo", "L_unused", "L_gamedata" })
                Assert.Equal(before[locale][key], after[locale][key]);
        }
    }

    [Fact]
    public void LeavesInterpolationAndFormattingTagsWhole()
    {
        var (output, _) = ProcessFixture();
        var strings = Strings(output);

        foreach (var locale in new[] { "en", "fr" })
        {
            var intro = strings[locale]["L_intro"];
            Assert.Contains("{@player_name}", intro, StringComparison.Ordinal);
            Assert.Contains("<b>", intro, StringComparison.Ordinal);
            Assert.Contains("</b>", intro, StringComparison.Ordinal);

            var shown = strings[locale]["T_shown"];
            Assert.Contains("<i>", shown, StringComparison.Ordinal);
            Assert.Contains("</i>", shown, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LeavesUnsupportedLocalesAloneAndWarns()
    {
        var (output, result) = ProcessFixture();
        var before = Strings(FixturePath)["ja"];
        var after = Strings(output)["ja"];

        foreach (var (id, text) in before)
            if (id != "T_action") Assert.Equal(text, after[id]);
        Assert.Contains(result.Warnings, w => w.Contains("'ja'"));
    }

    [Fact]
    public void ChangesNothingButTheSoftHyphens()
    {
        var (output, result) = ProcessFixture();
        Assert.True(result.ProcessedCount > 0);

        // Removing every soft hyphen gives back the input byte for byte: content.hash, layout,
        // key order and every other string are as they were.
        var input = File.ReadAllBytes(FixturePath);
        var stripped = Encoding.UTF8.GetBytes(Strip(Encoding.UTF8.GetString(File.ReadAllBytes(output))));
        Assert.Equal(input, stripped);
    }

    // ── The CLI around it ─────────────────────────────────────────────────────

    [Fact]
    public void WarnsThatLangDoesNotApply()
    {
        var (_, result) = ProcessFixture(lang: "de");
        Assert.Contains(result.Warnings, w => w.Contains("--lang"));
    }

    [Fact]
    public void CopiesIdsOnlyBundlesUnchanged()
    {
        var input = Path.Combine(_tmpDir, "ids.patterc");
        var output = Path.Combine(_tmpDir, "ids-out.patterc");
        File.WriteAllText(input, """
            {
              "localisation": { "mode": "ids", "sourceDebug": true },
              "locales": { "default": "en", "included": ["en"] },
              "scenes": { "s": { "blocks": [ { "id": "L_a", "kind": "line" } ] } },
              "schema": "patter/bundle@0",
              "strings": { "en": { "L_a": "Internationalization" } }
            }
            """);

        var result = new FileProcessor(_preprocessor, null, null).ProcessFile(input, output);

        Assert.False(result.HasErrors, string.Join(", ", result.Errors));
        Assert.Equal(0, result.ProcessedCount);
        Assert.Contains(result.Notes, n => n.Contains("IDs-only"));
        Assert.Equal(File.ReadAllBytes(input), File.ReadAllBytes(output));
    }

    [Fact]
    public void RejectsAPatterCFileThatIsNotABundle()
    {
        var input = Path.Combine(_tmpDir, "other.patterc");
        File.WriteAllText(input, """{ "schema": "patter/save@0" }""");

        var result = new FileProcessor(_preprocessor, null, null)
            .ProcessFile(input, Path.Combine(_tmpDir, "other-out.patterc"));

        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ProcessesBundlesInAFolder()
    {
        var outDir = Path.Combine(_tmpDir, "folder-out");
        var results = new FileProcessor(_preprocessor, null, null)
            .ProcessFolder(Path.GetDirectoryName(FixturePath)!, outDir);

        Assert.Contains(results, r => r.File == "sample.patterc" && r.Result.ProcessedCount > 0);
    }

    // ── The library ───────────────────────────────────────────────────────────

    [Fact]
    public void RecognisesBundles()
    {
        Assert.True(PatterBundleProcessor.IsPatterBundle(File.ReadAllBytes(FixturePath)));
        Assert.False(PatterBundleProcessor.IsPatterBundle("""{ "inkVersion": 21 }"""u8));
        Assert.False(PatterBundleProcessor.IsPatterBundle("""[1, 2]"""u8));
        Assert.False(PatterBundleProcessor.IsPatterBundle("not json"u8));
    }

    [Theory]
    [InlineData("{@international_organisation}")]               // a slot
    [InlineData("{internationalization}")]                       // braces that aren't a slot
    [InlineData("<b>")] [InlineData("</b>")] [InlineData("<i>")]
    [InlineData("</i>")] [InlineData("<bi>")] [InlineData("</bi>")]
    public void ProcessTextLeavesProtectedSpansWhole(string span)
    {
        var processor = new PatterBundleProcessor(_preprocessor);
        var result = processor.ProcessText($"Extraordinary {span} internationalization", "en");

        Assert.Contains($" {span} ", result, StringComparison.Ordinal);
        Assert.Contains(SoftHyphen, result);
    }

    [Fact]
    public void ProcessTextHyphenatesBetweenEscapedBraces()
    {
        // "{{" and "}}" print literal braces, so the word between them is ordinary text.
        var result = new PatterBundleProcessor(_preprocessor).ProcessText("{{internationalization}}", "en");

        Assert.StartsWith("{{", result, StringComparison.Ordinal);
        Assert.EndsWith("}}", result, StringComparison.Ordinal);
        Assert.Contains(SoftHyphen, result);
    }

    [Fact]
    public void ProcessTextTreatsAnUnclosedBraceAsText()
    {
        var result = new PatterBundleProcessor(_preprocessor).ProcessText("{internationalization", "en");

        Assert.StartsWith("{", result, StringComparison.Ordinal);
        Assert.Contains(SoftHyphen, result);
    }
}
