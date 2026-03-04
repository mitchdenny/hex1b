using Hex1b;
using Hex1b.Automation;
using Hex1b.Layout;
using Hex1b.Widgets;

const int TerminalWidth = 80;
const int TerminalHeight = 24;

using var cts = new CancellationTokenSource();
Hex1bApp? displayApp = null;

// Available Docker images
var images = new ImageDragData[]
{
    new("Ubuntu 24.04", "ubuntu:24.04"),
    new("Alpine 3.21", "alpine:3.21"),
    new(".NET SDK 10.0", "mcr.microsoft.com/dotnet/sdk:10.0"),
    new(".NET SDK 9.0", "mcr.microsoft.com/dotnet/sdk:9.0"),
    new("Debian Bookworm", "debian:bookworm-slim"),
};

// Available automation sequences
var sequences = new SequenceDragData[]
{
    new("Install .NET 9.0", () => new Hex1bTerminalInputSequenceBuilder()
        .Type("curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 9.0").Enter()
        .Build()),
    new("Install .NET 8.0", () => new Hex1bTerminalInputSequenceBuilder()
        .Type("curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0").Enter()
        .Build()),
    new("Install .NET 10.0", () => new Hex1bTerminalInputSequenceBuilder()
        .Type("curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --quality preview").Enter()
        .Build()),
    new("Add dotnet to PATH", () => new Hex1bTerminalInputSequenceBuilder()
        .Type("export PATH=$PATH:$HOME/.dotnet").Enter()
        .Build()),
    new("apt update", () => new Hex1bTerminalInputSequenceBuilder()
        .Type("apt-get update -y").Enter()
        .Build()),
    new("apt install curl", () => new Hex1bTerminalInputSequenceBuilder()
        .Type("apt-get install -y curl").Enter()
        .Build()),
    new("apt install nano", () => new Hex1bTerminalInputSequenceBuilder()
        .Type("apt-get install -y nano").Enter()
        .Build()),
    new("Show system info", () => new Hex1bTerminalInputSequenceBuilder()
        .Type("uname -a && cat /etc/os-release").Enter()
        .Build()),
    new("Install Node.js 22", () => new Hex1bTerminalInputSequenceBuilder()
        .Type("curl -fsSL https://deb.nodesource.com/setup_22.x | bash - && apt-get install -y nodejs").Enter()
        .Build()),
};

// Active terminal sessions — each gets its own floating window
var sessions = new Dictionary<int, TerminalSession>();
var nextId = 1;

void AddTerminal(string image, WindowManager windows)
{
    var id = nextId++;
    var terminal = Hex1bTerminal.CreateBuilder()
        .WithDimensions(TerminalWidth, TerminalHeight)
        .WithDockerContainer(c =>
        {
            c.Image = image;
            c.Shell = "/bin/bash";
            c.ShellArgs = ["--norc"];
        })
        .WithTerminalWidget(out var handle)
        .Build();

    handle.WindowTitleChanged += _ => displayApp?.Invalidate();
    handle.StateChanged += _ => displayApp?.Invalidate();

    // Create a floating window for this terminal
    var window = windows.Window(w =>
        w.Terminal(handle)
            .WhenNotRunning(args =>
                w.Text($"Container exited (code {args.ExitCode ?? 0})"))
            .Fill()
    )
    .Title($"#{id} {image}")
    .Size(TerminalWidth + 2, TerminalHeight + 2)
    .Resizable(minWidth: 40, minHeight: 12)
    .RightTitleActions(t => [t.Close()])
    .OnClose(() =>
    {
        sessions.Remove(id);
        _ = terminal.DisposeAsync();
        displayApp?.Invalidate();
    });

    var session = new TerminalSession(id, image, terminal, handle, window);
    sessions[id] = session;

    _ = Task.Run(async () =>
    {
        try { await terminal.RunAsync(cts.Token); }
        catch (OperationCanceledException) { }
    });

    windows.Open(window);
}

void RunSequence(Hex1bTerminal terminal, SequenceDragData seq)
{
    _ = Task.Run(async () =>
    {
        var sequence = seq.Build();
        await sequence.ApplyAsync(terminal, cts.Token);
    });
}

// Build the TUI app
await using var app = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((appRef, options) =>
    {
        displayApp = appRef;

        return ctx =>
            ctx.HStack(h =>
            [
                // Left sidebar: resizable drag bar panel with accordion
                h.DragBarPanel(
                    h.Accordion(a =>
                    [
                        a.Section("Images", s =>
                            images.Select(img =>
                                (Hex1bWidget)s.Draggable(img, dc =>
                                    dc.Text(dc.IsDragging ? $"  - {img.Name}" : $"  [img] {img.Name}"))
                                    .DragOverlay(dc => dc.Text($"[img] {img.Name}"))
                            )
                        ).Expanded(),

                        a.Section("Sequences", s =>
                            sequences.Select(seq =>
                                (Hex1bWidget)s.Draggable(seq, dc =>
                                    dc.Text(dc.IsDragging ? $"  - {seq.Name}" : $"  [seq] {seq.Name}"))
                                    .DragOverlay(dc => dc.Text($"[seq] {seq.Name}"))
                            )
                        ).Expanded()
                    ]).MultipleExpanded()
                )
                .InitialSize(28)
                .MinSize(20)
                .MaxSize(40),

                // Main area: WindowPanel with droppable surface
                h.Droppable(dc =>
                    dc.WindowPanel()
                        .Background(bg =>
                            bg.Text(dc.IsHoveredByDrag && dc.CanAcceptDrag
                                ? "\n\n    --> Drop here to start a container or run a sequence"
                                : sessions.Count == 0
                                    ? "\n\n    Drag an [img] image from the sidebar to start a container"
                                    : "")
                                .Fill()
                        )
                        .Fill()
                )
                .Accept(data => data is ImageDragData or SequenceDragData)
                .OnDrop(e =>
                {
                    if (e.DragData is ImageDragData img)
                        AddTerminal(img.Image, e.Windows);
                    else if (e.DragData is SequenceDragData seq)
                    {
                        // Apply sequence to the most recently created terminal
                        var last = sessions.Values.LastOrDefault();
                        if (last is not null)
                            RunSequence(last.Terminal, seq);
                    }
                })
                .Fill()
            ]);
    })
    .Build();

var exitCode = await app.RunAsync(cts.Token);

foreach (var s in sessions.Values)
    await s.Terminal.DisposeAsync();

return exitCode;

record ImageDragData(string Name, string Image);
record SequenceDragData(string Name, Func<Hex1bTerminalInputSequence> Build);
record TerminalSession(int Id, string ImageName, Hex1bTerminal Terminal, TerminalWidgetHandle Handle, WindowHandle Window);
