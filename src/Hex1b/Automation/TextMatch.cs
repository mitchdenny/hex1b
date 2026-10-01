using System.Text;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Represents a text match found in a terminal region, with its coordinates.
/// </summary>
/// <param name="Line">The line (Y coordinate) where the match was found.</param>
/// <param name="StartColumn">The starting column (X coordinate) of the match.</param>
/// <param name="EndColumn">The ending column (X coordinate, exclusive) of the match.</param>
/// <param name="Text">The matched text.</param>
public readonly record struct TextMatch(int Line, int StartColumn, int EndColumn, string Text)
{
    /// <summary>
    /// Gets the length of the matched text.
    /// </summary>
    public int Length => EndColumn - StartColumn;
}
