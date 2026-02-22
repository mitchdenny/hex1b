using Hex1b;
using Hex1b.Flow;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;
using GitReader.Structures;

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
        public bool ShowCommands;
        public TerminalWidgetHandle? TerminalHandle;
        public Hex1bTerminal? ChildTerminal;
        public CancellationTokenSource? TerminalCts;
        public Task? TerminalTask;
    }

    private static readonly (string Name, string Description)[] Commands =
    [
        ("/explain", "Explain code or a concept"),
        ("/fix", "Fix a bug or issue in the code"),
        ("/test", "Generate unit tests for code"),
        ("/docs", "Generate documentation"),
        ("/review", "Review code for issues"),
        ("/commit", "Generate a commit message"),
        ("/shell", "Open an inline bash shell"),
        ("/exit", "Exit the Copilot CLI"),
    ];

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
                        app.RequestFocus(n => n is TextBoxNode);
                        return ctx =>
                        {
                            var modeColor = GetModeColor(state.CurrentMode);

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
                            var contentPanel = ctx.VScrollPanel(sv =>
                            {
                                if (contentWidgets.Count == 0)
                                    return [sv.VStack(__ => []).Fill()];
                                return [sv.VStack(__ => []).Fill(), .. contentWidgets];
                            }, showScrollbar: false).Follow();

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

                            // Info bar above prompt: folder on left, model on right
                            var currentFolder = Environment.CurrentDirectory;
                            var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                            var displayPath = currentFolder.StartsWith(homePath, StringComparison.Ordinal)
                                ? "~" + currentFolder[homePath.Length..]
                                : currentFolder;
                            var branchName = GetGitBranch(currentFolder);
                            var folderDisplay = branchName is not null
                                ? $" {displayPath}[⎇ {branchName}]"
                                : $" {displayPath}";
                            var infoBar = ctx.HStack(h => [
                                h.Text(folderDisplay).FillWidth(),
                                h.Text("gpt-4o "),
                            ]);

                            // Prompt area (always at bottom)
                            var modeAnsi = GetModeAnsiText(state.CurrentMode);
                            var modeColorAnsi = GetModeColorAnsi(state.CurrentMode);
                            var promptArea = ctx.VStack(pv =>
                            [
                                infoBar,
                                pv.ThemePanel(
                                    theme => theme.Set(SeparatorTheme.Color, modeColor),
                                    ctx.Separator()
                                ),
                                pv.ThemePanel(
                                    theme => theme
                                        .Set(TextBoxTheme.LeftBracket, $"{modeColorAnsi}❯ \x1b[0m")
                                        .Set(TextBoxTheme.RightBracket, ""),
                                    ctx.TextBox().OnSubmit(e =>
                                    {
                                        var text = e.Text?.Trim() ?? "";
                                        if (string.IsNullOrEmpty(text))
                                            return;
                                        e.Node.Text = "";
                                        state.ShowCommands = false;
                                        HandleSubmit(text, app, state);
                                    })
                                    .WithInputBindings(bindings =>
                                    {
                                        bindings.Shift().Key(Hex1bKey.Tab).Action(actionCtx =>
                                        {
                                            int idx = Array.IndexOf(Modes, state.CurrentMode);
                                            state.CurrentMode = Modes[(idx + 1) % Modes.Length];
                                            actionCtx.Invalidate();
                                        }, "Cycle mode");
                                        bindings.Ctrl().Key(Hex1bKey.S).Action(actionCtx =>
                                        {
                                            state.ShowCommands = !state.ShowCommands;
                                            actionCtx.Invalidate();
                                        }, "Toggle commands");
                                    })
                                ),
                                pv.ThemePanel(
                                    theme => theme.Set(SeparatorTheme.Color, modeColor),
                                    ctx.Separator()
                                ),
                                pv.Text(modeAnsi),
                                .. (state.ShowCommands
                                    ? Commands.Select(c => pv.Text($"  {modeColorAnsi}{c.Name}\x1b[0m  {c.Description}")).ToArray()
                                    : []),
                            ]);

                            return ctx.VStack(v => [mainArea, promptArea]);
                        };
                    },
                    options: new Hex1bFlowSliceOptions { EnableMouse = true },
                    @yield: ctx => state.OutputLines.Count > 0
                        ? ctx.VStack(v =>
                            state.OutputLines.Select(line => v.Text(line)).ToArray()
                        )
                        : ctx.Text("")
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

        if (text == "/shell" || text.StartsWith('!'))
        {
            var stayOpen = text.StartsWith("!!");
            var shellCommand = text.StartsWith('!') ? text[(stayOpen ? 2 : 1)..].Trim() : null;
            int termWidth = Console.WindowWidth;
            var builder = Hex1bTerminal.CreateBuilder()
                .WithDimensions(termWidth / 2, 20)
                .WithTerminalWidget(out var handle);
            if (shellCommand is not null && stayOpen)
                builder = builder.WithPtyProcess("bash", "-c", $"{shellCommand}; exec bash");
            else if (shellCommand is not null)
                builder = builder.WithPtyProcess("bash", "-c", shellCommand);
            else
                builder = builder.WithPtyProcess("bash");
            var newTerminal = builder.Build();

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
                    app.RequestFocus(n => n is TextBoxNode);
                    app.Invalidate();
                }
            };

            app.RequestFocus(n => n is Hex1b.Nodes.TerminalNode);
            app.Invalidate();
            return;
        }

        // Regular prompt — show it in output, start thinking
        state.OutputLines.Add($"❯ {text}");
        state.IsThinking = true;
        app.Invalidate();

        _ = Task.Run(async () =>
        {
            await Task.Delay(250);

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

    private static string? GetGitBranch(string directory)
    {
        try
        {
            // Use GitReader to read the branch — handles worktrees, bare repos, etc.
            using var repo = GitReader.Repository.Factory.OpenStructureAsync(directory)
                .GetAwaiter().GetResult();
            return repo.Head?.Name;
        }
        catch
        {
            return null;
        }
    }

    private static string GetModeColorAnsi(Mode mode) => mode switch
    {
        Mode.Autopilot => "\x1b[38;2;0;187;0m",
        Mode.Plan => "\x1b[38;2;59;130;246m",
        _ => "\x1b[37m",
    };

    private static string GetModeAnsiText(Mode mode)
    {
        const string boldWhite = "\x1b[1;37m";
        const string reset = "\x1b[0m";

        var (label, colorAnsi) = mode switch
        {
            Mode.Autopilot => ("autopilot", "\x1b[38;2;0;187;0m"),
            Mode.Plan => ("plan", "\x1b[38;2;59;130;246m"),
            _ => ("normal", "\x1b[37m"),
        };

        var suffix = mode switch
        {
            Mode.Autopilot => $" {boldWhite}· shift+tab switch mode · ctrl+s run command{reset}",
            Mode.Plan => $" {boldWhite}· shift+tab switch mode{reset}",
            _ => $" {boldWhite}· shift+tab switch mode{reset}",
        };

        return $" {colorAnsi}{label}{reset}{suffix}";
    }

    private static string[] GenerateMockResponse(string prompt)
    {
        return [
            "",
            $"\x1b[32m●\x1b[0m \x1b[1mResponse to: {prompt}\x1b[0m",
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
