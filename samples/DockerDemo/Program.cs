using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

const int TerminalCount = 5;
const int TerminalWidth = 80;
const int TerminalHeight = 24;

using var cts = new CancellationTokenSource();
Hex1bApp? displayApp = null;

// Create 5 Docker container terminals
var sessions = new List<(Hex1bTerminal Terminal, TerminalWidgetHandle Handle, int Id)>();

for (int i = 0; i < TerminalCount; i++)
{
    var terminal = Hex1bTerminal.CreateBuilder()
        .WithDimensions(TerminalWidth, TerminalHeight)
        .WithDockerContainer(c =>
        {
            c.Image = "ubuntu:24.04";
            c.Shell = "/bin/bash";
            c.ShellArgs = ["--norc"];
        })
        .WithTerminalWidget(out var handle)
        .Build();

    handle.WindowTitleChanged += _ => displayApp?.Invalidate();
    sessions.Add((terminal, handle, i + 1));
}

// Start all containers in the background
foreach (var (terminal, _, id) in sessions)
{
    _ = Task.Run(async () =>
    {
        try
        {
            await terminal.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    });
}

// Build the TUI app
await using var app = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((appRef, options) =>
    {
        displayApp = appRef;

        return ctx =>
            ctx.VStack(v =>
            [
                v.Text(" Docker Demo — 5 Ubuntu containers (← → to scroll, Tab to switch focus, Ctrl+C to exit)")
                    .ContentHeight(),
                v.HScrollPanel(h =>
                    sessions.Select(s =>
                        (Hex1bWidget)h.Border(
                            h.Terminal(s.Handle)
                                .WhenNotRunning(args =>
                                    h.Text($"Container {s.Id} exited (code {args.ExitCode ?? 0})"))
                                .Fill()
                        )
                        .Title($"Container {s.Id}")
                        .FixedWidth(TerminalWidth + 2) // +2 for border
                        .FillHeight()
                    ).ToArray()
                ).Fill()
            ]);
    })
    .Build();

var exitCode = await app.RunAsync(cts.Token);

// Clean up all container terminals
foreach (var (terminal, _, _) in sessions)
{
    await terminal.DisposeAsync();
}

return exitCode;
