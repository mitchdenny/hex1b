using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace BuildLogoDemo;

internal static class BuildLogo
{
    private const char UpperHalf = '▀';
    private const int Cols = 51;
    private const int Rows = 17;

    public const int WidthCells = Cols;
    public const int HeightCells = (Rows + 1) / 2; // 9 rows

    private static readonly Random _random = new();
    private static readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private static double _shimmerStartTime = -10;
    private static double _nextShimmerDelay = 2; // first shimmer after 2s

    private static readonly (byte R, byte G, byte B)?[,] Pixels = new (byte, byte, byte)?[17, 51]
    {
        { (46,152,198), (46,152,198), (46,152,198), (71,138,177), (95,122,152), (119,109,130), (144,94,107), (167,79,84), (191,64,61), null, null, null, (46,152,198), (46,152,198), (46,152,198), (46,152,198), null, null, null, (235,37,20), (235,37,20), (235,37,20), null, null, (240,181,42), (240,181,42), (240,181,42), null, null, (46,152,198), (46,152,198), (16,85,110), null, null, null, null, null, null, null, (235,37,20), (235,37,20), (235,37,20), (219,56,23), (203,74,24), (188,93,29), (172,112,32), (156,130,34), (141,184,44), (122,183,44), null, null },
        { (46,152,198), null, null, null, null, null, null, null, (206,72,26), (236,52,21), null, null, (240,181,42), null, null, (240,181,42), null, null, null, (109,184,45), null, null, (46,152,198), null, (139,183,43), null, null, (47,153,200), null, (112,187,52), null, (19,92,122), null, null, null, null, null, null, null, (240,181,42), null, null, null, null, null, null, null, (235,37,20), (122,183,44), null, null },
        { (235,37,20), null, null, null, null, null, null, null, null, (223,53,21), null, null, (240,181,42), null, null, (240,181,42), null, null, null, (122,168,41), null, null, (46,152,198), null, (139,183,43), null, null, (47,153,200), null, (112,187,52), null, (19,92,122), null, null, null, null, null, null, null, (240,181,42), null, null, null, null, null, null, null, null, null, (109,184,45), null },
        { (236,58,22), null, null, (109,184,45), (235,37,20), (235,37,20), (235,37,20), (235,37,20), null, null, (106,181,58), null, (240,181,42), null, null, (240,181,42), null, null, null, (139,150,39), null, null, (46,152,198), null, (139,183,43), null, null, (47,153,200), null, (112,187,52), null, (60,110,13), null, null, null, null, null, null, null, (240,181,42), null, null, (109,184,45), (109,184,45), (109,184,45), (126,183,45), (144,182,44), null, null, null, (109,184,45) },
        { (237,88,27), null, null, (92,174,87), null, null, null, (240,181,42), null, null, (112,182,45), null, (240,181,42), null, null, (235,37,20), null, null, null, (156,130,34), null, null, (70,155,179), null, (139,183,43), null, null, (70,155,179), null, (112,187,52), null, (54,106,27), null, null, null, null, null, null, null, (240,181,42), null, null, (167,79,84), null, null, null, (240,181,42), (235,37,20), null, null, (46,152,198) },
        { (239,118,31), null, null, (100,180,69), null, null, null, (240,181,42), null, null, (112,183,43), null, (222,177,58), null, null, (235,37,20), null, null, null, (172,112,32), null, null, (93,160,158), null, (148,169,41), null, null, (93,160,158), null, (107,182,68), null, (50,106,41), null, null, null, null, null, null, null, (239,167,34), null, null, (141,94,105), null, null, null, null, (240,181,42), null, null, (46,152,198) },
        { (240,147,35), null, null, (162,183,44), (240,181,42), (240,181,42), (240,181,42), null, null, null, (112,182,45), null, (197,174,79), null, null, (235,37,20), null, null, null, (188,93,29), null, null, (121,163,140), null, (160,149,37), null, null, (121,163,140), null, (100,178,88), null, (44,101,57), null, null, null, null, null, null, null, (240,149,36), null, null, (138,96,113), null, null, null, null, (240,181,42), null, null, (70,155,179) },
        { (241,177,39), null, null, null, null, null, null, null, null, (111,181,41), null, null, (171,169,97), null, null, (235,37,20), null, null, null, (203,74,24), null, null, (147,166,117), null, (174,131,34), null, null, (148,167,120), null, (91,173,105), null, (37,99,69), null, null, null, null, null, null, null, (238,129,31), null, null, (119,109,130), null, null, null, null, (217,177,61), null, null, (97,158,158) },
        { (240,181,40), null, null, null, null, null, null, null, null, (112,180,44), null, null, (153,165,108), null, null, (235,37,20), null, null, null, (209,71,26), null, null, (150,166,112), null, (178,127,35), null, null, (148,166,112), null, (84,171,124), null, (39,100,75), null, null, null, null, null, null, null, (240,125,34), null, null, (95,122,152), null, null, null, null, (207,177,69), null, null, (103,158,153) },
        { (240,181,42), null, null, null, null, null, null, null, null, (112,182,45), null, null, (145,167,119), null, null, (235,37,20), null, null, null, (219,56,23), null, null, (171,169,97), null, (185,112,32), null, null, (171,169,97), null, (78,167,137), null, (34,98,83), null, null, null, null, null, null, null, (239,111,29), null, null, (71,138,177), null, null, null, null, (193,173,82), null, null, (121,163,140) },
        { (240,181,42), null, null, (109,184,45), (235,37,20), (235,37,20), (235,37,22), (235,37,22), null, null, (114,181,45), null, (121,163,140), null, null, (236,36,22), null, null, null, (235,37,20), null, null, (197,173,81), null, (199,93,29), null, null, (197,173,81), null, (77,167,143), null, (28,96,98), null, null, null, null, null, null, null, (237,92,27), null, null, (46,152,198), null, null, null, null, (168,170,101), null, null, (145,167,119) },
        { (240,181,42), null, null, (101,160,156), null, null, null, (240,181,42), null, null, (112,182,45), null, (95,159,158), null, null, (236,52,21), null, null, (240,181,42), (235,37,20), null, null, (222,177,58), null, (210,75,25), null, null, (222,177,58), null, (68,164,163), null, (19,91,124), null, null, null, null, null, null, null, (237,75,25), null, null, (46,152,198), null, null, null, null, (143,166,121), null, null, (171,169,97) },
        { (240,181,42), null, null, (75,156,176), null, null, null, (240,181,42), null, null, (110,182,49), null, (70,155,179), null, null, (235,37,20), null, null, (240,181,42), (235,37,20), null, null, (240,181,42), null, (224,56,22), null, null, (241,182,43), null, (57,158,181), null, (19,91,124), null, null, null, null, null, null, null, (235,57,21), null, null, (46,152,198), null, null, null, (240,181,42), (116,109,134), null, null, (197,174,79) },
        { (240,181,42), null, null, (46,152,198), (188,174,83), (213,177,64), (239,181,44), (219,57,21), null, null, (96,175,80), null, (235,37,20), null, null, (124,165,42), (240,181,42), (240,181,42), (235,37,20), null, null, (240,181,42), (240,181,42), null, (235,37,20), null, null, (241,182,43), null, (51,155,202), null, (58,110,15), (109,184,45), (109,184,45), (124,165,42), (142,146,38), (119,109,130), (144,94,107), null, (235,37,20), null, null, (144,182,44), (240,181,42), (240,181,42), (240,181,42), (235,37,20), null, null, null, (222,177,58) },
        { (240,181,42), null, null, null, (184,68,66), (209,53,45), (234,39,21), null, null, null, (83,170,113), null, (235,37,20), null, null, null, (235,37,20), (235,37,20), null, null, null, (240,181,42), (240,181,42), null, (235,37,20), null, null, (240,181,42), null, (238,43,24), null, null, null, null, null, null, null, (46,152,198), null, (46,152,198), null, null, null, (235,57,21), (235,37,20), (235,37,20), null, null, null, (240,181,42), null },
        { (240,181,42), null, null, null, null, null, null, null, (109,184,45), (240,181,42), null, null, null, (62,159,163), null, null, null, null, null, null, null, (240,181,42), null, null, (235,37,20), null, null, (240,181,42), null, (238,43,24), null, null, null, null, null, null, null, (46,152,198), null, (46,152,198), null, null, null, null, null, null, null, null, (240,181,42), (240,181,42), null },
        { (241,169,39), (235,37,20), (191,64,61), (167,79,84), (144,94,107), (121,108,130), (80,132,172), (48,151,198), (46,152,198), null, null, null, null, null, (116,109,134), (139,96,111), (163,80,88), (187,67,64), (211,51,41), (235,37,20), (46,152,198), null, null, null, (235,37,20), (235,37,20), (109,184,45), (46,152,198), null, (241,147,38), (242,128,34), (235,92,24), (236,76,23), (235,57,21), (235,37,20), (235,37,20), (235,37,20), null, null, (216,49,37), (191,64,61), (167,79,84), (144,94,107), (119,109,130), (95,122,152), (71,138,177), (46,152,198), (46,152,198), (240,181,42), null, null }
    };

    public static IEnumerable<SurfaceLayer> BuildLayers(SurfaceLayerContext ctx)
    {
        // Base layer: draw the BUILD logo with half-block chars
        yield return ctx.Layer(surface =>
        {
            for (int cellRow = 0; cellRow < HeightCells && cellRow < surface.Height; cellRow++)
            {
                int topPixelRow = cellRow * 2;
                int botPixelRow = topPixelRow + 1;

                for (int col = 0; col < Cols && col < surface.Width; col++)
                {
                    var topColor = topPixelRow < Rows ? Pixels[topPixelRow, col] : null;
                    var botColor = botPixelRow < Rows ? Pixels[botPixelRow, col] : null;

                    if (topColor is null && botColor is null)
                        continue;

                    var fg = topColor.HasValue
                        ? Hex1bColor.FromRgb(topColor.Value.R, topColor.Value.G, topColor.Value.B)
                        : Hex1bColor.Black;

                    var bg = botColor.HasValue
                        ? Hex1bColor.FromRgb(botColor.Value.R, botColor.Value.G, botColor.Value.B)
                        : Hex1bColor.Black;

                    if (topColor.HasValue && botColor.HasValue &&
                        topColor.Value.R == botColor.Value.R &&
                        topColor.Value.G == botColor.Value.G &&
                        topColor.Value.B == botColor.Value.B)
                    {
                        surface[col, cellRow] = new SurfaceCell(" ", null, bg);
                    }
                    else
                    {
                        surface[col, cellRow] = new SurfaceCell(
                            UpperHalf.ToString(), fg, bg);
                    }
                }
            }
        });

        // Shimmer layer: diagonal wave that boosts intensity of filled cells
        double now = _clock.Elapsed.TotalSeconds;
        double elapsed = now - _shimmerStartTime;

        // Check if it's time to start a new shimmer
        if (elapsed > _nextShimmerDelay + 1.5) // 1.5s for wave to fully pass
        {
            _shimmerStartTime = now;
            _nextShimmerDelay = 5 + _random.NextDouble() * 5; // 5-10s until next
            elapsed = 0;
        }

        // Wave duration: ~1.0s to sweep across the diagonal
        const double waveDuration = 1.0;
        double waveProgress = elapsed / waveDuration;

        if (waveProgress >= 0 && waveProgress < 1.5) // active shimmer window
        {
            double capturedProgress = waveProgress;
            yield return ctx.Layer(computeCtx =>
            {
                var below = computeCtx.GetBelow();

                // Skip transparent/empty cells
                if (below.Foreground is null && below.Background is null)
                    return below;
                if (below.Character == " " && below.Background is null)
                    return below;

                // Diagonal position normalized 0-1 across the surface
                double diag = ((double)computeCtx.X / Cols + (double)computeCtx.Y / HeightCells) / 2.0;

                // Wave front position moves from 0 to 1 over the duration
                double waveFront = capturedProgress;
                double dist = Math.Abs(diag - waveFront);

                // Shimmer width (how wide the bright band is)
                const double waveWidth = 0.15;

                if (dist > waveWidth)
                    return below;

                // Intensity boost: peaks at wave center, falls off to edges
                double intensity = 1.0 - (dist / waveWidth);
                intensity = intensity * intensity; // ease-in curve
                double boost = 1.0 + intensity * 0.6; // up to 60% brighter

                var newFg = BoostColor(below.Foreground, boost);
                var newBg = BoostColor(below.Background, boost);

                return below with { Foreground = newFg, Background = newBg };
            });
        }
    }

    private static Hex1bColor? BoostColor(Hex1bColor? color, double boost)
    {
        if (color is null) return null;

        var c = color.Value;
        return Hex1bColor.FromRgb(
            (byte)Math.Min(255, (int)(c.R * boost)),
            (byte)Math.Min(255, (int)(c.G * boost)),
            (byte)Math.Min(255, (int)(c.B * boost)));
    }
}
