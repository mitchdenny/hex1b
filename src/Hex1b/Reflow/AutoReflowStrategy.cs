namespace Hex1b.Reflow;

/// <summary>
/// Reflow strategy that automatically detects the appropriate terminal-specific strategy
/// based on environment variables (<c>TERM_PROGRAM</c>, <c>WT_SESSION</c>, <c>VTE_VERSION</c>).
/// </summary>
/// <remarks>
/// <para>
/// This strategy delegates to the detected terminal-specific strategy at construction time.
/// It is used by <c>Hex1bTerminalBuilder.WithReflow()</c> to enable reflow without requiring
/// the caller to know which terminal emulator is running.
/// </para>
/// <para>
/// For headless/testing scenarios, prefer passing an explicit strategy
/// (e.g., <c>WithReflow(KittyReflowStrategy.Instance)</c>) to get deterministic behavior.
/// </para>
/// </remarks>
public sealed class AutoReflowStrategy : ITerminalReflowProvider
{
    private readonly ITerminalReflowProvider _detected;

    /// <summary>
    /// Shared singleton instance. Detection runs once at first access.
    /// </summary>
    public static readonly AutoReflowStrategy Instance = new();

    private AutoReflowStrategy()
    {
        _detected = Detect();
    }

    /// <summary>
    /// Gets the terminal-specific strategy that was detected from the environment.
    /// </summary>
    public ITerminalReflowProvider DetectedStrategy => _detected;

    /// <inheritdoc/>
    public bool ShouldClearSoftWrapOnAbsolutePosition => _detected.ShouldClearSoftWrapOnAbsolutePosition;

    /// <inheritdoc/>
    public ReflowResult Reflow(ReflowContext context) => _detected.Reflow(context);

    /// <summary>
    /// Detects the appropriate reflow strategy based on environment variables.
    /// </summary>
    internal static ITerminalReflowProvider Detect()
    {
        // Check for Windows Terminal first (uses WT_SESSION env var)
        if (Environment.GetEnvironmentVariable("WT_SESSION") is not null)
            return WindowsTerminalReflowStrategy.Instance;

        var termProgram = Environment.GetEnvironmentVariable("TERM_PROGRAM");

        return termProgram?.ToLowerInvariant() switch
        {
            "kitty" => KittyReflowStrategy.Instance,
            "ghostty" => GhosttyReflowStrategy.Instance,
            "foot" => FootReflowStrategy.Instance,
            "gnome-terminal" or "tilix" or "xfce4-terminal" => VteReflowStrategy.Instance,
            "wezterm" => WezTermReflowStrategy.Instance,
            "alacritty" => AlacrittyReflowStrategy.Instance,
            "xterm" or "xterm-256color" => XtermReflowStrategy.Instance,
            "iterm.app" => ITerm2ReflowStrategy.Instance,
            _ => DetectFromTerm()
        };
    }

    private static ITerminalReflowProvider DetectFromTerm()
    {
        // VTE-based terminals often set TERM=xterm-256color but also set VTE_VERSION
        if (Environment.GetEnvironmentVariable("VTE_VERSION") is not null)
            return VteReflowStrategy.Instance;

        var term = Environment.GetEnvironmentVariable("TERM");

        if (term is not null && term.StartsWith("foot", StringComparison.OrdinalIgnoreCase))
            return FootReflowStrategy.Instance;

        // Default: no reflow (conservative — avoids surprising behavior in unknown terminals)
        return NoReflowStrategy.Instance;
    }
}
