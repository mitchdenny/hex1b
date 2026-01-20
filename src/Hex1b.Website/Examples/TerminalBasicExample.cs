using Hex1b;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Terminal Widget Documentation: Comprehensive Demo
/// Demonstrates embedding multiple diagnostic terminals within a Hex1b application,
/// with terminal switching, restart functionality, and menu-driven interaction.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the code samples in:
/// src/content/guide/widgets/terminal.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class TerminalBasicExample(ILogger<TerminalBasicExample> logger) : ReactiveExample
{
    private readonly ILogger<TerminalBasicExample> _logger = logger;

    public override string Id => "terminal-basic";
    public override string Title => "Terminal Widget - Embedded Terminals Demo";
    public override string Description => "Demonstrates multiple embedded diagnostic terminals with switching and restart";

    // Terminal session tracking
    private record TerminalSession(int Id, Hex1bTerminal Terminal, TerminalWidgetHandle Handle);

    public override async Task RunAsync(IHex1bAppTerminalWorkloadAdapter workloadAdapter, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating terminal widget demo");

        // State
        var terminals = new List<TerminalSession>();
        var terminalLock = new object();
        var nextTerminalId = 1;
        var activeTerminalId = 0;
        var statusMessage = "Ready - try 'help' for commands";

        // References
        Hex1bApp? displayApp = null;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Helper to create a new terminal
        TerminalSession CreateTerminal()
        {
            var id = nextTerminalId++;
            
            var terminal = Hex1bTerminal.CreateBuilder()
                .WithDimensions(workloadAdapter.Width - 4, workloadAdapter.Height - 8)
                .WithDiagnosticShell()
                .WithTerminalWidget(out var handle)
                .Build();

            var session = new TerminalSession(id, terminal, handle);

            // Subscribe to title changes
            handle.WindowTitleChanged += _ => displayApp?.Invalidate();

            return session;
        }

        // Helper to add a terminal
        void AddTerminal()
        {
            var session = CreateTerminal();
            
            lock (terminalLock)
            {
                terminals.Add(session);
                activeTerminalId = session.Id;
            }

            // Start the terminal
            _ = Task.Run(async () =>
            {
                try
                {
                    await session.Terminal.RunAsync(cts.Token);
                    displayApp?.Invalidate();
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                }
            });

            statusMessage = $"Terminal {session.Id} created";
            displayApp?.RequestFocus(node => node is TerminalNode tn && tn.Handle == session.Handle);
            displayApp?.Invalidate();
        }

        // Helper to remove a terminal
        void RemoveTerminal(TerminalSession session)
        {
            lock (terminalLock)
            {
                terminals.Remove(session);
                if (activeTerminalId == session.Id)
                {
                    activeTerminalId = terminals.Count > 0 ? terminals[^1].Id : 0;
                }
            }
            _ = session.Terminal.DisposeAsync();
            statusMessage = $"Terminal {session.Id} closed";
            displayApp?.Invalidate();
        }

        // Helper to restart a terminal
        void RestartTerminal(TerminalSession oldSession)
        {
            lock (terminalLock)
            {
                terminals.Remove(oldSession);
            }
            _ = oldSession.Terminal.DisposeAsync();

            var newSession = CreateTerminal();
            // Preserve the ID for continuity
            var preservedSession = newSession with { Id = oldSession.Id };
            
            lock (terminalLock)
            {
                terminals.Add(preservedSession);
                activeTerminalId = preservedSession.Id;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await preservedSession.Terminal.RunAsync(cts.Token);
                    displayApp?.Invalidate();
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                }
            });

            statusMessage = $"Terminal {preservedSession.Id} restarted";
            displayApp?.RequestFocus(node => node is TerminalNode tn && tn.Handle == preservedSession.Handle);
            displayApp?.Invalidate();
        }

        // Create initial terminal
        AddTerminal();

        // Build widget tree
        Hex1bWidget BuildWidget(RootContext ctx)
        {
            List<TerminalSession> currentTerminals;
            lock (terminalLock)
            {
                currentTerminals = [.. terminals];
            }

            // Build terminal pane with fallback
            Hex1bWidget BuildTerminalPane<TParent>(WidgetContext<TParent> v, TerminalSession session) 
                where TParent : Hex1bWidget
            {
                var title = !string.IsNullOrEmpty(session.Handle.WindowTitle)
                    ? $"Terminal {session.Id}: {session.Handle.WindowTitle}"
                    : $"Terminal {session.Id}";

                return v.Border(
                    v.Terminal(session.Handle)
                        .WhenNotRunning(args => v.VStack(fallback => [
                            fallback.Text(""),
                            fallback.Align(Alignment.Center, fallback.VStack(center => [
                                center.Text($"Terminal exited with code {args.ExitCode ?? 0}"),
                                center.Text(""),
                                center.HStack(buttons => [
                                    buttons.Button("Restart").OnClick(_ => RestartTerminal(session)),
                                    buttons.Text("  "),
                                    buttons.Button("Close").OnClick(_ => RemoveTerminal(session))
                                ])
                            ]))
                        ])),
                    title: title
                );
            }

            // Build main content
            Hex1bWidget BuildMainContent<TParent>(WidgetContext<TParent> v) where TParent : Hex1bWidget
            {
                if (currentTerminals.Count == 0)
                {
                    return v.Align(Alignment.Center,
                        v.Border(
                            v.VStack(inner => [
                                inner.Text("No terminals running"),
                                inner.Text(""),
                                inner.Text("Use File → New Terminal or Ctrl+N")
                            ]),
                            title: "Welcome"
                        )
                    ).Fill();
                }

                var activeSession = currentTerminals.FirstOrDefault(s => s.Id == activeTerminalId)
                    ?? currentTerminals[0];

                return BuildTerminalPane(v, activeSession).Fill();
            }

            return ctx.VStack(v => [
                // Menu bar
                v.MenuBar(m => [
                    m.Menu("File", menu => [
                        m.MenuItem("New Terminal").OnActivated(_ => AddTerminal()),
                        m.Separator(),
                        m.MenuItem("Quit").OnActivated(_ => displayApp?.RequestStop())
                    ]),
                    m.Menu("Terminals", menu => [
                        ..currentTerminals.Select(session =>
                        {
                            var label = session.Id == activeTerminalId ? $"● Terminal {session.Id}" : $"  Terminal {session.Id}";
                            return m.MenuItem(label).OnActivated(_ =>
                            {
                                activeTerminalId = session.Id;
                                displayApp?.Invalidate();
                            });
                        }),
                        ..(currentTerminals.Count == 0
                            ? [m.MenuItem("(No terminals)").Disabled()]
                            : Array.Empty<MenuItemWidget>())
                    ]),
                    m.Menu("Help", menu => [
                        m.MenuItem("Terminal Commands").OnActivated(_ =>
                        {
                            statusMessage = "Type 'help' in the terminal for available commands";
                            displayApp?.Invalidate();
                        }),
                        m.Separator(),
                        m.MenuItem("About").OnActivated(_ =>
                        {
                            statusMessage = "Hex1b Terminal Widget Demo - Diagnostic Shell";
                            displayApp?.Invalidate();
                        })
                    ])
                ]),

                // Main content
                BuildMainContent(v),

                // Status bar
                v.InfoBar([
                    "Ctrl+N", "New",
                    "Ctrl+Q", "Quit",
                    "", statusMessage
                ])
            ]).WithInputBindings(bindings =>
            {
                bindings.Ctrl().Key(Hex1bKey.N).Action(_ => AddTerminal(), "New terminal");
                bindings.Ctrl().Key(Hex1bKey.Q).Action(_ => displayApp?.RequestStop(), "Quit");
            });
        }

        // Create and run the display app
        await using var app = new Hex1bApp(
            ctx => BuildWidget(ctx),
            new Hex1bAppOptions
            {
                WorkloadAdapter = workloadAdapter,
                EnableMouse = true
            });

        displayApp = app;

        try
        {
            await app.RunAsync(cts.Token);
        }
        finally
        {
            cts.Cancel();

            // Clean up terminals
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
    }
}
