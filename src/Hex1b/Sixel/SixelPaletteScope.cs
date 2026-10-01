using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// Identifies whether color registers are shared by the terminal or private per graphic.
/// </summary>
internal enum SixelPaletteScope
{
    TerminalPersistent,
    PrivatePerGraphic,
}
