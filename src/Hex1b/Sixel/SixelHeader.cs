using Hex1b.Tokens;

namespace Hex1b.Sixel;

internal readonly record struct SixelHeader(
    int PixelAspectMacro,
    int BackgroundSelection,
    int HorizontalGridSize,
    SixelAspectRatio AspectRatio,
    SixelBackgroundMode BackgroundMode);
