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

// Available automation sequences (command strings — marker + completion detection added at runtime)
var sequences = new SequenceDragData[]
{
    new("Install .NET 9.0", "curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 9.0"),
    new("Install .NET 8.0", "curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0"),
    new("Install .NET 10.0", "curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --quality preview"),
    new("Add dotnet to PATH", "export PATH=$PATH:$HOME/.dotnet"),
    new("apt update", "apt-get update -y"),
    new("apt install curl", "apt-get install -y curl"),
    new("apt install nano", "apt-get install -y nano"),
    new("Show system info", "uname -a && cat /etc/os-release"),
    new("Install Node.js 22", "curl -fsSL https://deb.nodesource.com/setup_22.x | bash - && apt-get install -y nodejs"),
    new("Aspire (GA)", "curl -sSL https://aspire.dev/install.sh | bash"),
    new("Aspire (Dev)", "curl -sSL https://aspire.dev/install.sh | bash -s -- --quality dev"),
    new("Aspire (Staging)", "curl -sSL https://aspire.dev/install.sh | bash -s -- --quality staging"),
};

// Active terminal sessions — each gets its own floating window
var sessions = new Dictionary<int, TerminalSession>();
var nextId = 1;
var nextMarkerId = 1;

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

    // Create session first so window content can reference it
    var session = new TerminalSession(id, image, terminal, handle);
    sessions[id] = session;

    // Create a floating window — terminal wrapped in a per-terminal Droppable
    var window = windows.Window(w =>
        w.Droppable(dc =>
        {
            List<QueuedSequence> queue;
            lock (session.QueueLock)
                queue = [.. session.Queue];

            var terminalWidget = dc.Terminal(handle)
                .WhenNotRunning(args =>
                    dc.Text($"Container exited (code {args.ExitCode ?? 0})"))
                .Fill();

            if (queue.Count > 0)
            {
                return dc.HStack(h =>
                [
                    terminalWidget,
                    h.Border(
                        h.VStack(v =>
                            [.. queue.Select(q =>
                                (Hex1bWidget)v.Text(q.IsRunning ? $" > {q.Name}" : $"   {q.Name}")
                            )]
                        ).Fill()
                    ).Title("Queue").FixedWidth(24)
                ]);
            }

            return terminalWidget;
        })
        .Accept(data => data is SequenceDragData)
        .OnDrop(e =>
        {
            if (e.DragData is SequenceDragData seq)
                EnqueueSequence(session, seq);
        })
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

    session.Window = window;

    _ = Task.Run(async () =>
    {
        try { await terminal.RunAsync(cts.Token); }
        catch (OperationCanceledException) { }
    });

    windows.Open(window);
}

void EnqueueSequence(TerminalSession session, SequenceDragData seq)
{
    bool shouldStartProcessing;
    lock (session.QueueLock)
    {
        session.Queue.Add(new QueuedSequence(seq.Name, seq.Command, false));
        shouldStartProcessing = !session.ProcessingActive;
        if (shouldStartProcessing)
            session.ProcessingActive = true;
    }
    displayApp?.Invalidate();

    if (shouldStartProcessing)
        _ = Task.Run(async () => await ProcessQueueAsync(session));
}

async Task ProcessQueueAsync(TerminalSession session)
{
    while (true)
    {
        string command;
        lock (session.QueueLock)
        {
            if (session.Queue.Count == 0)
            {
                session.ProcessingActive = false;
                displayApp?.Invalidate();
                return;
            }
            var first = session.Queue[0];
            session.Queue[0] = first with { IsRunning = true };
            command = first.Command;
        }
        displayApp?.Invalidate();

        try
        {
            // Append a colored echo marker to detect command completion.
            // The typed command shows \033[32m as literal text (no color),
            // but the echo output renders the marker in green. We use
            // CellPatternSearcher to find the marker with a foreground
            // color, which uniquely identifies the echo output.
            var marker = $"SEQDONE_{nextMarkerId++}";
            var fullCommand = $"{command} && printf '\\033[32m{marker}\\033[0m\\n'";

            var searcher = BuildMarkerSearcher(marker);
            var sequence = new Hex1bTerminalInputSequenceBuilder()
                .Type(fullCommand).Enter()
                .WaitUntil(s => searcher.SearchFirst(s) is not null, TimeSpan.FromMinutes(10))
                .Build();
            await sequence.ApplyAsync(session.Terminal, cts.Token);
        }
        catch (OperationCanceledException) { return; }
        catch { /* sequence failed or timed out, continue to next */ }

        lock (session.QueueLock)
        {
            if (session.Queue.Count > 0)
                session.Queue.RemoveAt(0);
        }
        displayApp?.Invalidate();
    }
}

// Build a CellPatternSearcher that matches the marker text with a foreground
// color set — this only matches the echo output, never the typed command.
CellPatternSearcher BuildMarkerSearcher(string marker)
{
    var first = marker[0].ToString();
    var searcher = new CellPatternSearcher()
        .Find(ctx => ctx.Cell.Character == first && ctx.Cell.Foreground is not null);

    for (var i = 1; i < marker.Length; i++)
    {
        var ch = marker[i].ToString();
        searcher = searcher.Right(ctx => ctx.Cell.Character == ch);
    }

    return searcher;
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
                                ? "\n\n    --> Drop here to start a container"
                                : sessions.Count == 0
                                    ? "\n\n    Drag an [img] image from the sidebar to start a container"
                                    : "")
                                .Fill()
                        )
                        .Fill()
                )
                .Accept(data => data is ImageDragData)
                .OnDrop(e =>
                {
                    if (e.DragData is ImageDragData img)
                        AddTerminal(img.Image, e.Windows);
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
record SequenceDragData(string Name, string Command);
record QueuedSequence(string Name, string Command, bool IsRunning);

class TerminalSession(int id, string imageName, Hex1bTerminal terminal, TerminalWidgetHandle handle)
{
    public int Id { get; } = id;
    public string ImageName { get; } = imageName;
    public Hex1bTerminal Terminal { get; } = terminal;
    public TerminalWidgetHandle Handle { get; } = handle;
    public WindowHandle? Window { get; set; }
    public List<QueuedSequence> Queue { get; } = [];
    public bool ProcessingActive { get; set; }
    public object QueueLock { get; } = new();
}
