namespace Hex1b.Tokens;

/// <summary>
/// Token for DECSCA (Select Character Protection Attribute): CSI Ps " q
/// </summary>
/// <remarks>
/// <para>
/// Controls whether subsequently printed characters are protected from
/// selective erase operations (DECSED/DECSEL).
/// </para>
/// <list type="bullet">
/// <item><description><see cref="Mode"/> = 0 or 2: Unprotected (default)</description></item>
/// <item><description><see cref="Mode"/> = 1: Protected</description></item>
/// </list>
/// </remarks>
/// <param name="Mode">The protection mode: 0/2 = off, 1 = on.</param>
public sealed record DecscaToken(int Mode) : AnsiToken;
