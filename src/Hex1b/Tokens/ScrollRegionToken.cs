namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI Set Scrolling Region command (DECSTBM): ESC [ top ; bottom r
/// </summary>
/// <param name="Top">1-based top row of the scrolling region. Default is 1 (first row).</param>
/// <param name="Bottom">1-based bottom row of the scrolling region. Default is 0, meaning the last row of the screen.</param>
/// <remarks>
/// <para>
/// Defines a rectangular region of the screen that will scroll. Content outside
/// this region remains fixed. This is commonly used for status bars and headers.
/// </para>
/// <para>
/// When serialized:
/// <list type="bullet">
///   <item>ESC[r → Reset to full screen scrolling</item>
///   <item>ESC[5;20r → Scroll region from row 5 to row 20</item>
/// </list>
/// </para>
/// </remarks>
public sealed record ScrollRegionToken(int Top = 1, int Bottom = 0) : AnsiToken
{
    /// <summary>Reset scrolling region to the full screen.</summary>
    public static readonly ScrollRegionToken Reset = new(1, 0);
}
