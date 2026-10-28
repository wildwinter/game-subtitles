namespace GameSubtitles.CLI.Formatters;

internal sealed class ProcessingResult
{
    private readonly List<string> _warnings = [];
    private readonly List<string> _errors = [];
    private readonly List<string> _notes = [];

    public int ProcessedCount { get; private set; }
    public bool Skipped { get; private set; }
    public IReadOnlyList<string> Warnings => _warnings;
    public IReadOnlyList<string> Errors => _errors;
    public IReadOnlyList<string> Notes => _notes;
    public bool HasErrors => _errors.Count > 0;
    public bool HasWarnings => _warnings.Count > 0;

    public void AddWarning(string message) => _warnings.Add(message);
    public void AddError(string message) => _errors.Add(message);
    public void AddNote(string message) => _notes.Add(message);
    public void IncrementProcessed() => ProcessedCount++;
    public void AddProcessed(int count) => ProcessedCount += count;
    public void MarkSkipped() => Skipped = true;
}
