using Hex1b.Reflow;
using Hex1b.Sixel;

namespace Hex1b;

internal readonly record struct SixelStateEvent(
    SixelStateEventKind Kind,
    int Count = 1,
    string? Reason = null);
