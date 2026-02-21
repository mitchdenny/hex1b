using Hex1b;
using Hex1b.Flow;
using Hex1b.Input;
using Hex1b.Theming;
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

    public static async Task RunAsync()
    {
        var cursorRow = Console.GetCursorPosition().Top;
        var currentMode = Mode.Normal;

        await Hex1bTerminal.CreateBuilder()
            .WithScrollback()
            .WithHex1bFlow(async flow =>
            {
                string? pendingPrompt = null;

                while (true)
                {
                    string? submittedText = pendingPrompt;
                    pendingPrompt = null;

                    // Show the prompt unless we already have a pending one
                    if (submittedText == null)
                    {
                        await flow.SliceAsync(
                            builder: ctx =>
                            {
                                var modeColor = GetModeColor(currentMode);
                                var modeText = GetModeText(currentMode);

                                return ctx.VStack(v =>
                                [
                                    v.VStack(_ => []).Fill(),
                                    v.ThemePanel(
                                        theme => theme
                                            .Set(SeparatorTheme.Color, modeColor)
                                            .Set(GlobalTheme.ForegroundColor, modeColor),
                                        tv =>
                                        [
                                            tv.Separator(),
                                            tv.TextBox().OnSubmit(e =>
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
                                            tv.Separator(),
                                            tv.Text(modeText),
                                        ]
                                    ),
                                ]);
                            },
                            @yield: ctx => submittedText != null && submittedText != "/exit"
                                ? ctx.Text($"  > {submittedText}")
                                : ctx.Text(""),
                            options: new Hex1bFlowSliceOptions { MaxHeight = 5 }
                        );
                    }

                    if (submittedText == "/exit" || submittedText == null)
                        break;

                    if (submittedText == "/shell")
                    {
                        await RunShellAsync(flow);
                        continue;
                    }

                    // Simulate "thinking" with a magenta spinner
                    await flow.SliceAsync(
                        configure: app =>
                        {
                            // Auto-stop after 5 seconds
                            _ = Task.Run(async () =>
                            {
                                await Task.Delay(5000);
                                app.RequestStop();
                            });

                            return ctx => ctx.ThemePanel(
                                theme => theme.Set(SpinnerTheme.ForegroundColor, Hex1bColor.Magenta),
                                tv => [tv.HStack(h => [h.Spinner(), h.Text(" Thinking...")])]);
                        },
                        options: new Hex1bFlowSliceOptions { MaxHeight = 1 }
                    );

                    // Generate mock response
                    var mockResponse = GenerateMockResponse(submittedText);

                    // Show response with a terminal in a splitter + prompt below
                    string? nextSubmittedText = null;

                    // Create child terminal for the working session
                    int termWidth = Console.WindowWidth;
                    var childTerminal = Hex1bTerminal.CreateBuilder()
                        .WithDimensions(termWidth / 2, 20)
                        .WithPtyProcess("bash")
                        .WithTerminalWidget(out var handle)
                        .Build();

                    using var cts = new CancellationTokenSource();
                    var terminalTask = Task.Run(async () =>
                    {
                        await childTerminal.RunAsync(cts.Token);
                    });

                    await flow.SliceAsync(
                        configure: app =>
                        {
                            handle.StateChanged += state =>
                            {
                                if (state != TerminalState.Running)
                                    app.Invalidate();
                            };

                            // Focus the terminal widget initially
                            app.RequestFocus(n => n is Hex1b.Nodes.TerminalNode);

                            return ctx =>
                            {
                                var modeColor = GetModeColor(currentMode);
                                var modeText = GetModeText(currentMode);

                                var responsePanel = ctx.VStack(rv =>
                                    mockResponse.Select(line => rv.Text(line)).ToArray()
                                );

                                var terminalPanel = ctx.Terminal(handle);

                                return ctx.VStack(v =>
                                [
                                    v.HSplitter(responsePanel, terminalPanel, leftWidth: termWidth / 2).Fill(),
                                    v.ThemePanel(
                                        theme => theme
                                            .Set(SeparatorTheme.Color, modeColor)
                                            .Set(GlobalTheme.ForegroundColor, modeColor),
                                        tv =>
                                        [
                                            tv.Separator(),
                                            tv.TextBox().OnSubmit(e =>
                                            {
                                                nextSubmittedText = e.Text;
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
                                            tv.Separator(),
                                            tv.Text(modeText),
                                        ]
                                    ),
                                ]);
                            };
                        },
                        @yield: ctx => nextSubmittedText != null && nextSubmittedText != "/exit"
                            ? ctx.VStack(v =>
                            [
                                .. mockResponse.Select(line => v.Text(line)),
                                v.Text($"  > {nextSubmittedText}"),
                            ])
                            : ctx.VStack(v =>
                                mockResponse.Select(line => v.Text(line)).ToArray()
                            ),
                        options: new Hex1bFlowSliceOptions { EnableMouse = true }
                    );

                    // Clean up terminal
                    cts.Cancel();
                    try { await terminalTask; } catch (OperationCanceledException) { }
                    await childTerminal.DisposeAsync();

                    // If user typed something in the splitter prompt, process it
                    if (nextSubmittedText == "/exit")
                        break;

                    if (nextSubmittedText == "/shell")
                    {
                        await RunShellAsync(flow);
                        continue;
                    }

                    if (nextSubmittedText != null)
                    {
                        pendingPrompt = nextSubmittedText;
                    }
                }
            }, options => options.InitialCursorRow = cursorRow)
            .Build()
            .RunAsync();
    }

    private static Hex1bColor GetModeColor(Mode mode) => mode switch
    {
        Mode.Autopilot => Hex1bColor.FromRgb(0, 187, 0),
        Mode.Plan => Hex1bColor.FromRgb(59, 130, 246),
        _ => Hex1bColor.Default,
    };

    private static string GetModeText(Mode mode) => mode switch
    {
        Mode.Autopilot => " autopilot · shift+tab switch mode · ctrl+s run command",
        Mode.Plan => " plan · shift+tab switch mode",
        _ => " normal · shift+tab switch mode",
    };

    private static string[] GenerateMockResponse(string prompt)
    {
        return [
            "",
            $"  🟢 \x1b[1mResponse to: {prompt}\x1b[0m",
            "",
            "  I've analyzed your request and here's what I found:",
            "",
            "  • Created src/Components/AuthService.cs with JWT validation",
            "  • Updated Program.cs to register the auth middleware",
            "  • Added unit tests in tests/AuthServiceTests.cs",
            "",
            "  All changes have been applied to your workspace.",
            "",
        ];
    }

    private static async Task RunShellAsync(Hex1bFlowContext flow)
    {
        var scrollbackLines = new List<string>();

        int termWidth = Console.WindowWidth;
        int termHeight = Console.WindowHeight;

        var childTerminal = Hex1bTerminal.CreateBuilder()
            .WithDimensions(termWidth, termHeight)
            .WithScrollback(1000, args =>
            {
                scrollbackLines.Add(CellsToString(args.Cells.Span));
            })
            .WithPtyProcess("bash")
            .WithTerminalWidget(out var handle)
            .Build();

        using var cts = new CancellationTokenSource();
        var terminalTask = Task.Run(async () =>
        {
            await childTerminal.RunAsync(cts.Token);
        });

        Hex1bApp? sliceApp = null;
        List<string>? capturedLines = null;

        handle.StateChanged += state =>
        {
            if (state != TerminalState.Running)
            {
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
