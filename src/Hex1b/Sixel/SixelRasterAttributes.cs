using Hex1b.Tokens;

namespace Hex1b.Sixel;

internal readonly record struct SixelRasterAttributes(
    int Pan,
    int Pad,
    int Ph,
    int Pv);
