using Hex1b.Reflow;
using Hex1b.Sixel;

namespace Hex1b;

internal readonly record struct SixelPlacementCreationResult(
    SixelPlacement Placement,
    bool Retained);
