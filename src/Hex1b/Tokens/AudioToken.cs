namespace Hex1b.Tokens;

/// <summary>
/// Represents an audio protocol command: ESC _A ... ST
/// </summary>
/// <param name="ControlData">
/// The control data portion (comma-separated key=value pairs) before the semicolon.
/// </param>
/// <param name="Payload">
/// The base64-encoded payload after the semicolon, or empty if no payload.
/// </param>
/// <remarks>
/// <para>
/// Audio protocol uses APC (Application Programming Command) escape sequences with an 'A' prefix:
/// <c>ESC _A &lt;control data&gt; ; &lt;payload&gt; ESC \</c>
/// </para>
/// <para>
/// Modeled after KGP (<see cref="KgpToken"/>) which uses the 'G' prefix.
/// </para>
/// </remarks>
public sealed record AudioToken(string ControlData, string Payload) : AnsiToken;
