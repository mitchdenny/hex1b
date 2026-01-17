using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

// State: collection of active terminals
var terminals = new List<TerminalSession>();
var terminalLock = new object();
var nextTerminalId = 1;
var activeTerminalId = 0; // ID of the currently displayed terminal (0 = none)
var statusMessage = "Ready"; // Status bar message

// Cancellation for the entire demo
using var cts = new CancellationTokenSource();

// Reference to the app for invalidation
Hex1bApp? displayApp = null;

// Helper to remove a terminal from the collection
void RemoveTerminal(TerminalSession session)
{
    lock (terminalLock)
    {
        terminals.Remove(session);
        
        // If we removed the active terminal, switch to another one
        if (activeTerminalId == session.Id)
        {
            activeTerminalId = terminals.Count > 0 ? terminals[^1].Id : 0;
        }
    }
    _ = session.Terminal.DisposeAsync();
    displayApp?.Invalidate();
}

// Helper to add a new terminal
void AddTerminal()
{
    var id = nextTerminalId++;
    var terminal = Hex1bTerminal.CreateBuilder()
        .WithDimensions(40, 24)
        .WithPtyProcess("bash", "--norc")
        .WithTerminalWidget(out var handle)
        .Build();
    
    var session = new TerminalSession(id, terminal, handle);
    
    // Subscribe to title changes to update the UI
    handle.WindowTitleChanged += _ => displayApp?.Invalidate();
    
    lock (terminalLock)
    {
        terminals.Add(session);
        activeTerminalId = id; // Make new terminal the active one
    }
    
    // Start the terminal in the background
    // When it exits normally, the WhenNotRunning callback will be shown
    // Only on cancellation (app shutdown) do we remove immediately
    _ = Task.Run(async () =>
    {
        try
        {
            await terminal.RunAsync(cts.Token);
            // Normal exit - WhenNotRunning callback will handle the UI
            // Just trigger a re-render so the fallback shows
            displayApp?.Invalidate();
        }
        catch (OperationCanceledException)
        {
            // App shutdown - remove immediately without showing exit UI
            RemoveTerminal(session);
        }
    });
    
    // Request focus on the new terminal (will be applied after next render)
    displayApp?.RequestFocus(node => node is TerminalNode terminalNode && terminalNode.Handle == handle);
    displayApp?.Invalidate();
}

// Helper to restart a terminal session
void RestartTerminal(TerminalSession oldSession)
{
    // Remove the old session
    lock (terminalLock)
    {
        terminals.Remove(oldSession);
    }
    _ = oldSession.Terminal.DisposeAsync();
    
    // Create a new one with the same ID
    var terminal = Hex1bTerminal.CreateBuilder()
        .WithDimensions(40, 24)
        .WithPtyProcess("bash", "--norc")
        .WithTerminalWidget(out var handle)
        .Build();
    
    var newSession = new TerminalSession(oldSession.Id, terminal, handle);
    
    // Subscribe to title changes to update the UI
    handle.WindowTitleChanged += _ => displayApp?.Invalidate();
    
    lock (terminalLock)
    {
        terminals.Add(newSession);
        activeTerminalId = newSession.Id; // Keep this terminal active
    }
    
    // Start the terminal
    _ = Task.Run(async () =>
    {
        try
        {
            await terminal.RunAsync(cts.Token);
            // Normal exit - WhenNotRunning callback will handle the UI
            displayApp?.Invalidate();
        }
        catch (OperationCanceledException)
        {
            // App shutdown - remove immediately
            lock (terminalLock)
            {
                terminals.Remove(newSession);
            }
            await terminal.DisposeAsync();
        }
    });
    
    // Request focus on the restarted terminal (will be applied after next render)
    displayApp?.RequestFocus(node => node is TerminalNode terminalNode && terminalNode.Handle == handle);
    displayApp?.Invalidate();
}

// Predefined automation sequences organized by category
var basicShellSequences = new Dictionary<string, Func<Hex1bTerminalInputSequence>>
{
    ["List Files"] = () => new Hex1bTerminalInputSequenceBuilder()
        .Type("ls -la")
        .Enter()
        .Build(),
        
    ["Show Current Directory"] = () => new Hex1bTerminalInputSequenceBuilder()
        .Type("pwd")
        .Enter()
        .Build(),
        
    ["Clear Screen"] = () => new Hex1bTerminalInputSequenceBuilder()
        .Type("clear")
        .Enter()
        .Build(),
        
    ["System Info"] = () => new Hex1bTerminalInputSequenceBuilder()
        .Type("uname -a")
        .Enter()
        .Build(),
        
    ["Disk Usage"] = () => new Hex1bTerminalInputSequenceBuilder()
        .Type("df -h")
        .Enter()
        .Build(),
        
    ["Process List"] = () => new Hex1bTerminalInputSequenceBuilder()
        .Type("ps aux | head -20")
        .Enter()
        .Build(),
        
    ["Hello World Echo"] = () => new Hex1bTerminalInputSequenceBuilder()
        .Type("echo 'Hello from automation!'")
        .Enter()
        .Build(),
        
    ["Create Test File"] = () => new Hex1bTerminalInputSequenceBuilder()
        .Type("echo 'Test content' > /tmp/hex1b_test.txt && cat /tmp/hex1b_test.txt")
        .Enter()
        .Build()
};

var asciiArtSequences = new Dictionary<string, Func<Hex1bTerminalInputSequence>>
{
    ["Star Wars (SSH)"] = () => new Hex1bTerminalInputSequenceBuilder()
        .Type("ssh starwarstel.net")
        .Enter()
        .Build(),
        
    ["CMatrix (Docker)"] = () => new Hex1bTerminalInputSequenceBuilder()
        .Type("docker run -it --rm --log-driver none --net none --read-only --cap-drop=ALL willh/cmatrix")
        .Enter()
        .Build(),
        
    ["Pipes (Docker)"] = () => new Hex1bTerminalInputSequenceBuilder()
        .Type("docker run --rm -it joonas/pipes.sh")
        .Enter()
        .Build(),
        
    ["Asciiquarium (Docker)"] = () => new Hex1bTerminalInputSequenceBuilder()
        .Type("docker run -it --rm vanessa/asciiquarium")
        .Enter()
        .Build(),
        
    ["SL Train"] = () => new Hex1bTerminalInputSequenceBuilder()
        .Type("sl")
        .Enter()
        .Build(),
        
    ["Fortune + Cowsay"] = () => new Hex1bTerminalInputSequenceBuilder()
        .Type("fortune | cowsay")
        .Enter()
        .Build(),
        
    ["Figlet Banner"] = () => new Hex1bTerminalInputSequenceBuilder()
        .Type("figlet 'Hex1b'")
        .Enter()
        .Build(),
        
    ["Mapscii"] = () => new Hex1bTerminalInputSequenceBuilder()
        .Type("npx mapscii")
        .Enter()
        .Build()
};

// Combined sequences for lookup
var allSequences = basicShellSequences
    .Concat(asciiArtSequences)
    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

// Helper to run an automation sequence on the active terminal
void RunAutomation(string sequenceName)
{
    TerminalSession? activeSession;
    lock (terminalLock)
    {
        activeSession = terminals.FirstOrDefault(s => s.Id == activeTerminalId);
    }
    
    if (activeSession == null)
    {
        statusMessage = "No active terminal to run automation";
        displayApp?.Invalidate();
        return;
    }
    
    if (!allSequences.TryGetValue(sequenceName, out var sequenceFactory))
    {
        statusMessage = $"Unknown sequence: {sequenceName}";
        displayApp?.Invalidate();
        return;
    }
    
    statusMessage = $"Running: {sequenceName}...";
    displayApp?.Invalidate();
    
    // Run the sequence in the background
    _ = Task.Run(async () =>
    {
        try
        {
            var sequence = sequenceFactory();
            await sequence.ApplyAsync(activeSession.Terminal);
            statusMessage = $"Completed: {sequenceName}";
        }
        catch (Exception ex)
        {
            statusMessage = $"Error: {ex.Message}";
        }
        displayApp?.Invalidate();
    });
}

// Build the widget tree based on current terminals
Hex1bWidget BuildTerminalWidget(RootContext ctx)
{
    List<TerminalSession> currentTerminals;
    lock (terminalLock)
    {
        currentTerminals = [.. terminals];
    }
    
    // Helper to build a terminal widget with exit handling
    Hex1bWidget BuildTerminalPane<TParent>(WidgetContext<TParent> v, TerminalSession session) where TParent : Hex1bWidget
    {
        // Build the border title - use the terminal's window title if set, otherwise fall back to default
        var terminalTitle = !string.IsNullOrEmpty(session.Handle.WindowTitle) 
            ? $"Terminal {session.Id}: {session.Handle.WindowTitle}"
            : $"Terminal {session.Id}";
        
        return v.Border(
            v.Terminal(session.Handle)
                .WhenNotRunning(args => v.VStack(vv =>
                [
                    vv.Text(""),
                    vv.Align(Alignment.Center, 
                        vv.VStack(center =>
                        [
                            center.Text($"Terminal exited with code {args.ExitCode ?? 0}"),
                            center.Text(""),
                            center.HStack(buttons =>
                            [
                                buttons.Button("Restart").OnClick(_ => RestartTerminal(session)),
                                buttons.Text("  "),
                                buttons.Button("Close").OnClick(_ => RemoveTerminal(session))
                            ])
                        ])
                    )
                ]))
                .Fill(),
            title: terminalTitle
        );
    }
    
    // Build the main content area - only show the active terminal
    Hex1bWidget BuildMainContent<TParent>(WidgetContext<TParent> v) where TParent : Hex1bWidget
    {
        if (currentTerminals.Count == 0)
        {
            // No terminals - show placeholder
            return v.Align(Alignment.Center,
                v.Border(
                    v.VStack(vv =>
                    [
                        vv.Text("No terminals running"),
                        vv.Text(""),
                        vv.Text("Use File → New Terminal or press Ctrl+N")
                    ]),
                    title: "Welcome"
                )
            ).Fill();
        }
        
        // Find the active terminal
        var activeSession = currentTerminals.FirstOrDefault(s => s.Id == activeTerminalId) 
                         ?? currentTerminals[0];
        
        return BuildTerminalPane(v, activeSession).Fill();
    }
    
    return ctx.VStack(v =>
    [
        // Menu bar at the top
        v.MenuBar(m =>
        [
            m.Menu("File", m =>
            [
                m.MenuItem("New Terminal").OnActivated(_ => AddTerminal()),
                m.Separator(),
                m.MenuItem("Quit").OnActivated(_ => displayApp?.RequestStop())
            ]),
            m.Menu("Terminals", m =>
            [
                // List each existing terminal with a checkmark for the active one
                // Uses the terminal's WindowTitle from OSC 0/2 sequences if set
                ..currentTerminals.Select(session =>
                {
                    var menuLabel = !string.IsNullOrEmpty(session.Handle.WindowTitle)
                        ? $"{(session.Id == activeTerminalId ? "● " : "  ")}{session.Handle.WindowTitle}"
                        : $"{(session.Id == activeTerminalId ? "● " : "  ")}Terminal {session.Id}";
                    return m.MenuItem(menuLabel)
                        .OnActivated(_ => 
                        {
                            activeTerminalId = session.Id;
                            displayApp?.Invalidate();
                        });
                }),
                // Show placeholder if no terminals
                ..(currentTerminals.Count == 0 
                    ? [m.MenuItem("(No terminals)").Disabled()]
                    : Array.Empty<MenuItemWidget>())
            ]),
            m.Menu("Automation", m =>
            [
                // Basic Shell submenu
                m.Menu("Basic Shell", m =>
                [
                    ..basicShellSequences.Keys.Select(name =>
                        m.MenuItem(name).OnActivated(_ => RunAutomation(name))
                    )
                ]),
                // ASCII Art submenu
                m.Menu("ASCII Art", m =>
                [
                    ..asciiArtSequences.Keys.Select(name =>
                        m.MenuItem(name).OnActivated(_ => RunAutomation(name))
                    )
                ])
            ]),
            m.Menu("Help", m =>
            [
                m.MenuItem("Keyboard Shortcuts").OnActivated(_ => { /* TODO: show shortcuts */ }),
                m.Separator(),
                m.MenuItem("About").OnActivated(_ => { /* TODO: show about */ })
            ])
        ]),
        
        // Main content
        BuildMainContent(v),
        
        // Status bar at the bottom
        v.InfoBar([
            "Ctrl+N", "New Terminal",
            "Ctrl+Q", "Quit",
            "", statusMessage
        ])
    ]).WithInputBindings(bindings =>
    {
        bindings.Ctrl().Key(Hex1bKey.N).Action(_ => AddTerminal(), "Add terminal");
        bindings.Ctrl().Key(Hex1bKey.Q).Action(_ => displayApp?.RequestStop(), "Quit");
    });
}

// Create the TUI app that displays terminals
await using var displayTerminal = Hex1bTerminal.CreateBuilder()
    .WithRenderOptimization()
    .WithMouse()
    .WithHex1bApp((app, options) =>
    {
        displayApp = app;
        return ctx => BuildTerminalWidget(ctx);
    })
    .Build();

try
{
    await displayTerminal.RunAsync(cts.Token);
}
finally
{
    // Cancel all child terminals
    cts.Cancel();
    
    // Wait for all terminals to clean up
    List<TerminalSession> remaining;
    lock (terminalLock)
    {
        remaining = [.. terminals];
        terminals.Clear();
    }
    
    foreach (var session in remaining)
    {
        await session.Terminal.DisposeAsync();
    }
}

// === Types ===

record TerminalSession(int Id, Hex1bTerminal Terminal, TerminalWidgetHandle Handle);
