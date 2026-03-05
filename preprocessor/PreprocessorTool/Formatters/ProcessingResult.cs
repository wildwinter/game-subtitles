namespace GameSubtitles.CLI.Formatters;

internal sealed class ProcessingResult
{
    private readonly List<string> _warnings = [];
    private readonly List<string> _errors = [];

    public int ProcessedCount { get; private set; }
    public bool Skipped { get; private set; }
    public IReadOnlyList<string> Warnings => _warnings;
    public IReadOnlyList<string> Errors => _errors;
    public bool HasErrors => _errors.Count > 0;
    public bool HasWarnings => _warnings.Count > 0;

    public void AddWarning(string message) => _warnings.Add(message);
    public void AddError(string message) => _errors.Add(message);
    public void IncrementProcessed() => ProcessedCount++;
    public void MarkSkipped() => Skipped = true;
}
