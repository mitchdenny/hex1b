using Hex1b.Tokens;

namespace Hex1b.Sixel;

internal readonly record struct SixelPaletteCommand(
    int Register,
    SixelColorSpace? ColorSpace,
    int? X,
    int? Y,
    int? Z)
{
    public bool IsDefinition => ColorSpace is not null;
}
