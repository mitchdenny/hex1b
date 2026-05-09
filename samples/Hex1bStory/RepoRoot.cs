namespace Hex1bStory;

/// <summary>
/// Tiny helper that locates the repository root by walking up from
/// <see cref="AppContext.BaseDirectory"/>. Used by slides that read the
/// repo's own filesystem at runtime (samples graveyard, conformance,
/// verdict). Returns <c>null</c> if the root can't be located so callers
/// can render a friendly fallback instead of crashing the deck on stage.
/// </summary>
internal static class RepoRoot
{
    private static readonly Lazy<string?> _cached = new(LocateInternal);

    /// <summary>Absolute path to the repo root, or null if not found.</summary>
    public static string? Locate() => _cached.Value;

    private static string? LocateInternal()
    {
        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                    File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
        }
        catch
        {
            // Best-effort only — return null and let callers fall back.
        }
        return null;
    }
}
