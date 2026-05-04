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

    private static readonly InkJsonFormatter _inkJsonFormatter = new();

    private readonly SubtitlePreprocessor _preprocessor;
    private readonly string? _languageCode;
    private readonly string? _fieldName;
    private readonly bool _forceOverwrite;

    public FileProcessor(SubtitlePreprocessor preprocessor, string? languageCode, string? fieldName,
        bool forceOverwrite = false)
    {
        _preprocessor = preprocessor;
        _languageCode = languageCode;
        _fieldName = fieldName;
        _forceOverwrite = forceOverwrite;
    }

    public ProcessingResult ProcessFile(string inputPath, string outputPath)
    {
        var result = new ProcessingResult();

        if (!_forceOverwrite && IsUpToDate(inputPath, outputPath))
        {
            result.MarkSkipped();
            return result;
        }

        var ext = Path.GetExtension(inputPath);

        IFormatter? formatter = null;
        if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase) && InkJsonFormatter.IsInkJson(inputPath))
            formatter = _inkJsonFormatter;
        else
            Formatters.TryGetValue(ext, out formatter);

        if (formatter is null)
        {
            result.AddError($"Unsupported file format '{ext}' for: {inputPath}");
            return result;
        }

        Func<string, string> transform = text => _preprocessor.Process(text, _languageCode);
        formatter.Process(inputPath, outputPath, _fieldName, transform, result);
        return result;
    }

    private static bool IsUpToDate(string inputPath, string outputPath) =>
        File.Exists(outputPath) &&
        File.GetLastWriteTimeUtc(outputPath) >= File.GetLastWriteTimeUtc(inputPath);

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
