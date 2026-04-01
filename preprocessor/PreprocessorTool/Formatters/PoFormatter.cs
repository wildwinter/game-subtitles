using Karambolo.PO;
using SimpleVCLib;
using System.Text.RegularExpressions;

namespace GameSubtitles.CLI.Formatters;

internal sealed class PoFormatter : IFormatter
{
    // Matches POT-Creation-Date and PO-Revision-Date header lines in the generated file,
    // e.g.: "POT-Creation-Date: 2024-01-01 00:00+0000\n"
    // These are always updated by the generator even when content is unchanged, so we
    // strip them before comparing old and new output.
    private static readonly Regex DateHeaderLine = new(
        @"^""P(?:OT-Creation|O-Revision)-Date:[^""]*""$",
        RegexOptions.Multiline);

    private static string StripDates(string content) =>
        DateHeaderLine.Replace(content, string.Empty);

    public void Process(string inputPath, string outputPath, string? fieldName,
        Func<string, string> transform, ProcessingResult result)
    {
        if (fieldName is not null)
            result.AddWarning("--field is not applicable to PO files and will be ignored.");

        POCatalog catalog;
        using (var reader = new StreamReader(inputPath, System.Text.Encoding.UTF8))
        {
            var parser = new POParser();
            var parseResult = parser.Parse(reader);
            if (!parseResult.Success)
            {
                result.AddError($"Failed to parse PO file: {inputPath}");
                foreach (var diag in parseResult.Diagnostics)
                    result.AddError($"  {diag}");
                return;
            }
            catalog = parseResult.Catalog;
        }

        foreach (var entry in catalog.OfType<POSingularEntry>())
        {
            if (string.IsNullOrEmpty(entry.Translation)) continue;
            entry.Translation = transform(entry.Translation);
            result.IncrementProcessed();
        }

        // Generate the new content to a string so we can compare before writing.
        // Must use a UTF-8 StreamWriter — Karambolo rejects StringWriter (UTF-16).
        string newContent;
        using (var ms = new MemoryStream())
        {
            using (var sw = new StreamWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
                new POGenerator().Generate(sw, catalog);
            newContent = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }

        // Skip writing if the output already exists and content is unchanged (ignoring dates).
        if (File.Exists(outputPath))
        {
            var existingContent = File.ReadAllText(outputPath, System.Text.Encoding.UTF8);
            if (StripDates(existingContent) == StripDates(newContent))
            {
                result.MarkSkipped();
                return;
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        var prep = VCLib.PrepareToWrite(outputPath);
        if (!prep.Success)
        {
            result.AddError(prep.Message);
            return;
        }
        using (var writer = new StreamWriter(outputPath, append: false, System.Text.Encoding.UTF8))
            writer.Write(newContent);
        var done = VCLib.FinishedWrite(outputPath);
        if (!done.Success)
            result.AddError(done.Message);
    }
}
