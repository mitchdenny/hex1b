using Hex1b;
using Hex1b.Automation;
using Hex1b.Layout;
using Hex1b.Widgets;

const int TerminalCount = 5;
const int TerminalWidth = 80;
const int TerminalHeight = 24;

using var cts = new CancellationTokenSource();
Hex1bApp? displayApp = null;

// Define automation sequences that can be applied to any container
var sequences = new (string Label, Func<Hex1bTerminalInputSequence> Build)[]
{
    ("Install .NET 9.0", () => new Hex1bTerminalInputSequenceBuilder()
        .Type("curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 9.0").Enter()
        .Build()),
    ("Install .NET 8.0", () => new Hex1bTerminalInputSequenceBuilder()
        .Type("curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0").Enter()
        .Build()),
    ("Install .NET 10.0 (preview)", () => new Hex1bTerminalInputSequenceBuilder()
        .Type("curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --quality preview").Enter()
        .Build()),
    ("Add dotnet to PATH", () => new Hex1bTerminalInputSequenceBuilder()
        .Type("export PATH=$PATH:$HOME/.dotnet").Enter()
        .Build()),
    ("Update apt packages", () => new Hex1bTerminalInputSequenceBuilder()
        .Type("apt-get update -qq && apt-get install -y -qq curl").Enter()
        .Build()),
    ("Show system info", () => new Hex1bTerminalInputSequenceBuilder()
        .Type("uname -a && cat /etc/os-release").Enter()
        .Build()),
};

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
foreach (var (terminal, _, _) in sessions)
{
    _ = Task.Run(async () =>
    {
        try { await terminal.RunAsync(cts.Token); }
        catch (OperationCanceledException) { }
    });
}

// Build the TUI app with mouse support
await using var app = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((appRef, options) =>
    {
        displayApp = appRef;

        return ctx =>
            ctx.VStack(v =>
            [
                v.Text(" Docker Demo — 5 Ubuntu containers (← → to scroll, Tab/click to switch, Ctrl+C to exit)")
                    .ContentHeight(),
                v.HScrollPanel(h =>
                    sessions.Select(s =>
                        (Hex1bWidget)h.Border(
                            h.VStack(pane =>
                            [
                                pane.SplitButton()
                                    .PrimaryAction($"▶ Run sequence on #{s.Id}", e =>
                                        RunSequence(s.Terminal, 0))
                                    .SecondaryAction(sequences[0].Label, e => RunSequence(s.Terminal, 0))
                                    .SecondaryAction(sequences[1].Label, e => RunSequence(s.Terminal, 1))
                                    .SecondaryAction(sequences[2].Label, e => RunSequence(s.Terminal, 2))
                                    .SecondaryAction(sequences[3].Label, e => RunSequence(s.Terminal, 3))
                                    .SecondaryAction(sequences[4].Label, e => RunSequence(s.Terminal, 4))
                                    .SecondaryAction(sequences[5].Label, e => RunSequence(s.Terminal, 5))
                                    .ContentHeight(),
                                pane.Terminal(s.Handle)
                                    .WhenNotRunning(args =>
                                        pane.Text($"Container {s.Id} exited (code {args.ExitCode ?? 0})"))
                                    .Fill()
                            ])
                        )
                        .Title($"Container {s.Id}")
                        .FixedWidth(TerminalWidth + 2)
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

void RunSequence(Hex1bTerminal terminal, int index)
{
    _ = Task.Run(async () =>
    {
        var seq = sequences[index].Build();
        await seq.ApplyAsync(terminal, cts.Token);
    });
}
