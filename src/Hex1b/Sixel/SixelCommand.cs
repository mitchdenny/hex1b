using Hex1b.Tokens;

namespace Hex1b.Sixel;

internal readonly record struct SixelCommand(
    SixelCommandKind Kind,
    int X,
    int Band,
    int Value,
    int RepeatCount,
    SixelPaletteCommand? Palette);
