using Hex1b.Theming;

namespace Hex1b;

/// <summary>
/// Represents a single cell in the virtual terminal screen buffer.
/// </summary>
public readonly record struct TerminalCell(char Character, Hex1bColor? Foreground, Hex1bColor? Background)
{
    public static readonly TerminalCell Empty = new(' ', null, null);
}
