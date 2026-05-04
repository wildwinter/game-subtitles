using SimpleVCLib;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GameSubtitles.CLI.Formatters;

internal sealed class InkJsonFormatter : IFormatter
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public void Process(string inputPath, string outputPath, string? fieldName,
        Func<string, string> transform, ProcessingResult result)
    {
        if (fieldName is not null)
            result.AddWarning("--field is not applicable for Ink JSON files and will be ignored.");

        string json;
        try { json = File.ReadAllText(inputPath, System.Text.Encoding.UTF8); }
        catch (Exception ex) { result.AddError($"Failed to read file: {ex.Message}"); return; }

        JsonObject root;
        try
        {
            var node = JsonNode.Parse(json);
            if (node is not JsonObject obj || !obj.ContainsKey("inkVersion"))
            {
                result.AddError("File does not appear to be a compiled Ink JSON (no 'inkVersion' key).");
                return;
            }
            root = obj;
        }
        catch (JsonException ex)
        {
            result.AddError($"JSON parse error: {ex.Message}");
            return;
        }

        new InkJsonWalker(transform, result).WalkObject(root);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        var vcResult = VCLib.WriteTextFile(outputPath, root.ToJsonString(WriteOptions),
            System.Text.Encoding.UTF8);
        if (!vcResult.Success)
            result.AddError(vcResult.Message);
    }

    /// <summary>
    /// Peeks at the start of a JSON file to check for the Ink compiled JSON marker.
    /// </summary>
    public static bool IsInkJson(string filePath)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var buf = new byte[256];
            int read = fs.Read(buf, 0, buf.Length);
            // Skip UTF-8 BOM if present.
            int start = (read >= 3 && buf[0] == 0xEF && buf[1] == 0xBB && buf[2] == 0xBF) ? 3 : 0;
            return System.Text.Encoding.UTF8.GetString(buf, start, read - start)
                .Contains("\"inkVersion\"");
        }
        catch { return false; }
    }
}

/// <summary>
/// Recursively walks a compiled Ink JSON tree and applies a text transform to all
/// displayable string values.
///
/// Ink JSON text is identified by the "^" prefix. Three categories must be handled:
///   - Bare "^text" in arrays: narrative/dialogue lines — always transformed.
///   - "#" / "/#" delimiters: tag content — skipped.
///   - "str"…"/str" blocks: may be choice text (transform) or string expressions
///     such as comparisons and variable assignments (skip). The block is choice text
///     only when the token immediately after "/str" is "/ev" AND the token after
///     that is a choice-pointer object containing a "*" key.
/// </summary>
file sealed class InkJsonWalker
{
    private readonly Func<string, string> _transform;
    private readonly ProcessingResult _result;
    private int _inTag;

    internal InkJsonWalker(Func<string, string> transform, ProcessingResult result)
    {
        _transform = transform;
        _result = result;
    }

    internal void WalkObject(JsonObject obj)
    {
        foreach (var (_, value) in obj)
        {
            if (value is JsonArray arr)      WalkArray(arr);
            else if (value is JsonObject sub) WalkObject(sub);
        }
    }

    internal void WalkArray(JsonArray arr)
    {
        int i = 0;
        while (i < arr.Count)
        {
            var node = arr[i];

            if (node is JsonArray subArr)    { WalkArray(subArr);   i++; continue; }
            if (node is JsonObject subObj)   { WalkObject(subObj);  i++; continue; }

            if (node is JsonValue val && val.TryGetValue<string>(out var s))
            {
                if (s == "#")       { _inTag++;  i++; continue; }
                else if (s == "/#") { _inTag--;  i++; continue; }
                else if (s == "str")
                {
                    int strEnd = FindStrEnd(arr, i + 1);
                    if (strEnd < 0) { i++; continue; }  // malformed — skip

                    // Choice text: .../str /ev {"*":...}
                    // Any other pattern (operator, VAR=, etc.) is an expression — skip.
                    bool isChoiceText =
                        strEnd + 2 < arr.Count &&
                        arr[strEnd + 1] is JsonValue nxt &&
                        nxt.TryGetValue<string>(out var nxtS) && nxtS == "/ev" &&
                        arr[strEnd + 2] is JsonObject choiceObj &&
                        choiceObj.ContainsKey("*");

                    if (isChoiceText)
                        ProcessRange(arr, i + 1, strEnd - 1);

                    i = strEnd + 1;  // resume after /str
                    continue;
                }
                else if (_inTag == 0 && s.StartsWith('^'))
                {
                    ApplyTransform(arr, i, s);
                }
            }

            i++;
        }
    }

    // Processes only the "^"-prefixed strings within [from..to] of arr (choice text range).
    private void ProcessRange(JsonArray arr, int from, int to)
    {
        for (int k = from; k <= to && k < arr.Count; k++)
        {
            var node = arr[k];
            if (node is JsonValue val && val.TryGetValue<string>(out var s))
            {
                if      (s == "#")  { _inTag++; }
                else if (s == "/#") { _inTag--; }
                else if (_inTag == 0 && s.StartsWith('^'))
                    ApplyTransform(arr, k, s);
            }
            else if (node is JsonArray sub)   WalkArray(sub);
            else if (node is JsonObject sub2) WalkObject(sub2);
        }
    }

    private void ApplyTransform(JsonArray arr, int index, string raw)
    {
        var text = raw.Substring(1);
        var processed = _transform(text);
        if (processed == text) return;
        arr[index] = JsonValue.Create("^" + processed);
        _result.IncrementProcessed();
    }

    // Returns the index of the "/str" that closes the "str" starting at arr[from-1],
    // handling nested "str"…"/str" pairs. Returns -1 if not found.
    private static int FindStrEnd(JsonArray arr, int from)
    {
        int depth = 1;
        for (int j = from; j < arr.Count; j++)
        {
            if (arr[j] is JsonValue v && v.TryGetValue<string>(out var s))
            {
                if      (s == "str")  depth++;
                else if (s == "/str") { if (--depth == 0) return j; }
            }
        }
        return -1;
    }
}
