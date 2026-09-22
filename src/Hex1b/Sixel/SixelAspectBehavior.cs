using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// Identifies how a reference implementation applies Sixel pixel-aspect metadata.
/// </summary>
internal enum SixelAspectBehavior
{
    /// <summary>Apply the DEC macro and DECGRA pixel aspect ratio.</summary>
    Dec,

    /// <summary>Render logical Sixel pixels as square pixels.</summary>
    SquarePixels,
}
