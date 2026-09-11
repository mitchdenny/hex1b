namespace Hex1b.Tokens;

/// <summary>
/// Represents an XTWINOPS window operation request: <c>CSI Ps t</c>.
/// </summary>
/// <param name="Operation">The requested operation code (for example, 14, 16, or 18).</param>
/// <remarks>
/// <para>
/// Only the read-only report operations relevant to Sixel cell-metrics discovery are
/// recognized here:
/// </para>
/// <list type="bullet">
///   <item><description>14 — report the text area size in pixels; reply <c>CSI 4 ; height ; width t</c>.</description></item>
///   <item><description>16 — report the character cell size in pixels; reply <c>CSI 6 ; height ; width t</c>.</description></item>
///   <item><description>18 — report the text area size in characters; reply <c>CSI 8 ; rows ; cols t</c>.</description></item>
/// </list>
/// <para>
/// Other <c>CSI Ps t</c> operations (window manipulation, iconify, move, etc.) are not
/// modeled by this token and remain <see cref="UnrecognizedSequenceToken"/>. Whether
/// <see cref="Hex1bTerminal"/> answers this token at all depends on query ownership:
/// see <see cref="DeviceAttributesQueryToken"/> for the shared rule.
/// </para>
/// </remarks>
public sealed record WindowOperationToken(int Operation) : AnsiToken;
