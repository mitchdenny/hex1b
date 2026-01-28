using Hex1b;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace SurfaceDemo;

/// <summary>
/// Noise demo - animated random noise pattern.
/// </summary>
public static class NoiseDemo
{
    public static IEnumerable<SurfaceLayer> BuildLayers(SurfaceLayerContext ctx, Random random)
    {
        yield return ctx.Layer(surface =>
        {
            for (int y = 0; y < surface.Height; y++)
            {
                for (int x = 0; x < surface.Width; x++)
                {
                    // Random grayscale noise
                    var v = (byte)random.Next(256);
                    surface[x, y] = new SurfaceCell(" ", null, Hex1bColor.FromRgb(v, v, v));
                }
            }
        });
    }
}
