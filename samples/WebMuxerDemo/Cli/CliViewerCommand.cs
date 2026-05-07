using System.Net.Sockets;

namespace WebMuxerDemo.Cli;

/// <summary>
/// Entry point for the <c>webmuxerdemo connect</c> subcommand. Parses
/// args, lists discoverable sessions on no <c>--session</c>, verifies
/// the target socket file exists, and runs the viewer app. The HMP1
/// handshake itself is owned by <see cref="CliViewerApp"/> via the
/// easy-path <c>WithHmp1Client</c> builder extension; this command
/// merely catches the typical transport-failure exception shapes that
/// can escape <see cref="CliViewerApp.RunAsync"/> and translates them
/// into clean stderr messages.
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

        var app = new CliViewerApp(socketPath, sessionName, displayName);
        try
        {
            await app.RunAsync();
        }
        catch (SocketException ex)
        {
            // Typical when the producer is gone but the socket file
            // still exists — e.g. webmuxerdemo serve crashed without
            // unlinking, or someone deleted just the inode.
            Console.Error.WriteLine($"Failed to connect to '{sessionName}': {ex.Message}");
            return 1;
        }
        catch (IOException ex)
        {
            // Catches read/write errors from the underlying NetworkStream
            // (e.g. EPIPE if the producer hangs up mid-handshake).
            Console.Error.WriteLine($"Connection error on '{sessionName}': {ex.Message}");
            return 1;
        }
        catch (OperationCanceledException)
        {
            // Surfaces if the host caller cancels mid-handshake.
            Console.Error.WriteLine($"Connection to '{sessionName}' was cancelled.");
            return 1;
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
