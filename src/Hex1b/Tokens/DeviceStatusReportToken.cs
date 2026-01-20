namespace Hex1b.Tokens;

/// <summary>
/// Represents a Device Status Report (DSR) request.
/// ESC [ Ps n where Ps is the type of report requested.
/// Common values:
/// - 5: Status report (terminal responds ESC [ 0 n for "ready")
/// - 6: Cursor position report (terminal responds ESC [ row ; col R)
/// </summary>
public record DeviceStatusReportToken(int Type) : AnsiToken
{
    /// <summary>
    /// DSR type 5: Status report request.
    /// </summary>
    public const int StatusReport = 5;
    
    /// <summary>
    /// DSR type 6: Cursor position report request.
    /// </summary>
    public const int CursorPositionReport = 6;
}
