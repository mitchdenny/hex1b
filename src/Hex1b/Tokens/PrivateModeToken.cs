namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI private mode set/reset command: ESC [ ? mode h/l
/// </summary>
/// <param name="Mode">The private mode number (e.g., 1049 for alternate screen).</param>
/// <param name="Enable">True for 'h' (set/enable), false for 'l' (reset/disable).</param>
/// <remarks>
/// <para>
/// Common private modes:
/// <list type="bullet">
///   <item>1 = Application cursor keys</item>
///   <item>25 = Show/hide cursor</item>
///   <item>1000 = Mouse tracking (X11)</item>
///   <item>1006 = SGR mouse mode</item>
///   <item>1049 = Alternate screen buffer</item>
///   <item>2004 = Bracketed paste mode</item>
/// </list>
/// </para>
/// </remarks>
public sealed record PrivateModeToken(int Mode, bool Enable) : AnsiToken;
