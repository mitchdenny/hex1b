using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

// State: collection of active terminals
var terminals = new List<TerminalSession>();
var terminalLock = new object();
var nextTerminalId = 1;

// Cancellation for the entire demo
using var cts = new CancellationTokenSource();

// Reference to the app for invalidation
Hex1bApp? displayApp = null;

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
    
    lock (terminalLock)
    {
        terminals.Add(session);
    }
    
    // Start the terminal and remove it when it exits
    _ = Task.Run(async () =>
    {
        try
        {
            await terminal.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        finally
        {
            lock (terminalLock)
            {
                terminals.Remove(session);
            }
            await terminal.DisposeAsync();
            
            // Re-render the UI to reflect the removed terminal
            displayApp?.Invalidate();
        }
    });
    
    displayApp?.Invalidate();
}

// Build the widget tree based on current terminals
Hex1bWidget BuildTerminalWidget(RootContext ctx)
{
    List<TerminalSession> currentTerminals;
    lock (terminalLock)
    {
        currentTerminals = [.. terminals];
    }
    
    return ctx.VStack(v =>
    {
        var children = new List<Hex1bWidget>
        {
            // Header with instructions
            v.Text($"Embedded Terminal Demo - {currentTerminals.Count} terminal(s)"),
            v.Text("Press Ctrl+N to add a terminal, Ctrl+Q to quit"),
            v.Separator()
        };
        
        if (currentTerminals.Count == 0)
        {
            // No terminals - show placeholder
            children.Add(
                v.Align(Alignment.Center,
                    v.Border(
                        v.VStack(vv =>
                        [
                            vv.Text("No terminals running"),
                            vv.Text(""),
                            vv.Text("Press Ctrl+N to add a terminal")
                        ]),
                        title: "Welcome"
                    )
                ).Fill()
            );
        }
        else if (currentTerminals.Count == 1)
        {
            // Single terminal - no splitter needed
            var session = currentTerminals[0];
            children.Add(
                v.Border(
                    v.Terminal(session.Handle).Fill(),
                    title: $"Terminal {session.Id}"
                ).Fill()
            );
        }
        else
        {
            // Multiple terminals - build nested splitters
            // Start with the first terminal
            Hex1bWidget result = v.Border(
                v.Terminal(currentTerminals[0].Handle).Fill(),
                title: $"Terminal {currentTerminals[0].Id}"
            );
            
            // Add remaining terminals with splitters
            for (int i = 1; i < currentTerminals.Count; i++)
            {
                var session = currentTerminals[i];
                var rightPane = v.Border(
                    v.Terminal(session.Handle).Fill(),
                    title: $"Terminal {session.Id}"
                );
                
                // Calculate proportional width for left pane
                var leftRatio = (double)i / (i + 1);
                var leftWidth = (int)(80 * leftRatio); // Base width scaled by ratio
                
                result = v.HSplitter(result, rightPane, leftWidth: leftWidth);
            }
            
            children.Add(result.Fill());
        }
        
        return [.. children];
    }).WithInputBindings(bindings =>
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
