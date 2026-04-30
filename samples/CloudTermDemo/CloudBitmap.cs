namespace CloudTermDemo;

/// <summary>
/// A cloud-computing-style cloud icon as a greyscale pixel bitmap for half-block rendering.
/// Each value is 0 (transparent) to 255 (fully opaque white).
/// The bitmap is 60 pixels wide × 30 pixels tall, rendering as 60×15 terminal cells.
/// </summary>
/// <remarks>
/// Shaped after the classic cloud computing icon silhouette: flat bottom, four bumps
/// along the top (small left, two medium center, one large right-top), roughly 4:3
/// aspect ratio — similar to the Azure/AWS/GCP cloud icon style.
/// </remarks>
internal static class CloudBitmap
{
    public const int Width = 60;
    public const int Height = 30;

    public static readonly byte[,] Pixels = GenerateCloud();

    private static byte[,] GenerateCloud()
    {
        var pixels = new byte[Height, Width];

        // Cloud computing icon shape: 4 bumps (small-left, med, med-large, large-right)
        // with a flat bottom. Coordinates designed for a 60×30 canvas.
        (double cx, double cy, double rx, double ry, double strength)[] blobs =
        [
            // ── Flat bottom / main body ──
            // Wide horizontal ellipse that forms the flat base
            (30, 22, 24, 5, 1.0),
            (30, 20, 22, 6, 1.0),

            // ── Bump 1: small left bump ──
            (12, 15, 7, 6, 1.0),
            (14, 13, 6, 5, 0.9),

            // ── Bump 2: medium-left center bump ──
            (22, 10, 8, 7, 1.0),
            (24, 8, 7, 6, 0.95),

            // ── Bump 3: medium-right center bump ──
            (36, 8, 9, 7, 1.0),
            (34, 6, 7, 5.5, 0.9),

            // ── Bump 4: large right bump (tallest, most prominent) ──
            (46, 10, 10, 8, 1.0),
            (47, 6, 8, 6, 1.0),
            (48, 4, 6, 4.5, 0.95),

            // ── Fill: connect bumps smoothly into the body ──
            (20, 16, 14, 7, 1.0),
            (35, 14, 14, 8, 1.0),
            (28, 12, 12, 6, 0.95),
            (42, 12, 10, 6, 0.95),

            // ── Bottom fill: ensure the base is solid and flat ──
            (30, 24, 22, 3, 0.9),
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
                        var edge = 1.0 - dist;
                        var alpha = edge * edge * (3.0 - 2.0 * edge); // smoothstep
                        alpha *= strength;
                        maxAlpha = Math.Max(maxAlpha, alpha);
                    }
                }

                // Flatten: boost mid-values toward solid white, soft edges at perimeter only
                var flattened = maxAlpha > 0.12
                    ? Math.Clamp((maxAlpha - 0.12) / 0.45, 0, 1)
                    : 0.0;
                flattened = flattened * flattened * (3.0 - 2.0 * flattened); // extra smoothstep
                pixels[y, x] = (byte)(Math.Clamp(flattened, 0, 1) * 255);
            }
        }

        // Trim: ensure bottom row is flat — zero out anything below the body
        for (var x = 0; x < Width; x++)
        {
            for (var y = 26; y < Height; y++)
                pixels[y, x] = 0;
        }

        return pixels;
    }
}
