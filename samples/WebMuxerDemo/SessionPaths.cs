namespace WebMuxerDemo;

/// <summary>
/// Well-known directory layout for WebMuxerDemo per-session UDS sockets.
/// Mirrors MuxerDemo's discovery model: session sockets live under
/// <c>~/.hex1bsamples/webmuxerdemo/</c> as <c>{name}.sock</c>; clients
/// (web server, CLI viewer) discover sessions by enumerating the directory.
/// </summary>
/// <remarks>
/// No registry file or PID tracking — the filesystem IS the registry. A
/// crashed serve leaves stale <c>.sock</c> files behind; the next serve
/// removes them via try-delete-before-bind in <see cref="SessionHost"/>.
/// </remarks>
internal static class SessionPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".hex1bsamples", "webmuxerdemo");

    public static string ForSession(string sessionName) =>
        Path.Combine(Root, $"{sessionName}.sock");

    public static IReadOnlyList<string> ListSessions()
    {
        if (!Directory.Exists(Root))
        {
            return Array.Empty<string>();
        }

        var sessions = new List<string>();
        foreach (var file in Directory.GetFiles(Root, "*.sock"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!string.IsNullOrEmpty(name))
            {
                sessions.Add(name);
            }
        }

        sessions.Sort(StringComparer.Ordinal);
        return sessions;
    }

    public static void EnsureRootExists()
    {
        Directory.CreateDirectory(Root);
    }
}
