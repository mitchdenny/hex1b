using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// Identifies how DECSDM (private mode 80) set/reset maps onto Sixel scrolling.
/// </summary>
/// <remarks>
/// The VT340 manual and hardware tests make <c>CSI ? 80 h</c> enable Sixel
/// scrolling. Current xterm documentation and implementation interpret the same
/// mode in the opposite direction. Hex1b selects the DEC interpretation and keeps
/// the inversion here rather than in a terminal-name check.
/// </remarks>
internal enum SixelDecsdmPolarity
{
    /// <summary>
    /// <c>CSI ? 80 h</c> enables Sixel scrolling; <c>CSI ? 80 l</c> disables it.
    /// </summary>
    Dec,

    /// <summary>
    /// <c>CSI ? 80 h</c> disables Sixel scrolling; <c>CSI ? 80 l</c> enables it.
    /// </summary>
    Xterm,
}
