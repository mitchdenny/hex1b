internal static class TerminalAnimationSupport
{
    private static readonly Version FirstGhosttyVersionWithNativeAnimation = new(1, 4);

    internal static bool SupportsNativeKgpAnimation()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("TERM_PROGRAM"),
                "ghostty",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var versionText = Environment.GetEnvironmentVariable("TERM_PROGRAM_VERSION");
        return Version.TryParse(versionText, out var version) &&
            version >= FirstGhosttyVersionWithNativeAnimation;
    }
}
