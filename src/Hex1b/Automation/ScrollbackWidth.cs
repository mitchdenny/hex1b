namespace Hex1b.Automation;

/// <summary>
/// Controls how scrollback line widths are adapted when included in a snapshot.
/// </summary>
public enum ScrollbackWidth
{
    /// <summary>
    /// Truncate or pad scrollback lines to match the current terminal width.
    /// This produces a uniform snapshot where all lines (visible + scrollback) are
    /// the same width, which is simpler for pattern matching and assertions.
    /// </summary>
    CurrentTerminal,

    /// <summary>
    /// Return scrollback lines at their original width when they were captured.
    /// Lines in the buffer may be wider or narrower than the current viewport.
    /// Useful when you need to see the full original content.
    /// </summary>
    Original
}
