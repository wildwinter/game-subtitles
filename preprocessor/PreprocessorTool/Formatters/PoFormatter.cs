using Karambolo.PO;

namespace GameSubtitles.CLI.Formatters;

internal sealed class PoFormatter : IFormatter
{
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

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        using var writer = new StreamWriter(outputPath, append: false, System.Text.Encoding.UTF8);
        new POGenerator().Generate(writer, catalog);
    }
}
