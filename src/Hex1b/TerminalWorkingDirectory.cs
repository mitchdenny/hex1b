namespace Hex1b;

/// <summary>
/// Contains the working directory last reported by a shell using OSC 7.
/// </summary>
/// <remarks>
/// This reflects the most recently reported directory only, not a history of visited
/// directories. No directory is inferred from output; it is only ever set from OSC 7.
/// </remarks>
public sealed record TerminalWorkingDirectory
{
    internal static TerminalWorkingDirectory Default { get; } = new(null, null, null);

    internal TerminalWorkingDirectory(string? uri, string? host, string? path)
    {
        Uri = uri;
        Host = host;
        Path = path;
    }

    /// <summary>Gets the raw reported URI, or null if no directory has been reported.</summary>
    public string? Uri { get; }

    /// <summary>
    /// Gets the reported host name, or null if none was present or no directory has been
    /// reported. An empty host (as in <c>file:///path</c>) is reported as an empty string.
    /// </summary>
    public string? Host { get; }

    /// <summary>
    /// Gets the percent-decoded local path, or null if no directory has been reported.
    /// </summary>
    public string? Path { get; }

    /// <summary>
    /// Parses a raw OSC 7 <c>file://</c> URI, or returns null if it is not a well-formed
    /// absolute file URI.
    /// </summary>
    internal static TerminalWorkingDirectory? TryCreate(string uri)
    {
        if (!System.Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || !parsed.IsFile)
            return null;
        return new TerminalWorkingDirectory(uri, parsed.Host, parsed.LocalPath);
    }
}
