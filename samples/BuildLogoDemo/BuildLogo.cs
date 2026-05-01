using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace BuildLogoDemo;

internal static class BuildLogo
{
    private const char UpperHalf = '▀';
    private const int Cols = 48;
    private const int Rows = 14;

    // Each terminal cell shows 2 vertical pixels via half-block chars
    public const int WidthCells = Cols;
    public const int HeightCells = (Rows + 1) / 2; // 7 rows

    // Color data extracted from buildlogo.jpg — 48x14 grid of RGB block colors
    // null = dark background, (R,G,B) = block color
    private static readonly (byte R, byte G, byte B)?[,] Pixels = new (byte, byte, byte)?[14, 48]
    {
        { (31,151,210), (32,155,210), (30,154,215), (60,137,190), (93,118,159), (122,105,129), (157,85,100), (182,70,74), (184,60,49), null, null, (63,134,170), (22,157,213), (31,154,208), (28,157,211), null, null, null, (255,25,1), (255,23,0), (215,29,13), null, (187,135,37), (255,191,11), (255,185,24), null, null, (23,158,203), (25,155,215), (40,124,165), null, null, null, null, null, null, null, (255,25,0), (255,26,6), (255,27,1), (243,53,7), (216,80,12), (195,106,15), (176,128,26), (161,188,30), (123,202,24), null, null },
        { (43,151,195), null, null, null, null, null, null, null, (234,60,16), (199,29,15), null, (236,169,47), null, null, (255,193,18), null, null, null, (114,182,42), null, (24,98,134), (24,92,127), (145,191,51), null, null, (24,156,219), null, (111,197,39), null, null, (35,143,185), null, null, null, null, null, null, (255,188,24), null, null, null, null, null, null, (242,29,16), (135,192,36), null, null },
        { (255,30,4), null, null, null, null, null, null, null, null, (163,41,9), null, (229,177,38), null, null, (255,190,21), null, null, null, (128,165,38), null, (22,100,132), (20,93,130), (143,193,52), null, null, (21,159,216), null, (110,196,40), null, null, (54,143,171), null, null, null, null, null, null, (255,187,23), null, null, null, null, null, null, null, null, (118,192,32), null },
        { (255,66,4), null, null, (103,202,43), (217,20,9), (217,20,8), (222,19,0), (248,60,6), null, null, (88,164,41), (230,175,42), null, null, (255,152,22), null, null, null, (144,145,32), null, (27,98,129), (26,91,125), (145,192,50), null, null, (32,159,213), null, (110,196,40), null, null, (106,176,46), null, null, null, null, null, null, (255,187,23), null, null, (128,165,43), (114,190,49), (126,189,53), (166,187,26), null, null, null, (86,185,86) },
        { (255,102,9), null, null, (90,194,66), null, null, null, (255,188,28), null, null, (97,166,33), (217,170,55), null, null, (255,24,5), null, null, null, (160,120,29), null, null, (50,92,109), (147,182,48), null, null, (67,162,183), null, (106,192,50), null, null, (93,175,61), null, null, null, null, null, null, (255,180,20), null, null, (156,80,87), null, null, (160,120,44), (255,107,5), null, null, (30,155,207) },
        { (255,145,15), null, null, (149,198,26), (187,135,19), (189,135,18), (184,136,25), null, null, null, (102,165,38), (190,169,68), null, null, (255,24,5), null, null, null, (183,97,22), null, null, null, (160,162,39), null, null, (106,168,149), null, (93,188,74), null, null, (82,167,93), null, null, null, null, null, null, (255,158,16), null, null, (122,101,118), null, null, null, (253,188,20), null, null, (44,159,193) },
        { (255,182,18), null, null, null, null, null, null, null, null, (80,139,32), null, (165,161,93), null, null, (255,26,2), null, null, null, (198,75,18), null, null, null, (175,138,39), null, null, (140,175,122), null, (85,180,98), null, null, (73,163,110), null, null, null, null, null, null, (255,133,11), null, null, (100,113,143), null, null, null, (232,180,45), null, null, (81,163,168) },
        { (255,187,17), null, null, null, null, null, null, null, null, (87,145,30), null, (144,157,106), null, null, (255,27,1), null, null, null, (216,51,14), null, null, null, (152,94,24), null, null, (173,178,93), null, (75,174,119), null, null, (64,157,129), null, null, null, null, null, null, (255,110,9), null, null, (73,127,166), null, null, null, (201,177,67), null, null, (112,168,144) },
        { (255,187,17), null, null, (115,199,37), (255,23,4), (255,26,3), (255,26,2), (251,30,4), null, null, (105,155,51), (119,147,130), null, null, (255,27,1), null, null, null, (235,30,8), null, (133,113,42), (122,109,43), (155,67,14), null, null, (214,181,58), null, (64,168,144), null, null, (55,150,156), null, null, null, null, null, null, (255,89,6), null, null, (43,144,195), null, null, null, (170,172,93), null, null, (146,173,111) },
        { (255,192,17), null, null, (88,166,165), null, null, null, (255,187,27), null, null, (99,164,38), (94,149,145), null, null, (255,41,16), null, null, (255,181,9), (241,26,7), null, (148,116,29), (143,111,29), (174,51,15), null, null, (249,180,33), null, (50,165,165), null, null, (37,140,190), null, null, null, null, null, null, (255,64,10), null, null, (41,148,186), null, null, null, (146,164,118), null, null, (181,175,79) },
        { (255,192,17), null, null, (58,160,195), null, null, null, (255,178,37), null, null, (93,162,48), (92,126,136), null, null, (239,40,0), null, null, (255,176,24), (223,33,16), null, (164,118,19), (159,110,23), (184,32,15), null, null, (255,187,21), null, (38,162,186), null, null, (42,141,177), null, null, null, null, null, null, (255,45,7), null, null, (50,142,179), null, null, (227,157,23), (120,110,110), null, null, (212,181,52) },
        { (255,192,17), null, null, (25,129,166), (209,149,71), (239,151,39), (255,148,25), (180,45,15), null, null, (75,160,73), (169,19,10), null, null, (124,147,42), (255,165,12), (255,161,9), (210,30,11), null, null, (255,188,13), (161,109,24), (194,19,9), null, null, (255,188,15), null, (106,116,134), null, null, (102,171,32), (96,174,33), (108,162,33), (122,140,29), (101,97,108), (123,104,126), null, (223,41,32), null, null, (150,170,11), (255,163,8), (255,164,13), (232,45,6), null, null, null, (175,134,46) },
        { (255,190,14), null, null, null, (165,65,62), (199,43,43), (189,30,19), null, (93,124,22), (118,111,43), null, (150,28,28), (64,78,77), null, null, (174,22,12), (178,19,12), null, null, null, (243,170,20), null, (198,18,5), null, null, (255,190,9), null, (254,29,0), null, null, null, null, null, null, null, (25,155,219), null, (39,148,215), null, null, null, (184,31,12), (189,20,9), null, null, (172,121,31), (255,190,13), null },
        { (255,180,15), (223,33,15), (178,55,58), (156,72,80), (121,96,105), (94,112,134), (59,131,167), (34,142,187), (67,164,127), null, null, null, null, (103,99,110), (120,94,108), (158,69,83), (180,59,56), (205,44,28), (181,50,38), (59,113,126), null, null, (192,21,9), (201,22,0), (119,178,43), (112,171,136), null, (255,98,6), (234,119,27), (239,95,22), (236,75,18), (233,54,9), (233,29,8), (235,27,9), (235,27,12), null, null, (164,77,95), (188,53,51), (149,73,85), (123,94,105), (102,108,124), (76,119,158), (42,139,180), (32,142,197), (255,193,25), (156,105,26), null },
    };

    public static IEnumerable<SurfaceLayer> BuildLayers(SurfaceLayerContext ctx)
    {
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
                    {
                        // Both dark — leave as default (transparent/black)
                        continue;
                    }

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
                        // Same color — use space with background
                        surface[col, cellRow] = new SurfaceCell(" ", null, bg);
                    }
                    else
                    {
                        // Different colors — upper half block: fg=top, bg=bottom
                        surface[col, cellRow] = new SurfaceCell(
                            UpperHalf.ToString(), fg, bg);
                    }
                }
            }
        });
    }
}
