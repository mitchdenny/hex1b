using System.Text;
using Hex1b.Layout;

namespace Hex1b.Automation;

/// <summary>
/// Represents a single cell that was traversed during pattern matching.
/// </summary>
/// <param name="X">X coordinate of the cell.</param>
/// <param name="Y">Y coordinate of the cell.</param>
/// <param name="Cell">The terminal cell at this position.</param>
/// <param name="CaptureNames">Names of captures this cell belongs to, or null if none.</param>
public readonly record struct TraversedCell(
    int X,
    int Y,
    TerminalCell Cell,
    IReadOnlySet<string>? CaptureNames);
