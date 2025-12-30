namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI cursor shape command (DECSCUSR): ESC [ n SP q
/// </summary>
/// <param name="Shape">
/// The cursor shape code:
/// <list type="bullet">
///   <item>0 = Default (usually blinking block)</item>
///   <item>1 = Blinking block</item>
///   <item>2 = Steady block</item>
///   <item>3 = Blinking underline</item>
///   <item>4 = Steady underline</item>
///   <item>5 = Blinking bar (I-beam)</item>
///   <item>6 = Steady bar (I-beam)</item>
/// </list>
/// </param>
/// <remarks>
/// <para>
/// This is the DECSCUSR (DEC Set Cursor Style) sequence.
/// Note the space before 'q' - it's <c>ESC [ n SP q</c> not <c>ESC [ n q</c>.
/// </para>
/// </remarks>
public sealed record CursorShapeToken(int Shape) : AnsiToken
{
    /// <summary>Default cursor shape (terminal-dependent, usually blinking block).</summary>
    public static readonly CursorShapeToken Default = new(0);
    
    /// <summary>Blinking block cursor.</summary>
    public static readonly CursorShapeToken BlinkingBlock = new(1);
    
    /// <summary>Steady (non-blinking) block cursor.</summary>
    public static readonly CursorShapeToken SteadyBlock = new(2);
    
    /// <summary>Blinking underline cursor.</summary>
    public static readonly CursorShapeToken BlinkingUnderline = new(3);
    
    /// <summary>Steady (non-blinking) underline cursor.</summary>
    public static readonly CursorShapeToken SteadyUnderline = new(4);
    
    /// <summary>Blinking bar (I-beam) cursor.</summary>
    public static readonly CursorShapeToken BlinkingBar = new(5);
    
    /// <summary>Steady (non-blinking) bar (I-beam) cursor.</summary>
    public static readonly CursorShapeToken SteadyBar = new(6);
}
