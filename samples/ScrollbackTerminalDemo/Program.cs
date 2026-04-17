using Hex1b;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

// State
Hex1bApp? displayApp = null;
TerminalWidgetHandle? activeHandle = null;

using var cts = new CancellationTokenSource();

// Determine shell based on OS
Hex1bTerminalBuilder ConfigureShell(Hex1bTerminalBuilder builder)
{
    if (OperatingSystem.IsWindows())
    {
        return builder.WithPtyProcess(options =>
        {
            options.FileName = "pwsh";
            options.Arguments = ["-NoProfile", "-NoLogo"];
            options.WindowsPtyMode = WindowsPtyMode.RequireProxy;
        });
    }
    return builder.WithPtyProcess("bash", "--norc");
}

// Create the embedded terminal with scrollback enabled
var innerBuilder = Hex1bTerminal.CreateBuilder()
    .WithDimensions(80, 24)
    .WithScrollback(1000); // Enable 1000-line scrollback buffer

innerBuilder = ConfigureShell(innerBuilder);

var innerTerminal = innerBuilder
    .WithTerminalWidget(out var handle)
    .Build();

activeHandle = handle;

// Start the inner terminal in the background
_ = Task.Run(async () =>
{
    try
    {
        await innerTerminal.RunAsync(cts.Token);
        displayApp?.Invalidate();
    }
    catch (OperationCanceledException) { }
});

// Build the widget tree
Hex1bWidget BuildUI(RootContext ctx)
{
    // Get scrollback info for status bar
    var scrollbackCount = activeHandle?.ScrollbackCount ?? 0;
    var terminalNode = displayApp?.FocusedNode as TerminalNode;
    var scrollOffset = terminalNode?.ScrollbackOffset ?? 0;
    var inScrollback = terminalNode?.IsInScrollbackMode ?? false;

    var scrollStatus = inScrollback
        ? $"SCROLLBACK [{scrollOffset}/{scrollbackCount}]"
        : $"LIVE [{scrollbackCount} lines in buffer]";

    var altScreen = activeHandle?.InAlternateScreen == true ? " | ALT SCREEN" : "";
    var mouseTracking = activeHandle?.MouseTrackingEnabled == true ? " | MOUSE" : "";

    return ctx.VStack(v =>
    [
        // Title
        v.Border(
            v.Terminal(handle)
                .WhenNotRunning(args => v.Align(Alignment.Center,
                    v.VStack(vv =>
                    [
                        vv.Text($"Terminal exited with code {args.ExitCode ?? 0}"),
                        vv.Text(""),
                        vv.Button("Quit").OnClick(_ => displayApp?.RequestStop())
                    ])
                ))
        ).Title("Scrollback Terminal Demo").Fill(),

        // Keybinding reference
        v.InfoBar([
            "Shift+↑/↓", "Scroll Line",
            "Shift+PgUp/PgDn", "Scroll Page",
            "Shift+Home/End", "Top/Bottom",
            "Mouse Wheel", "Scroll"
        ]),

        // Status bar
        v.InfoBar([
            "", $"{scrollStatus}{altScreen}{mouseTracking}"
        ])
    ]).WithInputBindings(bindings =>
    {
        bindings.Ctrl().Key(Hex1bKey.Q).Action(_ => displayApp?.RequestStop(), "Quit");
    });
}

// Create the display terminal
await using var displayTerminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((app, options) =>
    {
        displayApp = app;
        return ctx => BuildUI(ctx);
    })
    .Build();

try
{
    await displayTerminal.RunAsync(cts.Token);
}
finally
{
    cts.Cancel();
    await innerTerminal.DisposeAsync();
}
