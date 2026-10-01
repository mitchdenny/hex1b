using Hex1b.Reflow;
using Hex1b.Sixel;

namespace Hex1b;

internal enum SixelStateEventKind
{
    ImageAllocated,
    ImageDeduplicated,
    ImageRejected,
    ImageReleased,
    PlacementAdded,
    PlacementDamaged,
    PlacementEvicted,
}
