using System.Text;
using Hex1b.Theming;
using Hex1b.Tokens;

namespace Hex1b.Automation;

/// <summary>
/// Options for ANSI rendering of terminal regions.
/// </summary>
public class TerminalAnsiOptions
{
    /// <summary>
    /// Default options for ANSI rendering.
    /// </summary>
    public static readonly TerminalAnsiOptions Default = new();

    /// <summary>
    /// Whether to include escape sequences to clear the screen and reset cursor.
    /// Default is false to just render the content.
    /// </summary>
    public bool IncludeClearScreen { get; set; } = false;

    /// <summary>
    /// Whether to reset all attributes at the end of the output.
    /// Default is true to ensure terminal returns to normal state.
    /// </summary>
    public bool ResetAtEnd { get; set; } = true;

    /// <summary>
    /// Whether to render null characters as spaces.
    /// Default is false to skip them entirely.
    /// </summary>
    public bool RenderNullAsSpace { get; set; } = false;

    /// <summary>
    /// Whether to include cursor positioning at the end (for snapshots).
    /// Default is true.
    /// </summary>
    public bool IncludeCursorPosition { get; set; } = true;

    /// <summary>
    /// Whether to include a trailing newline at the end of output.
    /// Useful when the output will be written to a file.
    /// Default is true.
    /// </summary>
    public bool IncludeTrailingNewline { get; set; } = true;
}
