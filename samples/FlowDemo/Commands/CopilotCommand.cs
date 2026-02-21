using Hex1b;
using Hex1b.Flow;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace FlowDemo.Commands;

/// <summary>
/// Implements the "flowdemo copilot" command — a mock Copilot CLI chat interface.
/// A single persistent slice manages all state: prompt history, spinner, and
/// an optional terminal panel. Type /exit to end, /shell to open a terminal.
/// </summary>
internal static class CopilotCommand
{
    private enum Mode { Normal, Plan, Autopilot }
    private static readonly Mode[] Modes = [Mode.Normal, Mode.Plan, Mode.Autopilot];

    /// <summary>Mutable state bag driving the widget tree on each render.</summary>
    private sealed class AppState
    {
        public Mode CurrentMode = Mode.Normal;
        public List<string> OutputLines = [];
        public bool IsThinking;
        public TerminalWidgetHandle? TerminalHandle;
        public Hex1bTerminal? ChildTerminal;
        public CancellationTokenSource? TerminalCts;
        public Task? TerminalTask;
    }

    public static async Task RunAsync()
    {
        var cursorRow = Console.GetCursorPosition().Top;
        var state = new AppState();

        await Hex1bTerminal.CreateBuilder()
            .WithScrollback()
            .WithHex1bFlow(async flow =>
            {
                await flow.SliceAsync(
                    configure: app =>
                    {
                        return ctx =>
                        {
                            var modeColor = GetModeColor(state.CurrentMode);
                            var modeText = GetModeText(state.CurrentMode);

                            // Build the content area (output lines + optional spinner)
                            var contentWidgets = new List<Hex1bWidget>();
                            foreach (var line in state.OutputLines)
                            {
                                contentWidgets.Add(ctx.Text(line));
                            }
                            if (state.IsThinking)
                            {
                                contentWidgets.Add(ctx.ThemePanel(
                                    theme => theme.Set(SpinnerTheme.ForegroundColor, Hex1bColor.Magenta),
                                    ctx.HStack(h => [h.Spinner(), h.Text(" Thinking...")])));
                            }

                            // Content panel (scrollable output area)
                            var contentPanel = ctx.VStack(_ =>
                            {
                                if (contentWidgets.Count == 0)
                                    return [ctx.VStack(__ => []).Fill()];
                                return [ctx.VStack(__ => []).Fill(), .. contentWidgets];
                            });

                            // If terminal is active, show in HSplitter
                            Hex1bWidget mainArea;
                            if (state.TerminalHandle != null)
                            {
                                int termWidth = Console.WindowWidth;
                                mainArea = ctx.HSplitter(
                                    contentPanel,
                                    ctx.Terminal(state.TerminalHandle),
                                    leftWidth: termWidth / 2
                                ).Fill();
                            }
                            else
                            {
                                mainArea = contentPanel.Fill();
                            }

                            // Prompt area (always at bottom)
                            var promptArea = ctx.ThemePanel(
                                theme => theme
                                    .Set(SeparatorTheme.Color, modeColor)
                                    .Set(GlobalTheme.ForegroundColor, modeColor),
                                tv =>
                                [
                                    tv.Separator(),
                                    tv.TextBox().OnSubmit(e =>
                                    {
                                        HandleSubmit(e.Text?.Trim() ?? "", app, state);
                                    })
                                    .WithInputBindings(bindings =>
                                    {
                                        bindings.Shift().Key(Hex1bKey.Tab).Action(actionCtx =>
                                        {
                                            int idx = Array.IndexOf(Modes, state.CurrentMode);
                                            state.CurrentMode = Modes[(idx + 1) % Modes.Length];
                                            actionCtx.Invalidate();
                                        }, "Cycle mode");
                                    }),
                                    tv.Separator(),
                                    tv.Text(modeText),
                                ]
                            );

                            return ctx.VStack(v => [mainArea, promptArea]);
                        };
                    },
                    options: new Hex1bFlowSliceOptions { EnableMouse = true }
                );

                // Cleanup terminal if still running
                if (state.TerminalCts != null)
                {
                    state.TerminalCts.Cancel();
                    if (state.TerminalTask != null)
                        try { await state.TerminalTask; } catch (OperationCanceledException) { }
                    if (state.ChildTerminal != null)
                        await state.ChildTerminal.DisposeAsync();
                }
            }, options => options.InitialCursorRow = cursorRow)
            .Build()
            .RunAsync();
    }

    private static void HandleSubmit(string text, Hex1bApp app, AppState state)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (text == "/exit")
        {
            app.RequestStop();
            return;
        }

        if (text == "/shell")
        {
            int termWidth = Console.WindowWidth;
            var newTerminal = Hex1bTerminal.CreateBuilder()
                .WithDimensions(termWidth / 2, 20)
                .WithPtyProcess("bash")
                .WithTerminalWidget(out var handle)
                .Build();

            state.TerminalHandle = handle;
            state.ChildTerminal = newTerminal;
            state.TerminalCts = new CancellationTokenSource();

            var cts = state.TerminalCts;
            state.TerminalTask = Task.Run(async () =>
            {
                await newTerminal.RunAsync(cts.Token);
            });

            // When shell exits, remove the terminal panel
            var handleRef = handle;
            handle.StateChanged += stateChange =>
            {
                if (stateChange != TerminalState.Running && state.TerminalHandle == handleRef)
                {
                    state.TerminalHandle = null;
                    app.Invalidate();
                }
            };

            app.RequestFocus(n => n is Hex1b.Nodes.TerminalNode);
            app.Invalidate();
            return;
        }

        // Regular prompt — show it in output, start thinking
        state.OutputLines.Add($"  > {text}");
        state.IsThinking = true;
        app.Invalidate();

        _ = Task.Run(async () =>
        {
            await Task.Delay(5000);

            state.IsThinking = false;
            state.OutputLines.AddRange(GenerateMockResponse(text));
            app.Invalidate();
        });
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
}
