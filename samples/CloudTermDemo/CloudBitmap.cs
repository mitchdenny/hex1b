namespace CloudTermDemo;

/// <summary>
/// A fluffy cumulus cloud as a greyscale pixel bitmap for half-block rendering.
/// Each value is 0 (transparent) to 255 (fully opaque white).
/// The bitmap is 48 pixels wide × 24 pixels tall, rendering as 48×12 terminal cells.
/// </summary>
/// <remarks>
/// Designed to resemble the classic "cloud computing" icon — a rounded cumulus shape
/// with soft edges achieved via graduated alpha values.
/// </remarks>
internal static class CloudBitmap
{
    public const int Width = 48;
    public const int Height = 24;

    // Greyscale alpha values: 0=transparent, 255=solid white
    // Soft edges use intermediate values (40-180) for the half-block blending
    public static readonly byte[,] Pixels = GenerateCloud();

    private static byte[,] GenerateCloud()
    {
        var pixels = new byte[Height, Width];

        // Define the cloud as overlapping ellipses with soft falloff
        // Each ellipse: (centerX, centerY, radiusX, radiusY, brightness)
        (double cx, double cy, double rx, double ry, double strength)[] blobs =
        [
            // Main body — large central mass
            (24, 14, 18, 7, 1.0),

            // Top billow — the classic bumpy top
            (18, 8, 8, 6, 1.0),
            (28, 6, 10, 7, 1.0),
            (36, 9, 7, 5.5, 0.95),
            (12, 11, 7, 5, 0.9),

            // Upper detail bumps
            (22, 5, 5, 4, 0.85),
            (33, 5, 5, 3.5, 0.8),
            (15, 7, 5, 4, 0.8),

            // Fill gaps in the body
            (24, 11, 14, 6, 0.95),
            (20, 13, 12, 5, 0.9),
            (30, 12, 10, 5, 0.9),

            // Soften the flat bottom slightly
            (24, 17, 16, 3, 0.7),
        ];

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var maxAlpha = 0.0;

                foreach (var (cx, cy, rx, ry, strength) in blobs)
                {
                    var dx = (x - cx) / rx;
                    var dy = (y - cy) / ry;
                    var dist = Math.Sqrt(dx * dx + dy * dy);

                    if (dist < 1.0)
                    {
                        // Smooth falloff using smoothstep-like curve
                        var edge = 1.0 - dist;
                        var alpha = edge * edge * (3.0 - 2.0 * edge); // smoothstep
                        alpha *= strength;
                        maxAlpha = Math.Max(maxAlpha, alpha);
                    }
                }

                // Flatten: boost mid-values toward white so the cloud reads as solid
                // with soft edges only at the perimeter
                var flattened = maxAlpha > 0.15
                    ? Math.Clamp((maxAlpha - 0.15) / 0.55, 0, 1)  // remap 0.15–0.70 → 0–1
                    : 0.0;
                flattened = flattened * flattened * (3.0 - 2.0 * flattened); // extra smoothstep
                pixels[y, x] = (byte)(Math.Clamp(flattened, 0, 1) * 255);
            }
        }

        return pixels;
    }
}
