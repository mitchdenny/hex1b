using Hex1b.Diagnostics;

namespace Hex1b.Tool.Infrastructure;

/// <summary>
/// Discovers terminals by scanning the Hex1b sockets directory.
/// </summary>
internal sealed class TerminalDiscovery
{
    private readonly string _socketDirectory;

    public TerminalDiscovery()
    {
        _socketDirectory = McpDiagnosticsPresentationFilter.GetSocketDirectory();
    }

    internal TerminalDiscovery(string socketDirectory)
    {
        _socketDirectory = socketDirectory;
    }

    /// <summary>
    /// Represents a discovered terminal socket.
    /// </summary>
    internal sealed record DiscoveredTerminal(
        string Id,
        string SocketPath,
        string Type // "tui" or "host"
    );

    /// <summary>
    /// Scans the sockets directory for all terminal sockets.
    /// </summary>
    public IReadOnlyList<DiscoveredTerminal> Scan()
    {
        if (!Directory.Exists(_socketDirectory))
        {
            return [];
        }

        var terminals = new List<DiscoveredTerminal>();

        foreach (var socketFile in Directory.EnumerateFiles(_socketDirectory, "*.socket"))
        {
            var fileName = Path.GetFileName(socketFile);

            if (fileName.EndsWith(".diagnostics.socket", StringComparison.Ordinal))
            {
                var id = fileName[..fileName.IndexOf('.')];
                terminals.Add(new DiscoveredTerminal(id, socketFile, "tui"));
            }
            else if (fileName.EndsWith(".terminal.socket", StringComparison.Ordinal))
            {
                var id = fileName[..fileName.IndexOf('.')];
                terminals.Add(new DiscoveredTerminal(id, socketFile, "host"));
            }
        }

        return terminals;
    }

    /// <summary>
    /// Removes socket files that are not reachable (stale sockets from exited processes).
    /// Returns the number of sockets removed.
    /// </summary>
    public async Task<int> CleanStaleAsync(TerminalClient client, CancellationToken cancellationToken = default)
    {
        var terminals = Scan();
        var removed = 0;

        foreach (var terminal in terminals)
        {
            var info = await client.TryProbeAsync(terminal.SocketPath, cancellationToken);
            if (info == null)
            {
                try
                {
                    File.Delete(terminal.SocketPath);
                    removed++;
                }
                catch
                {
                    // Best effort
                }
            }
        }

        return removed;
    }
}
