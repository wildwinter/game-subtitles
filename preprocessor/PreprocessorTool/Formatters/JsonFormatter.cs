using SimpleVCLib;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GameSubtitles.CLI.Formatters;

internal sealed class JsonFormatter : IFormatter
{
    // UnsafeRelaxedJsonEscaping lets U+00AD and other non-ASCII characters appear
    // as literal UTF-8 in the output rather than being escaped to \u00ad sequences.
    // These are subtitle data files (not web output), so HTML-safety escaping is not needed.
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public void Process(string inputPath, string outputPath, string? fieldName,
        Func<string, string> transform, ProcessingResult result)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            result.AddError("--field is required for JSON files.");
            return;
        }

        var json = File.ReadAllText(inputPath, System.Text.Encoding.UTF8);

        JsonArray array;
        try
        {
            var node = JsonNode.Parse(json);
            if (node is not JsonArray arr)
            {
                result.AddError("JSON input must be an array of objects at the top level.");
                return;
            }
            array = arr;
        }
        catch (JsonException ex)
        {
            result.AddError($"JSON parse error: {ex.Message}");
            return;
        }

        foreach (var item in array)
        {
            if (item is not JsonObject obj) continue;

            var key = obj.Select(kv => kv.Key)
                         .FirstOrDefault(k => k.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
            if (key is null) continue;

            var val = obj[key]?.GetValue<string>();
            if (string.IsNullOrEmpty(val)) continue;

            obj[key] = transform(val);
            result.IncrementProcessed();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        var vcResult = VCLib.WriteTextFile(outputPath, array.ToJsonString(WriteOptions), System.Text.Encoding.UTF8);
        if (!vcResult.Success)
            result.AddError(vcResult.Message);
    }
}
