using Hex1b.Reflow;
using Hex1b.Sixel;

namespace Hex1b;

/// <summary>
/// A single tracked placement participating in a Sixel reflow pass: its
/// reflow anchor id, the placement geometry to re-derive from, and (when it
/// originated from history) the retained-window descriptor to slice before
/// re-deriving. Mirrors <c>KgpTerminalGraphicsState.ReflowPlacement</c>.
/// </summary>
internal readonly record struct SixelReflowPlacement(
    int Id,
    SixelPlacement Placement,
    SixelHistoryPlacement? History);
