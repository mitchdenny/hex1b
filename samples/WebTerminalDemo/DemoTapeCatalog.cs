using Hex1b.Automation;

namespace WebTerminalDemo;

internal sealed class DemoTapeCatalog
{
    private readonly DemoTape[] _tapes;

    public DemoTapeCatalog(string contentRoot)
    {
        List<DemoTapeInfo> entries =
        [
            new("hello", "shell", "Hello, Tape", "Type a command in the existing shell and display its output."),
            new("line-editing", "shell", "Editing and history", "Correct a command with Backspace, then replay it with Up.")
        ];
        if (!OperatingSystem.IsWindows())
        {
            entries.Add(new("ansi-colors", "shell", "ANSI colors", "Use the shell's printf command to display colored text."));
            entries.Add(new("shell-integration", "shell", "Shell integration",
                "Use printf to emit OSC 7 working-directory and OSC 133 command-mark sequences across a few commands."));
        }

        var options = new TapeParserOptions();
        options.SyntaxExtensions.Remove("Source");
        options.SyntaxExtensions.Remove("Output");
        var parser = new TapeParser(options);
        _tapes = entries.Select(info =>
        {
            var path = Path.Combine("Tapes", info.Scene, $"{info.Id}.tape");
            return new DemoTape(info, parser.Parse(File.ReadAllText(Path.Combine(contentRoot, path)), path));
        }).ToArray();
    }

    public DemoTape[] ForScene(string scene) => _tapes.Where(tape => tape.Info.Scene == scene).ToArray();
}
