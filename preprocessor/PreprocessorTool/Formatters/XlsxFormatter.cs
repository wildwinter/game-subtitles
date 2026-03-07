using ClosedXML.Excel;

namespace GameSubtitles.CLI.Formatters;

internal sealed class XlsxFormatter : IFormatter
{
    public void Process(string inputPath, string outputPath, string? fieldName,
        Func<string, string> transform, ProcessingResult result)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            result.AddError("--field is required for XLSX files.");
            return;
        }

        using var workbook = new XLWorkbook(inputPath);
        var ws = workbook.Worksheet(1);

        var headerRow = ws.FirstRowUsed();
        if (headerRow is null)
        {
            result.AddWarning($"XLSX file has no data: {inputPath}");
            return;
        }

        int? targetCol = null;
        foreach (var cell in headerRow.CellsUsed())
        {
            if (cell.GetString().Equals(fieldName, StringComparison.OrdinalIgnoreCase))
            {
                targetCol = cell.Address.ColumnNumber;
                break;
            }
        }

        if (targetCol is null)
        {
            result.AddError($"Field '{fieldName}' not found in XLSX header row.");
            return;
        }

        var dataRow = headerRow.RowBelow();
        while (dataRow is not null && dataRow.CellsUsed().Any())
        {
            var cell = dataRow.Cell(targetCol.Value);
            var val = cell.GetString();
            if (!string.IsNullOrEmpty(val))
            {
                cell.Value = transform(val);
                result.IncrementProcessed();
            }
            dataRow = dataRow.RowBelow();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        workbook.SaveAs(outputPath);
    }
}
