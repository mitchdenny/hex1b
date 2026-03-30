using System.Text;
using Hex1b;
using Hex1b.Flow;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace FlowWidgetCompatDemo.Scenarios;

internal class TerminalSuccessScenario : IWidgetScenario
{
    private const int TerminalHeight = 5;

    private static readonly string s_command = string.Join(" && ",
        "echo 'Compiling source files...'",
        "sleep 0.5",
        "echo 'Linking objects...'",
        "sleep 0.5",
        "echo 'Running tests...'",
        "sleep 0.5",
        "echo 'All 42 tests passed.'",
        "echo '✓ Build succeeded!'");

    private Hex1bTerminal? _terminal;
    private TerminalWidgetHandle? _handle;
    private bool _started;

    public string Name => "Terminal (Success)";
    public string Description => "Non-interactive terminal running a successful command";
    public int? MaxHeight => 15;

    public Hex1bWidget Build(FlowStepContext ctx)
    {
        EnsureStarted(ctx);

        return ctx.Terminal(_handle!)
            .WhenNotRunning(args =>
            {
                var exitCode = args.ExitCode ?? 0;
                var outputLines = CaptureBuffer(args.Handle);

                return ctx.VStack(v =>
                {
                    var widgets = new List<Hex1bWidget>
                    {
                        v.Text($"✓ Command completed successfully (exit code: {exitCode})"),
                        v.Separator(),
                    };

                    foreach (var line in outputLines)
                        widgets.Add(v.Text(line));

                    return widgets.ToArray();
                });
            })
            .Height(SizeHint.Fixed(TerminalHeight));
    }

    private void EnsureStarted(FlowStepContext ctx)
    {
        if (_started)
            return;

        _started = true;
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithDimensions(80, TerminalHeight)
            .WithPtyProcess("bash", "-c", s_command)
            .WithTerminalWidget(out _handle!)
            .Build();

        _ = Task.Run(async () =>
        {
            try { await _terminal.RunAsync(ctx.CancellationToken); }
            catch (OperationCanceledException) { }
        });
    }

    private static List<string> CaptureBuffer(TerminalWidgetHandle handle)
    {
        var (buffer, width, height) = handle.GetScreenBufferSnapshot();
        var lines = new List<string>();

        for (var y = 0; y < height; y++)
        {
            var sb = new StringBuilder();
            for (var x = 0; x < width; x++)
                sb.Append(buffer[y, x].Character);

            lines.Add(sb.ToString().TrimEnd());
        }

        // Trim trailing empty lines
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            lines.RemoveAt(lines.Count - 1);

        return lines;
    }
}
