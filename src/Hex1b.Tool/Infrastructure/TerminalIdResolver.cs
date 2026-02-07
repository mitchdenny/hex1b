namespace Hex1b.Tool.Infrastructure;

/// <summary>
/// Resolves a partial terminal ID (prefix) to a full ID and socket path.
/// </summary>
internal sealed class TerminalIdResolver(TerminalDiscovery discovery)
{
    /// <summary>
    /// Result of resolving a terminal ID.
    /// </summary>
    internal sealed record ResolveResult(
        bool Success,
        string? Id,
        string? SocketPath,
        string? Type,
        string? Error
    );

    /// <summary>
    /// Resolves a terminal ID prefix to a unique terminal.
    /// </summary>
    public ResolveResult Resolve(string idPrefix)
    {
        var terminals = discovery.Scan();

        if (terminals.Count == 0)
        {
            return new ResolveResult(false, null, null, null, "No terminals found. Is a Hex1b app running with diagnostics enabled?");
        }

        var matches = terminals
            .Where(t => t.Id.StartsWith(idPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count switch
        {
            0 => new ResolveResult(false, null, null, null, $"No terminal found matching '{idPrefix}'. Run 'dotnet hex1b terminal list' to see available terminals."),
            1 => new ResolveResult(true, matches[0].Id, matches[0].SocketPath, matches[0].Type, null),
            _ => new ResolveResult(false, null, null, null, $"Ambiguous terminal ID '{idPrefix}' matches {matches.Count} terminals: {string.Join(", ", matches.Select(m => m.Id))}. Use a longer prefix.")
        };
    }
}
