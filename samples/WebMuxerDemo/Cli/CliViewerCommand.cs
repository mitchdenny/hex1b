using System.Net.Sockets;
using Hex1b;

namespace WebMuxerDemo.Cli;

/// <summary>
/// Entry point for the <c>webmuxerdemo connect</c> subcommand.
/// Parses args, opens a UDS connection to the well-known session socket,
/// builds the outer (host TTY) and inner (embedded HMP1-driven) Hex1b
/// terminals, and runs the viewer app.
/// </summary>
internal static class CliViewerCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        // Parse args. Supported:
        //   connect                                         -> list sessions
        //   connect --session NAME [--display-name LABEL]   -> viewer
        string? sessionName = null;
        string? displayName = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--session" when i + 1 < args.Length:
                    sessionName = args[++i];
                    break;
                case "--display-name" when i + 1 < args.Length:
                    displayName = args[++i];
                    break;
                case "--help" or "-h":
                    PrintUsage();
                    return 0;
                default:
                    Console.Error.WriteLine($"Unknown argument: {args[i]}");
                    PrintUsage();
                    return 2;
            }
        }

        if (sessionName is null)
        {
            return ListSessions();
        }

        var socketPath = SessionPaths.ForSession(sessionName);
        if (!File.Exists(socketPath))
        {
            Console.Error.WriteLine($"Session '{sessionName}' not found at {socketPath}.");
            Console.Error.WriteLine();
            ListSessions();
            return 1;
        }

        Hmp1WorkloadAdapter? adapter = null;
        Stream? stream = null;
        try
        {
            stream = await Hmp1Transports.ConnectUnixSocket(socketPath, CancellationToken.None);
            adapter = new Hmp1WorkloadAdapter(
                stream,
                displayName: displayName ?? "webmux-cli",
                defaultRole: "viewer");

            // Connect handshake: this awaits the server Hello which carries
            // PeerId, PrimaryPeerId, current dims, and the existing roster.
            // We fail fast if the producer doesn't reply within 5s — better
            // than hanging forever on a wedged socket.
            using (var handshakeCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                await adapter.ConnectAsync(handshakeCts.Token);
            }
        }
        catch (SocketException ex)
        {
            Console.Error.WriteLine($"Failed to connect to '{sessionName}': {ex.Message}");
            try { await (adapter?.DisposeAsync() ?? ValueTask.CompletedTask); } catch { }
            try { stream?.Dispose(); } catch { }
            return 1;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine($"Timed out waiting for session '{sessionName}' to handshake.");
            try { await (adapter?.DisposeAsync() ?? ValueTask.CompletedTask); } catch { }
            try { stream?.Dispose(); } catch { }
            return 1;
        }

        try
        {
            var app = new CliViewerApp(adapter, sessionName);
            await app.RunAsync();
        }
        finally
        {
            // Bound the adapter teardown for the same reason we bound the
            // embedded terminal teardown — the read pump can outlive a
            // graceful Dispose if the producer is still pumping output.
            try
            {
                var disposeTask = adapter.DisposeAsync().AsTask();
                var timeout = Task.Delay(TimeSpan.FromSeconds(2));
                await Task.WhenAny(disposeTask, timeout);
            }
            catch { }
        }

        return 0;
    }

    private static int ListSessions()
    {
        var sessions = SessionPaths.ListSessions();
        if (sessions.Count == 0)
        {
            Console.WriteLine("No sessions found.");
            Console.WriteLine($"  (looked under {SessionPaths.Root})");
            Console.WriteLine();
            Console.WriteLine("Start the web server first:  webmuxerdemo serve");
            return 1;
        }

        Console.WriteLine("Available sessions:");
        foreach (var name in sessions)
        {
            Console.WriteLine($"  {name}");
        }
        Console.WriteLine();
        Console.WriteLine("Connect with:  webmuxerdemo connect --session <name>");
        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("WebMuxerDemo CLI viewer");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  webmuxerdemo connect");
        Console.WriteLine("    List discoverable sessions and exit.");
        Console.WriteLine();
        Console.WriteLine("  webmuxerdemo connect --session NAME [--display-name LABEL]");
        Console.WriteLine("    Attach as a multi-head viewer to the named session.");
        Console.WriteLine();
        Console.WriteLine("Hotkeys (Ctrl+B is the chord prefix, tmux-style):");
        Console.WriteLine("  Ctrl+B T   take control (resizes producer to your terminal)");
        Console.WriteLine("  Ctrl+B D   detach and exit");
    }
}
