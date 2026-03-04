namespace GameSubtitles.CLI.Formatters;

/// <summary>
/// Reads an input file, applies a transform to the target field, and writes to outputPath.
/// </summary>
internal interface IFormatter
{
    void Process(
        string inputPath,
        string outputPath,
        string? fieldName,
        Func<string, string> transform,
        ProcessingResult result);
}
