using Xunit;
using GameSubtitles.Lib;

namespace Preprocessor.Tests;

public class SubtitlePreprocessorTests
{
    private readonly SubtitlePreprocessor _sut = new();

    [Fact]
    public void Process_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _sut.Process(string.Empty, "en_US"));
    }

    [Fact]
    public void Process_NullLanguage_ReturnsOriginal()
    {
        const string text = "Internationalization";
        Assert.Equal(text, _sut.Process(text, null));
    }

    [Fact]
    public void Process_UnknownLanguage_ReturnsOriginal()
    {
        const string text = "Internationalization";
        Assert.Equal(text, _sut.Process(text, "xx_XX"));
    }

    [Fact]
    public void Process_EnglishLongWord_InsertsSoftHyphens()
    {
        const string text = "Internationalization";
        var result = _sut.Process(text, "en_US");
        Assert.Contains('\u00AD', result);
    }

    [Fact]
    public void Process_FrenchLongWord_InsertsSoftHyphens()
    {
        var result = _sut.Process("internationalisation", "fr_FR");
        Assert.Contains('\u00AD', result);
    }

    [Fact]
    public void Process_IcuStyleCode_NormalisedCorrectly()
    {
        // en_GB should resolve to en-gb patterns
        var result = _sut.Process("Internationalisation", "en_GB");
        Assert.Contains('\u00AD', result);
    }

    [Fact]
    public void Process_ShortWord_NotHyphenated()
    {
        const string text = "cat";
        var result = _sut.Process(text, "en_US");
        Assert.DoesNotContain('\u00AD', result);
    }

    [Fact]
    public void SupportedLanguages_ContainsEfigs()
    {
        var langs = SubtitlePreprocessor.SupportedLanguages;
        Assert.Contains("en-us", langs);
        Assert.Contains("fr",    langs);
        Assert.Contains("it",    langs);
        Assert.Contains("de",    langs);
        Assert.Contains("es",    langs);
    }

    [Fact]
    public void Process_AlreadyHyphenated_IsIdempotent()
    {
        const string text = "Internationalization";
        var once  = _sut.Process(text, "en_US");
        var twice = _sut.Process(once, "en_US");
        Assert.Equal(once, twice);
    }
}
