using Hex1b.Tokens;

namespace Hex1b.Sixel;

/// <summary>
/// Whether unpainted Sixel pixels resolve to the captured background color or
/// remain transparent.
/// </summary>
public enum SixelBackgroundMode
{
    /// <summary>Unpainted pixels resolve to the captured background color.</summary>
    Opaque,

    /// <summary>Unpainted pixels remain transparent.</summary>
    Transparent,
}
