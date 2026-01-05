namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI Scroll Up (SU) command: ESC [ n S
/// Scrolls the content up by n lines, inserting blank lines at the bottom.
/// </summary>
/// <param name="Count">Number of lines to scroll. Default is 1.</param>
public sealed record ScrollUpToken(int Count = 1) : AnsiToken;

/// <summary>
/// Represents a CSI Scroll Down (SD) command: ESC [ n T
/// Scrolls the content down by n lines, inserting blank lines at the top.
/// </summary>
/// <param name="Count">Number of lines to scroll. Default is 1.</param>
public sealed record ScrollDownToken(int Count = 1) : AnsiToken;

/// <summary>
/// Represents a CSI Insert Line (IL) command: ESC [ n L
/// Inserts n blank lines at the cursor position, pushing existing lines down.
/// </summary>
/// <param name="Count">Number of lines to insert. Default is 1.</param>
public sealed record InsertLinesToken(int Count = 1) : AnsiToken;

/// <summary>
/// Represents a CSI Delete Line (DL) command: ESC [ n M
/// Deletes n lines at the cursor position, pulling lines up from below.
/// </summary>
/// <param name="Count">Number of lines to delete. Default is 1.</param>
public sealed record DeleteLinesToken(int Count = 1) : AnsiToken;

/// <summary>
/// Represents an Index (IND) command: ESC D
/// Moves the cursor down one line, scrolling if at the bottom margin.
/// </summary>
public sealed record IndexToken : AnsiToken
{
    /// <summary>Singleton instance.</summary>
    public static readonly IndexToken Instance = new();
    private IndexToken() { }
}

/// <summary>
/// Represents a Reverse Index (RI) command: ESC M
/// Moves the cursor up one line, scrolling if at the top margin.
/// </summary>
public sealed record ReverseIndexToken : AnsiToken
{
    /// <summary>Singleton instance.</summary>
    public static readonly ReverseIndexToken Instance = new();
    private ReverseIndexToken() { }
}

/// <summary>
/// Represents a CSI Delete Character (DCH) command: ESC [ n P
/// Deletes n characters at cursor, shifting remaining characters left.
/// </summary>
/// <param name="Count">Number of characters to delete. Default is 1.</param>
public sealed record DeleteCharacterToken(int Count = 1) : AnsiToken;

/// <summary>
/// Represents a CSI Insert Character (ICH) command: ESC [ n @
/// Inserts n blank characters at cursor, shifting existing characters right.
/// </summary>
/// <param name="Count">Number of characters to insert. Default is 1.</param>
public sealed record InsertCharacterToken(int Count = 1) : AnsiToken;

/// <summary>
/// Represents a CSI Erase Character (ECH) command: ESC [ n X
/// Erases n characters from cursor without moving cursor or shifting.
/// </summary>
/// <param name="Count">Number of characters to erase. Default is 1.</param>
public sealed record EraseCharacterToken(int Count = 1) : AnsiToken;

/// <summary>
/// Represents a CSI Repeat (REP) command: ESC [ n b
/// Repeats the previous graphic character n times.
/// </summary>
/// <param name="Count">Number of times to repeat. Default is 1.</param>
public sealed record RepeatCharacterToken(int Count = 1) : AnsiToken;
