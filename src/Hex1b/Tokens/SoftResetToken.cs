namespace Hex1b.Tokens;

/// <summary>
/// Token for DECSTR (Soft Terminal Reset, CSI ! p).
/// </summary>
/// <remarks>
/// A soft reset restores mode state without clearing the screen, the scrollback,
/// or the Sixel color registers, and without moving the cursor.
/// </remarks>
public sealed record SoftResetToken : AnsiToken
{
    /// <summary>
    /// The shared instance. The sequence carries no parameters.
    /// </summary>
    public static readonly SoftResetToken Instance = new();

    private SoftResetToken() { }
}
