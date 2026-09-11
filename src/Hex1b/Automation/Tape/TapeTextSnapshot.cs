namespace Hex1b.Automation;

internal static class TapeTextSnapshot
{
    internal static Hex1bTerminalSnapshot FromBufferStart(Hex1bTerminal terminal) => new(terminal,
        terminal.CaptureSnapshotState(0, ScrollbackWidth.CurrentTerminal, textViewportTop: 0),
        ScrollbackWidth.CurrentTerminal, TerminalCell.Empty);

    internal static string BufferText(Hex1bTerminal terminal)
    {
        using var snapshot = FromBufferStart(terminal);
        return string.Join('\n', Enumerable.Range(0, snapshot.Height).Select(row => TrimRow(snapshot.GetLine(row))));
    }

    internal static string TrimRow(string text) => text.TrimEnd(
        ' ', '\t', '\r', '\n', '\v', '\f', '\u00a0', '\u1680', '\u2000', '\u2001', '\u2002',
        '\u2003', '\u2004', '\u2005', '\u2006', '\u2007', '\u2008', '\u2009', '\u200a',
        '\u2028', '\u2029', '\u202f', '\u205f', '\u3000', '\ufeff');
}
