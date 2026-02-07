using Hex1b.Diagnostics;

namespace Hex1b.Tool.Infrastructure;

/// <summary>
/// Discovers terminals by scanning the Hex1b sockets directory.
/// </summary>
internal sealed class TerminalDiscovery
{
    private static readonly string s_socketDirectory = McpDiagnosticsPresentationFilter.GetSocketDirectory();

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
        if (!Directory.Exists(s_socketDirectory))
        {
            return [];
        }

        var terminals = new List<DiscoveredTerminal>();

        foreach (var socketFile in Directory.EnumerateFiles(s_socketDirectory, "*.socket"))
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
}
