using GameSubtitles.CLI.Formatters;
using GameSubtitles.Lib;

namespace GameSubtitles.CLI;

internal sealed class FileProcessor
{
    private static readonly Dictionary<string, IFormatter> Formatters =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".po"]   = new PoFormatter(),
            [".csv"]  = new CsvFormatter(),
            [".json"] = new JsonFormatter(),
            [".xlsx"] = new XlsxFormatter(),
        };

    private readonly SubtitlePreprocessor _preprocessor;
    private readonly string? _languageCode;
    private readonly string? _fieldName;

    public FileProcessor(SubtitlePreprocessor preprocessor, string? languageCode, string? fieldName)
    {
        _preprocessor = preprocessor;
        _languageCode = languageCode;
        _fieldName = fieldName;
    }

    public ProcessingResult ProcessFile(string inputPath, string outputPath)
    {
        var result = new ProcessingResult();
        var ext = Path.GetExtension(inputPath);

        if (!Formatters.TryGetValue(ext, out var formatter))
        {
            result.AddError($"Unsupported file format '{ext}' for: {inputPath}");
            return result;
        }

        Func<string, string> transform = text => _preprocessor.Process(text, _languageCode);
        formatter.Process(inputPath, outputPath, _fieldName, transform, result);
        return result;
    }

    public IReadOnlyList<(string File, ProcessingResult Result)> ProcessFolder(
        string inputFolder, string outputFolder)
    {
        var results = new List<(string, ProcessingResult)>();

        var files = Directory
            .EnumerateFiles(inputFolder, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => Formatters.ContainsKey(Path.GetExtension(f)));

        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(inputFolder, file);
            var outPath = Path.Combine(outputFolder, relative);
            results.Add((relative, ProcessFile(file, outPath)));
        }

        return results;
    }
}
