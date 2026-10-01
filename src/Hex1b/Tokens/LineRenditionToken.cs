namespace Hex1b.Tokens;

/// <summary>
/// Sets the current row's DEC line rendition (ESC # 3, 4, 5, or 6).
/// </summary>
/// <param name="Rendition">The row's width and height.</param>
public sealed record LineRenditionToken(LineRendition Rendition) : AnsiToken;
