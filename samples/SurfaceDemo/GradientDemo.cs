using Hex1b;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace SurfaceDemo;

/// <summary>
/// Gradient demo - animated color gradients across the surface.
/// </summary>
public static class GradientDemo
{
    private static double _phase = 0;
    
    public static IEnumerable<SurfaceLayer> BuildLayers(SurfaceLayerContext ctx)
    {
        // Advance animation phase
        _phase += 0.02;
        if (_phase > 1) _phase -= 1;
        
        yield return ctx.Layer(surface =>
        {
            for (int y = 0; y < surface.Height; y++)
            {
                for (int x = 0; x < surface.Width; x++)
                {
                    // Animated diagonal wave gradient
                    var t = (x + y) / (double)(surface.Width + surface.Height - 2);
                    
                    // Add sine wave for animation
                    var wave = Math.Sin((t + _phase) * Math.PI * 4) * 0.5 + 0.5;
                    
                    // Cycle through colors: purple -> cyan -> magenta -> purple
                    var hue = (_phase + t * 0.5) % 1.0;
                    var (r, g, b) = HsvToRgb(hue, 0.7, 0.3 + wave * 0.7);
                    
                    surface[x, y] = new SurfaceCell(" ", null, Hex1bColor.FromRgb(r, g, b));
                }
            }
        });
    }
    
    private static (byte r, byte g, byte b) HsvToRgb(double h, double s, double v)
    {
        var hi = (int)(h * 6) % 6;
        var f = h * 6 - Math.Floor(h * 6);
        var p = v * (1 - s);
        var q = v * (1 - f * s);
        var t = v * (1 - (1 - f) * s);
        
        var (r, g, b) = hi switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q)
        };
        
        return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }
}
