using System.CommandLine;
using GameSubtitles.CLI;
using GameSubtitles.CLI.Formatters;
using GameSubtitles.Lib;

// Argument
var inputArg = new Argument<string>("input")
{
    Description = "Input file or folder path",
};

// Options: beta5 signature is Option<T>(name, aliases[])
var outputOpt = new Option<string>("--output", ["-o"])
{
    Description = "Output file or folder path",
    Required = true,
};
var fieldOpt = new Option<string?>("--field", ["-f"])
{
    Description = "Field name to process in CSV / JSON / XLSX files",
};
var langOpt = new Option<string?>("--lang", ["-l"])
{
    Description = "Language code e.g. en_GB, fr_FR (default: en_US)",
};
var forceOverwriteOpt = new Option<bool>("--force-overwrite")
{
    Description = "Always reprocess files even if the output is already up to date",
};

var root = new RootCommand("Game Subtitles \u2014 inserts soft hyphen (U+00AD) markers in localised subtitle strings")
{
    inputArg, outputOpt, fieldOpt, langOpt, forceOverwriteOpt
};

root.SetAction(parseResult =>
{
    var input          = parseResult.GetValue(inputArg)!;
    var output         = parseResult.GetValue(outputOpt)!;
    var field          = parseResult.GetValue(fieldOpt);
    var lang           = parseResult.GetValue(langOpt);
    var forceOverwrite = parseResult.GetValue(forceOverwriteOpt);

    var preprocessor = new SubtitlePreprocessor();
    var processor    = new FileProcessor(preprocessor, lang, field, forceOverwrite);
    int exitCode     = 0;

    if (Directory.Exists(input))
    {
        var results = processor.ProcessFolder(input, output);
        foreach (var (file, r) in results)
        {
            Report(file, r);
            exitCode = Math.Max(exitCode, ExitCode(r));
        }
    }
    else if (File.Exists(input))
    {
        var r = processor.ProcessFile(input, output);
        Report(input, r);
        exitCode = ExitCode(r);
    }
    else
    {
        Console.Error.WriteLine($"ERROR: Input path not found: {input}");
        return 2;
    }

    return exitCode;
});

var config = new CommandLineConfiguration(root);
return await config.InvokeAsync(args);

static void Report(string label, ProcessingResult r)
{
    if (r.Skipped) { Console.WriteLine($"SKIP [{label}]: up to date."); return; }
    foreach (var w in r.Warnings) Console.Error.WriteLine($"WARN [{label}]: {w}");
    foreach (var e in r.Errors)   Console.Error.WriteLine($"ERROR [{label}]: {e}");
    if (!r.HasErrors)
        Console.WriteLine($"OK [{label}]: {r.ProcessedCount} string(s) processed.");
}

static int ExitCode(ProcessingResult r)
{
    if (r.HasErrors)   return 2;
    if (r.HasWarnings) return 1;
    return 0;
}
