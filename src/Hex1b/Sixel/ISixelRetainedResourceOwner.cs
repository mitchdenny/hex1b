using Hex1b.Surfaces;

namespace Hex1b.Sixel;

internal interface ISixelRetainedResourceOwner
{
    SixelRasterResult GetRaster(SixelData image);

    SixelPixelBuffer? GetPixels(SixelData image);
}
