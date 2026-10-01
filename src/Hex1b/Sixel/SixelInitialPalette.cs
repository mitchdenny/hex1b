using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// Identifies the initial Sixel color-register map used by a reference profile.
/// </summary>
internal enum SixelInitialPalette
{
    /// <summary>Use the DEC VT340 palette with Hex1b's documented 256-color extension.</summary>
    DecVt340Extended,

    /// <summary>Use the palette initialized by WezTerm 20240203.</summary>
    WezTerm20240203,
}
