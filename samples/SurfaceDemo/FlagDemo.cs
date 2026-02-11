using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace SurfaceDemo;

/// <summary>
/// Flag demo - simulates a flag waving in the wind using partial-height block
/// characters along the top and bottom edges driven by a sine wave, with
/// depth shading to give the impression of fabric folds.
/// </summary>
public static class FlagDemo
{
    private static double _phase;

    // Wave parameters
    private const double Frequency = 0.35;
    private const double Speed = -0.008; // negative = flows right-to-left
    private const double AmplitudeMin = 1.0;  // eighths at left (pole) edge
    private const double AmplitudeMax = 7.0;  // eighths at right (free) edge
    private const double FlutterFreq = 0.17;  // secondary wave for organic variance
    private const double FlutterAmount = 1.0; // max extra eighths from flutter

    // Base cream color
    private const byte BaseR = 220;
    private const byte BaseG = 210;
    private const byte BaseB = 180;

    // Shading range (added/subtracted from base)
    private const int ShadeRange = 6;

    // Lower-block characters indexed 0..8 (0 = space, 8 = full block)
    private static readonly string[] LowerBlocks = [" ", "▁", "▂", "▃", "▄", "▅", "▆", "▇", "█"];

    public static IEnumerable<SurfaceLayer> BuildLayers(SurfaceLayerContext ctx)
    {
        _phase += Speed;

        yield return ctx.Layer(surface =>
        {
            int w = surface.Width;
            int h = surface.Height;

            // Flag is ~1/4 of surface, centered
            int flagW = w / 2;
            int flagH = h / 2;
            int offsetX = (w - flagW) / 2;
            int offsetY = (h - flagH) / 2;

            for (int fx = 0; fx < flagW; fx++)
            {
                int x = offsetX + fx;

                // Sine wave value for this column
                double angle = fx * Frequency + _phase;
                double wave = Math.Sin(angle);           // -1..1

                // Depth shading: wave peak (high amplitude) = lightest, trough = darkest
                var shade = (int)(wave * ShadeRange);
                byte r = ClampByte(BaseR + shade);
                byte g = ClampByte(BaseG + shade);
                byte b = ClampByte(BaseB + shade);
                var bgColor = Hex1bColor.FromRgb(r, g, b);

                // Per-column amplitude: grows from left (pole) to right (free edge),
                // with a secondary sine for organic flutter
                double t = (double)fx / Math.Max(flagW - 1, 1); // 0..1 across flag
                double baseAmp = AmplitudeMin + (AmplitudeMax - AmplitudeMin) * t;
                double flutter = Math.Sin(fx * FlutterFreq + _phase * 2.3) * FlutterAmount * t;
                double amplitude = Math.Max(0, baseAmp + flutter);

                // Wave offset in eighths-of-a-cell
                double edgeEighths = (wave + 1.0) / 2.0 * amplitude;
                int blockIndex = Math.Clamp((int)Math.Round(edgeEighths), 0, 8);

                // Fill interior cells (all rows between top and bottom edges)
                for (int fy = 1; fy < flagH - 1; fy++)
                {
                    surface[x, offsetY + fy] = new SurfaceCell(" ", null, bgColor);
                }

                // --- Bottom edge ---
                // Bottom should mirror the top: when the top wave dips down,
                // the bottom also dips down (recedes upward).
                int bottomY = offsetY + flagH - 1;
                int bottomBlockIndex = 8 - blockIndex;
                int bottomComplement = 8 - bottomBlockIndex;
                if (bottomComplement == 0)
                {
                    surface[x, bottomY] = new SurfaceCell(" ", null, bgColor);
                }
                else if (bottomComplement == 8)
                {
                    surface[x, bottomY] = new SurfaceCell(" ", null, Hex1bColor.Black);
                }
                else
                {
                    // Lower block fg = empty space color (below flag), bg = flag color (top portion)
                    surface[x, bottomY] = new SurfaceCell(
                        LowerBlocks[bottomComplement], Hex1bColor.Black, bgColor);
                }

                // --- Top edge ---
                // Flag extends from bottom up. Lower blocks fill from bottom as fg,
                // so use blockIndex directly with fg=flag, bg=null.
                int topY = offsetY;
                if (blockIndex == 0)
                {
                    surface[x, topY] = new SurfaceCell(" ", null, null);
                }
                else if (blockIndex == 8)
                {
                    surface[x, topY] = new SurfaceCell(" ", null, bgColor);
                }
                else
                {
                    // Block portion (bottom) = flag, gap portion (top) = null
                    surface[x, topY] = new SurfaceCell(
                        LowerBlocks[blockIndex], bgColor, null);
                }
            }
        });
    }

    private static byte ClampByte(int value) =>
        (byte)Math.Clamp(value, 0, 255);
}
