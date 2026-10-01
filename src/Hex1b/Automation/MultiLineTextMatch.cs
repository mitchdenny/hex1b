using System.Text;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Represents a text match that may span multiple lines in a terminal region.
/// </summary>
/// <param name="StartLine">The line (Y coordinate) where the match starts.</param>
/// <param name="StartColumn">The starting column (X coordinate) of the match.</param>
/// <param name="EndLine">The line (Y coordinate) where the match ends.</param>
/// <param name="EndColumn">The ending column (X coordinate, exclusive) of the match.</param>
/// <param name="Text">The matched text (including newline characters if multi-line).</param>
public readonly record struct MultiLineTextMatch(int StartLine, int StartColumn, int EndLine, int EndColumn, string Text)
{
    /// <summary>
    /// Gets whether this match spans multiple lines.
    /// </summary>
    public bool IsMultiLine => StartLine != EndLine;

    /// <summary>
    /// Gets the number of lines this match spans.
    /// </summary>
    public int LineCount => EndLine - StartLine + 1;
}
