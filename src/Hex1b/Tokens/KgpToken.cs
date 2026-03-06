namespace Hex1b.Tokens;

/// <summary>
/// Represents a Kitty Graphics Protocol (KGP) command: ESC _G ... ST
/// </summary>
/// <param name="ControlData">
/// The control data portion (comma-separated key=value pairs) before the semicolon.
/// </param>
/// <param name="Payload">
/// The base64-encoded payload after the semicolon, or empty if no payload.
/// </param>
/// <remarks>
/// <para>
/// KGP uses APC (Application Programming Command) escape sequences with a 'G' prefix:
/// <c>ESC _G &lt;control data&gt; ; &lt;payload&gt; ESC \</c>
/// </para>
/// <para>
/// The control data contains comma-separated key=value pairs that describe the
/// graphics command (action, format, dimensions, IDs, etc.).
/// The payload is base64-encoded pixel data or file paths.
/// </para>
/// <para>
/// Protocol specification: https://sw.kovidgoyal.net/kitty/graphics-protocol/
/// </para>
/// </remarks>
public sealed record KgpToken(string ControlData, string Payload) : AnsiToken;
