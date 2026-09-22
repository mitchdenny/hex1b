using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// Identifies where opaque (<c>P2</c> 0 or 2) Sixel backgrounds come from.
/// </summary>
internal enum SixelBackgroundSource
{
    /// <summary>
    /// Use the terminal background captured when the graphic was created.
    /// </summary>
    CapturedTerminalBackground,

    /// <summary>
    /// Use Sixel color register zero, the xterm/WezTerm compatibility behavior.
    /// </summary>
    PaletteRegisterZero,
}
