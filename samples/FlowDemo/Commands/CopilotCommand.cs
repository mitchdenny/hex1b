using Hex1b;
using Hex1b.Flow;
using Hex1b.Input;
using Hex1b.Widgets;

namespace FlowDemo.Commands;

/// <summary>
/// Implements the "flowdemo copilot" command — a mock Copilot CLI chat interface.
/// Each prompt/response pair is yielded as frozen output that scrolls up naturally.
/// Type /exit to end the session, /shell to launch an interactive bash shell.
/// Shift+Tab cycles between modes (normal/plan/autopilot).
/// </summary>
internal static class CopilotCommand
{
    private enum Mode { Normal, Plan, Autopilot }

    private static readonly Mode[] Modes = [Mode.Normal, Mode.Plan, Mode.Autopilot];

    private static string ModeLabel(Mode mode) => mode switch
    {
        Mode.Normal => "normal",
        Mode.Plan => "plan",
        Mode.Autopilot => "autopilot",
        _ => "normal",
    };

    public static async Task RunAsync()
    {
        var cursorRow = Console.GetCursorPosition().Top;
        var currentMode = Mode.Normal;

        await Hex1bTerminal.CreateBuilder()
            .WithScrollback()
            .WithHex1bFlow(async flow =>
            {
                while (true)
                {
                    string? submittedText = null;

                    await flow.SliceAsync(
                        builder: ctx => ctx.VStack(v =>
                        [
                            v.VStack(_ => []).Fill(),
                            v.Separator(),
                            v.TextBox().OnSubmit(e =>
                            {
                                submittedText = e.Text;
                                e.Context.RequestStop();
                            })
                            .WithInputBindings(bindings =>
                            {
                                bindings.Shift().Key(Hex1bKey.Tab).Action(actionCtx =>
                                {
                                    int idx = Array.IndexOf(Modes, currentMode);
                                    currentMode = Modes[(idx + 1) % Modes.Length];
                                    actionCtx.Invalidate();
                                }, "Cycle mode");
                            }),
                            v.Separator(),
                            v.Text($"  Mode: {ModeLabel(currentMode)}  (Shift+Tab to change)"),
                        ]),
                        @yield: ctx => submittedText != null && submittedText != "/exit"
                            ? ctx.VStack(v =>
                            [
                                v.Text($"  > {submittedText}"),
                                v.Text($"  Echo: {submittedText}"),
                            ])
                            : ctx.Text(""),
                        options: new Hex1bFlowSliceOptions { MaxHeight = 5 }
                    );

                    if (submittedText == "/exit" || submittedText == null)
                        break;

                    if (submittedText == "/shell")
                    {
                        await RunShellAsync(flow);
                    }
                }
            }, options => options.InitialCursorRow = cursorRow)
            .Build()
            .RunAsync();
    }

    private static async Task RunShellAsync(Hex1bFlowContext flow)
    {
        // Capture scrollback lines as they scroll off
        var scrollbackLines = new List<string>();

        int termWidth = Console.WindowWidth;
        int termHeight = Console.WindowHeight;

        // Create child terminal with bash, scrollback capture, and widget handle
        var childTerminal = Hex1bTerminal.CreateBuilder()
            .WithDimensions(termWidth, termHeight)
            .WithScrollback(1000, args =>
            {
                scrollbackLines.Add(CellsToString(args.Cells.Span));
            })
            .WithPtyProcess("bash")
            .WithTerminalWidget(out var handle)
            .Build();

        // Run the child terminal in the background
        using var cts = new CancellationTokenSource();
        var terminalTask = Task.Run(async () =>
        {
            await childTerminal.RunAsync(cts.Token);
        });

        // Build the yield content from scrollback + screen buffer
        Hex1bApp? sliceApp = null;
        List<string>? capturedLines = null;

        handle.StateChanged += state =>
        {
            if (state != TerminalState.Running)
            {
                // Capture screen buffer immediately when process exits
                var (buf, w, h) = handle.GetScreenBufferSnapshot();
                var screenLines = new List<string>();
                for (int row = 0; row < h; row++)
                {
                    var rowCells = new TerminalCell[w];
                    for (int col = 0; col < w; col++)
                        rowCells[col] = buf[row, col];
                    screenLines.Add(CellsToString(rowCells));
                }

                var allLines = new List<string>(scrollbackLines);
                allLines.AddRange(screenLines);

                // Trim trailing blank lines
                while (allLines.Count > 0 && string.IsNullOrWhiteSpace(allLines[^1]))
                    allLines.RemoveAt(allLines.Count - 1);

                capturedLines = allLines;
                sliceApp?.RequestStop();
            }
        };

        await flow.SliceAsync(
            configure: app =>
            {
                sliceApp = app;
                return ctx => ctx.Terminal(handle).Fill();
            },
            @yield: ctx =>
            {
                if (capturedLines == null || capturedLines.Count == 0)
                    return ctx.Text("  (shell exited)");

                return ctx.VStack(v =>
                    capturedLines.Select(line => v.Text(line)).ToArray()
                );
            },
            options: new Hex1bFlowSliceOptions { EnableMouse = true }
        );

        // Cancel the terminal task if still running
        cts.Cancel();
        try { await terminalTask; } catch (OperationCanceledException) { }
        await childTerminal.DisposeAsync();
    }

    private static string CellsToString(ReadOnlySpan<TerminalCell> cells)
    {
        var sb = new System.Text.StringBuilder(cells.Length);
        for (int i = 0; i < cells.Length; i++)
            sb.Append(cells[i].Character);
        return sb.ToString().TrimEnd();
    }
}
