namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI Select Graphic Rendition command (SGR): ESC [ params m
/// </summary>
/// <param name="Parameters">
/// The raw SGR parameter string (e.g., "1;38;2;255;0;0" for bold red).
/// Empty string or null represents reset (ESC[m or ESC[0m).
/// </param>
/// <remarks>
/// <para>
/// We preserve the raw parameter string rather than interpreting it, because:
/// <list type="bullet">
///   <item>SGR sequences can be complex (256-color, RGB, combined attributes)</item>
///   <item>Filters may want to pass through unchanged</item>
///   <item>Preserves the original encoding for faithful serialization</item>
/// </list>
/// </para>
/// <para>
/// Common SGR codes:
/// <list type="bullet">
///   <item>0 = Reset all</item>
///   <item>1 = Bold, 2 = Dim, 3 = Italic, 4 = Underline</item>
///   <item>30-37 = Foreground colors, 40-47 = Background colors</item>
///   <item>38;5;N = 256-color foreground, 38;2;R;G;B = RGB foreground</item>
/// </list>
/// </para>
/// </remarks>
public sealed record SgrToken(string Parameters) : AnsiToken
{
    /// <summary>SGR reset - clears all attributes and colors.</summary>
    public static readonly SgrToken Reset = new("");
}
