using System.Diagnostics;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace BuildLogoDemo;

internal static class BuildLogo
{
    private const char UpperHalf = '▀';
    private const int LogoCols = 51;
    private const int LogoRows = 17;

    public const int LogoWidthCells = LogoCols;
    public const int LogoHeightCells = (LogoRows + 1) / 2; // 9 rows

    // Shimmer state
    private static readonly Random _random = new();
    private static readonly Stopwatch _clock = Stopwatch.StartNew();
    private static double _shimmerStartTime = -10;
    private static double _nextShimmerDelay = 2;

    // Perlin noise tables (two fields with different seeds)
    private static readonly int[] _perm1 = BuildPermutationTable(42);
    private static readonly int[] _perm2 = BuildPermutationTable(137);

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
        double time = _clock.Elapsed.TotalSeconds;
        int surfW = ctx.Width;
        int surfH = ctx.Height;

        // Logo position (centered)
        int logoX = (surfW - LogoCols) / 2;
        int logoY = (surfH - LogoHeightCells) / 2;

        // --- Layer 1: Rotating Perlin noise background with half-blocks ---
        double capturedTime = time;
        yield return ctx.Layer(surface =>
        {
            double t = capturedTime;
            double angle1 = t * 0.15;  // slow rotation
            double angle2 = t * -0.10; // counter-rotate, different speed
            double cos1 = Math.Cos(angle1), sin1 = Math.Sin(angle1);
            double cos2 = Math.Cos(angle2), sin2 = Math.Sin(angle2);

            // Cross-fade between two noise fields
            double fadePhase = (Math.Sin(t * 0.3) + 1.0) / 2.0; // 0-1 oscillation

            double centerX = surface.Width / 2.0;
            double centerY = surface.Height; // center in "pixel rows" (2 per cell)

            for (int cellY = 0; cellY < surface.Height; cellY++)
            {
                for (int cellX = 0; cellX < surface.Width; cellX++)
                {
                    // Each cell has 2 vertical pixels
                    int topPixY = cellY * 2;
                    int botPixY = topPixY + 1;

                    var topColor = SampleNoiseColor(cellX, topPixY, centerX, centerY,
                        cos1, sin1, cos2, sin2, fadePhase, t);
                    var botColor = SampleNoiseColor(cellX, botPixY, centerX, centerY,
                        cos1, sin1, cos2, sin2, fadePhase, t);

                    if (topColor.r == botColor.r && topColor.g == botColor.g && topColor.b == botColor.b)
                    {
                        surface[cellX, cellY] = new SurfaceCell(" ", null,
                            Hex1bColor.FromRgb(topColor.r, topColor.g, topColor.b));
                    }
                    else
                    {
                        surface[cellX, cellY] = new SurfaceCell(UpperHalf.ToString(),
                            Hex1bColor.FromRgb(topColor.r, topColor.g, topColor.b),
                            Hex1bColor.FromRgb(botColor.r, botColor.g, botColor.b));
                    }
                }
            }
        });

        // --- Layer 2: Dim noise toward black near the logo ---
        int capLogoX = logoX, capLogoY = logoY;
        yield return ctx.Layer(computeCtx =>
        {
            var below = computeCtx.GetBelow();
            if (below.Background is null) return below;

            // Distance from cell to logo bounding box (in cells)
            double dx = 0, dy = 0;
            if (computeCtx.X < capLogoX) dx = capLogoX - computeCtx.X;
            else if (computeCtx.X >= capLogoX + LogoCols) dx = computeCtx.X - (capLogoX + LogoCols - 1);
            if (computeCtx.Y < capLogoY) dy = capLogoY - computeCtx.Y;
            else if (computeCtx.Y >= capLogoY + LogoHeightCells) dy = computeCtx.Y - (capLogoY + LogoHeightCells - 1);

            double dist = Math.Sqrt(dx * dx + dy * dy);

            // Inside or very close to logo: fully black
            // Far from logo: full noise brightness
            // Fade zone: ~3 cells (black) to ~20 cells (full brightness)
            const double innerRadius = 3.0;
            const double outerRadius = 20.0;

            double brightness;
            if (dist <= innerRadius)
                brightness = 0.0;
            else if (dist >= outerRadius)
                brightness = 1.0;
            else
            {
                double t = (dist - innerRadius) / (outerRadius - innerRadius);
                brightness = t * t; // ease-in: gradual emergence
            }

            if (brightness < 0.01) return new SurfaceCell(" ", null, Hex1bColor.Black);
            if (brightness > 0.99) return below;

            var newFg = DimColor(below.Foreground, brightness);
            var newBg = DimColor(below.Background, brightness);
            return below with { Foreground = newFg, Background = newBg };
        });

        // --- Layer 3: BUILD logo at center ---
        int capLX = logoX, capLY = logoY;
        yield return ctx.Layer(surface =>
        {
            for (int cellRow = 0; cellRow < LogoHeightCells; cellRow++)
            {
                int sy = capLY + cellRow;
                if (sy < 0 || sy >= surface.Height) continue;

                int topPixelRow = cellRow * 2;
                int botPixelRow = topPixelRow + 1;

                for (int col = 0; col < LogoCols; col++)
                {
                    int sx = capLX + col;
                    if (sx < 0 || sx >= surface.Width) continue;

                    var topColor = topPixelRow < LogoRows ? Pixels[topPixelRow, col] : null;
                    var botColor = botPixelRow < LogoRows ? Pixels[botPixelRow, col] : null;

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
                        surface[sx, sy] = new SurfaceCell(" ", null, bg);
                    }
                    else
                    {
                        surface[sx, sy] = new SurfaceCell(
                            UpperHalf.ToString(), fg, bg);
                    }
                }
            }
        });

        // --- Layer 4: Shimmer wave on the logo ---
        double now = time;
        double shimmerElapsed = now - _shimmerStartTime;

        if (shimmerElapsed > _nextShimmerDelay + 1.5)
        {
            _shimmerStartTime = now;
            _nextShimmerDelay = 5 + _random.NextDouble() * 5;
            shimmerElapsed = 0;
        }

        const double waveDuration = 1.0;
        double waveProgress = shimmerElapsed / waveDuration;

        if (waveProgress >= 0 && waveProgress < 1.5)
        {
            double capturedWave = waveProgress;
            int sCapLX = capLX, sCapLY = capLY;
            yield return ctx.Layer(computeCtx =>
            {
                var below = computeCtx.GetBelow();

                // Only shimmer cells within the logo bounds
                int lx = computeCtx.X - sCapLX;
                int ly = computeCtx.Y - sCapLY;
                if (lx < 0 || lx >= LogoCols || ly < 0 || ly >= LogoHeightCells)
                    return below;

                if (below.Foreground is null && below.Background is null)
                    return below;

                double diag = ((double)lx / LogoCols + (double)ly / LogoHeightCells) / 2.0;
                double dist = Math.Abs(diag - capturedWave);
                const double waveWidth = 0.15;

                if (dist > waveWidth) return below;

                double intensity = 1.0 - (dist / waveWidth);
                intensity *= intensity;
                double boost = 1.0 + intensity * 0.6;

                return below with
                {
                    Foreground = BoostColor(below.Foreground, boost),
                    Background = BoostColor(below.Background, boost)
                };
            });
        }
    }

    // --- Perlin noise helpers ---

    private static (byte r, byte g, byte b) SampleNoiseColor(
        int cellX, int pixY, double centerX, double centerY,
        double cos1, double sin1, double cos2, double sin2,
        double fadePhase, double time)
    {
        // Position relative to center
        double rx = cellX - centerX;
        double ry = pixY - centerY;

        // Rotate for field 1
        double x1 = rx * cos1 - ry * sin1;
        double y1 = rx * sin1 + ry * cos1;

        // Rotate for field 2
        double x2 = rx * cos2 - ry * sin2;
        double y2 = rx * sin2 + ry * cos2;

        // Scale for noise sampling
        const double scale = 0.08;
        double n1 = PerlinNoise(x1 * scale, y1 * scale, _perm1) * 0.7
                   + PerlinNoise(x1 * scale * 2, y1 * scale * 2, _perm1) * 0.3;
        double n2 = PerlinNoise(x2 * scale, y2 * scale, _perm2) * 0.7
                   + PerlinNoise(x2 * scale * 2, y2 * scale * 2, _perm2) * 0.3;

        // Cross-fade
        double n = n1 * (1.0 - fadePhase) + n2 * fadePhase;

        // Map noise (-1..1) to a subtle colored value
        double v = (n + 1.0) * 0.5; // 0-1
        v = Math.Clamp(v, 0, 1);

        // Subtle coloring: dark blues/purples/teals
        byte r = (byte)(v * 30);
        byte g = (byte)(v * 20 + 5);
        byte b = (byte)(v * 45 + 10);

        return (r, g, b);
    }

    private static double PerlinNoise(double x, double y, int[] perm)
    {
        int xi = (int)Math.Floor(x) & 255;
        int yi = (int)Math.Floor(y) & 255;
        double xf = x - Math.Floor(x);
        double yf = y - Math.Floor(y);

        double u = Fade(xf);
        double v = Fade(yf);

        int aa = perm[perm[xi] + yi];
        int ab = perm[perm[xi] + yi + 1];
        int ba = perm[perm[xi + 1] + yi];
        int bb = perm[perm[xi + 1] + yi + 1];

        double x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
        double x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);
        return Lerp(x1, x2, v);
    }

    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    private static double Lerp(double a, double b, double t) => a + t * (b - a);
    private static double Grad(int hash, double x, double y)
    {
        int h = hash & 3;
        double u = h < 2 ? x : y;
        double v = h < 2 ? y : x;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    private static int[] BuildPermutationTable(int seed)
    {
        var rng = new Random(seed);
        var p = new int[256];
        for (int i = 0; i < 256; i++) p[i] = i;
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }
        var perm = new int[512];
        for (int i = 0; i < 512; i++) perm[i] = p[i & 255];
        return perm;
    }

    // --- Color helpers ---

    private static Hex1bColor? BoostColor(Hex1bColor? color, double boost)
    {
        if (color is null) return null;
        var c = color.Value;
        return Hex1bColor.FromRgb(
            (byte)Math.Min(255, (int)(c.R * boost)),
            (byte)Math.Min(255, (int)(c.G * boost)),
            (byte)Math.Min(255, (int)(c.B * boost)));
    }

    private static Hex1bColor? DimColor(Hex1bColor? color, double brightness)
    {
        if (color is null) return null;
        var c = color.Value;
        return Hex1bColor.FromRgb(
            (byte)(c.R * brightness),
            (byte)(c.G * brightness),
            (byte)(c.B * brightness));
    }
}
