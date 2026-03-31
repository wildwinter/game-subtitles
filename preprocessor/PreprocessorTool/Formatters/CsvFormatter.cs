using CsvHelper;
using SimpleVCLib;
using System.Globalization;

namespace GameSubtitles.CLI.Formatters;

internal sealed class CsvFormatter : IFormatter
{
    public void Process(string inputPath, string outputPath, string? fieldName,
        Func<string, string> transform, ProcessingResult result)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            result.AddError("--field is required for CSV files.");
            return;
        }

        var outputEncoding = new System.Text.UTF8Encoding(HasUtf8Bom(inputPath));

        List<string> headers;
        List<Dictionary<string, string>> rows = [];

        using (var reader = new StreamReader(inputPath, System.Text.Encoding.UTF8))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            csv.Read();
            csv.ReadHeader();
            headers = [.. csv.HeaderRecord ?? []];

            if (!headers.Any(h => h.Equals(fieldName, StringComparison.OrdinalIgnoreCase)))
            {
                result.AddError(
                    $"Field '{fieldName}' not found in CSV. Available: {string.Join(", ", headers)}");
                return;
            }

            while (csv.Read())
            {
                var row = new Dictionary<string, string>();
                foreach (var h in headers)
                    row[h] = csv.GetField(h) ?? string.Empty;
                rows.Add(row);
            }
        }

        var actualField = headers.First(h => h.Equals(fieldName, StringComparison.OrdinalIgnoreCase));

        foreach (var row in rows)
        {
            var val = row[actualField];
            if (!string.IsNullOrEmpty(val))
            {
                row[actualField] = transform(val);
                result.IncrementProcessed();
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        var prep = VCLib.PrepareToWrite(outputPath);
        if (!prep.Success)
        {
            result.AddError(prep.Message);
            return;
        }
        using (var writer = new StreamWriter(outputPath, append: false, outputEncoding))
        using (var csv2 = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            foreach (var h in headers) csv2.WriteField(h);
            csv2.NextRecord();

            foreach (var row in rows)
            {
                foreach (var h in headers) csv2.WriteField(row[h]);
                csv2.NextRecord();
            }
        }
        var done = VCLib.FinishedWrite(outputPath);
        if (!done.Success)
            result.AddError(done.Message);
    }

    private static bool HasUtf8Bom(string path)
    {
        Span<byte> buf = stackalloc byte[3];
        using var fs = File.OpenRead(path);
        return fs.Read(buf) == 3 && buf[0] == 0xEF && buf[1] == 0xBB && buf[2] == 0xBF;
    }
}
