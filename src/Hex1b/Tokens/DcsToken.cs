namespace Hex1b.Tokens;

/// <summary>
/// Represents a Device Control String (DCS): ESC P ... ST
/// </summary>
/// <param name="Payload">
/// The complete DCS sequence including the ESC P header and ST terminator.
/// </param>
/// <remarks>
/// <para>
/// The most common DCS use in Hex1b is Sixel graphics (ESC P q ... ST).
/// We preserve the entire payload because:
/// <list type="bullet">
///   <item>Sixel data can be large and complex</item>
///   <item>Filters typically pass through or drop entirely</item>
///   <item>Re-parsing would be wasteful</item>
/// </list>
/// </para>
/// </remarks>
public sealed record DcsToken(string Payload) : AnsiToken;
