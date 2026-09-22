using Hex1b.Reflow;

namespace Hex1b;

/// <summary>
/// A single row stored in the scrollback buffer.
/// </summary>
public readonly record struct ScrollbackRow(
    TerminalCell[] Cells,
    int OriginalWidth,
    DateTimeOffset Timestamp);
