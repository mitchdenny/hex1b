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
            new("line-editing", "shell", "Editing and history", "Correct a command with Backspace, then replay it with Up."),
            new("exit-success", "shell", "Exit successfully (0)", "Print final output and exit the real shell with code 0. Ends every attached view."),
            new("exit-error", "shell", "Exit with an error (7)", "Print final output and exit the real shell with code 7. Ends every attached view.")
        ];
        if (!OperatingSystem.IsWindows())
        {
            entries.Add(new("ansi-colors", "shell", "ANSI colors", "Use the shell's printf command to display colored text."));
            entries.Add(new("named-colors", "shell", "Named color swatches",
                "Display all 16 ANSI colors as background swatches and colored text, then compare light/dark palettes."));
            entries.Add(new("double-height", "shell", "DEC double-height text",
                "Compare normal, double-width, and paired double-height rows, Unicode, clipping, and autowrap."));
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
