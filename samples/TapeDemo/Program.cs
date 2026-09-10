using Hex1b.Automation;
using Hex1b.Layout;

var inputPath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "demo.tape");
var file = new FileInfo(inputPath);
var tape = await new TapeParser().ParseAsync(file);
var capture = args.Length > 1
    ? new TapeCaptureOptions
    {
        AsciinemaPath = Path.GetFullPath(Path.Combine(args[1], "demo.cast")),
        GoldenTextPath = Path.GetFullPath(Path.Combine(args[1], "demo.txt"))
    }
    : null;
var options = new TapePlaybackOptions
{
    WorkingDirectory = file.DirectoryName,
    TerminalSize = new Size(80, 24),
    Capture = capture
};
var player = new TapePlayer();
var validation = await player.ValidateAsync(tape, options: options);
if (!validation.CanExecute)
{
    foreach (var diagnostic in validation.Diagnostics)
        Console.Error.WriteLine($"{diagnostic.Span.SourceName}:{diagnostic.Span.Line}: {diagnostic.Message}");
    Environment.ExitCode = 1;
    return;
}

using var result = await player.PlayAsync(tape, options: options);
Console.WriteLine(result.FinalSnapshot.GetScreenText());
Console.WriteLine($"Completed {result.CompletedCommandCount} commands.");
foreach (var artifact in result.Artifacts)
    Console.WriteLine($"{artifact.Kind}: {artifact.Path}");
