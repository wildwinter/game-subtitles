using GameSubtitles.Lib;
using SimpleVCLib;

namespace GameSubtitles.CLI.Formatters;

/// <summary>
/// Compiled Patter bundles (.patterc). Each locale is hyphenated by its own language, so the
/// --lang code is not used, and the bundle is written back byte for byte apart from the
/// changed strings.
/// </summary>
internal sealed class PatterBundleFormatter
{
    private readonly PatterBundleProcessor _processor;

    public PatterBundleFormatter(SubtitlePreprocessor preprocessor) =>
        _processor = new PatterBundleProcessor(preprocessor);

    public void Process(string inputPath, string outputPath, string? fieldName, string? languageCode,
        ProcessingResult result)
    {
        if (fieldName is not null)
            result.AddWarning("--field is not applicable for Patter bundles and will be ignored.");
        if (languageCode is not null)
            result.AddWarning("--lang is not applicable for Patter bundles and will be ignored: " +
                              "each locale is hyphenated by its own language.");

        byte[] input;
        try { input = File.ReadAllBytes(inputPath); }
        catch (Exception ex) { result.AddError($"Failed to read file: {ex.Message}"); return; }

        PatterBundleResult processed;
        try { processed = _processor.Process(input); }
        catch (FormatException ex) { result.AddError(ex.Message); return; }

        if (processed.IdsOnly)
            result.AddNote("IDs-only bundle (localisation.mode \"ids\"): no text to hyphenate, copied unchanged.");
        foreach (var w in processed.Warnings) result.AddWarning(w);
        result.AddProcessed(processed.ProcessedCount);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        var vcResult = VCLib.WriteBinaryFile(outputPath, processed.Utf8Json);
        if (!vcResult.Success)
            result.AddError(vcResult.Message);
    }
}
