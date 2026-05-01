using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace BuildLogoDemo;

internal static class BuildLogo
{
    private const char UpperHalf = '\u2580';
    private const int Cols = 53;
    private const int Rows = 17;

    // Each terminal cell shows 2 vertical pixels via half-block chars
    public const int WidthCells = Cols;
    public const int HeightCells = (Rows + 1) / 2; // 9 rows

    // Color data extracted from Build 2026 hero banner — 53x17 grid of RGB block colors
    // null = transparent (dark or white background)
    private static readonly (byte R, byte G, byte B)?[,] Pixels = new (byte, byte, byte)?[17, 53]
    {
        { (46,152,198), (46,152,198), (46,152,198), (71,138,177), (95,122,152), (119,109,130), (144,94,108), (165,80,87), (183,69,68), (171,56,54), null, null, (78,129,152), (46,152,198), (46,152,198), (46,152,198), (53,139,176), null, null, null, (235,37,20), (235,37,20), (226,36,19), null, (52,49,44), (240,181,42), (240,181,42), (240,181,42), null, null, (46,152,198), (46,152,198), (46,152,198), null, null, null, null, null, null, null, null, (235,37,20), (235,37,20), (235,37,20), (219,56,23), (203,74,24), (188,93,29), (172,112,32), (156,130,34), (141,184,44), (122,183,44), null, null },
        { (46,152,198), null, null, null, null, null, null, null, (184,71,31), (221,62,23), (179,61,39), null, (196,169,102), (211,175,88), null, (145,119,65), (216,169,57), null, null, null, (104,172,43), null, null, (47,149,194), (46,44,40), (140,183,44), null, null, (46,152,198), null, (109,184,45), null, null, (46,152,198), null, null, null, null, null, null, null, (240,181,42), null, null, null, null, null, null, null, (235,37,20), (122,183,44), null, null },
        { (235,37,20), null, null, null, null, null, null, null, null, (155,57,41), (168,60,38), null, (196,169,102), (211,175,88), null, (145,119,65), (216,169,57), null, null, null, (117,158,40), null, null, (47,149,194), (46,44,40), (140,183,44), null, null, (46,152,198), null, (109,184,45), null, null, (46,152,198), null, null, null, null, null, null, null, (240,181,42), null, null, null, null, null, null, null, null, null, (109,184,45), null },
        { (236,58,22), null, null, (109,184,45), (235,37,20), (235,37,20), (235,37,20), (235,37,20), (181,55,44), null, (101,151,74), (98,141,72), (196,170,102), (211,176,88), null, (145,119,65), (216,169,57), null, null, null, (132,141,38), null, null, (47,149,194), (46,44,40), (140,183,44), null, null, (46,152,198), null, (109,184,45), null, null, (109,184,45), null, null, null, null, null, null, null, (240,181,42), null, null, (109,184,45), (109,184,45), (109,184,45), (126,183,45), (144,182,44), null, null, null, (109,184,45) },
        { (237,88,27), null, null, (92,174,87), null, null, null, (194,159,83), (208,171,84), null, (106,151,66), (103,142,66), (196,169,102), (211,175,88), null, (127,43,36), (203,43,29), null, null, null, (148,122,33), null, null, (70,153,176), (46,44,40), (139,183,43), null, null, (70,155,179), null, (109,184,45), null, null, (102,180,65), null, null, null, null, null, null, null, (240,181,42), null, null, (176,74,77), null, null, null, (240,181,42), (235,37,20), null, null, (46,152,198) },
        { (239,118,31), null, null, (100,180,69), null, null, null, (198,165,88), (212,175,87), null, (106,152,66), (103,143,66), (187,166,108), (200,171,97), null, (125,41,34), (202,42,28), null, null, null, (162,105,31), null, null, (94,157,156), (45,44,39), (149,168,41), null, null, (95,159,159), null, (103,180,61), null, null, (95,175,85), null, null, null, null, null, null, null, (241,166,36), null, null, (154,87,96), null, null, null, null, (240,181,42), null, null, (46,152,198) },
        { (240,147,35), null, null, (162,183,45), (240,182,45), (239,181,45), (239,181,44), (214,170,62), null, null, (106,152,66), (103,143,67), (172,161,115), (183,168,107), null, (127,43,36), (202,42,28), null, null, null, (176,89,29), null, null, (118,160,139), (43,41,37), (160,150,38), null, null, (118,163,142), null, (95,176,80), null, null, (86,172,103), null, null, null, null, null, null, null, (240,149,36), null, null, (133,100,117), null, null, null, null, (240,181,42), null, null, (68,155,182) },
        { (241,176,39), null, null, (131,140,87), (166,146,95), (166,146,94), (166,146,94), (149,134,94), null, (99,137,64), (104,149,63), (87,103,74), (159,157,120), (166,163,115), null, (129,44,37), (202,42,28), null, null, null, (187,75,25), null, null, (137,163,124), null, (171,136,36), null, null, (138,166,126), null, (87,172,100), null, null, (81,168,118), null, null, null, null, null, null, null, (239,135,33), null, null, (111,114,138), null, null, null, null, (222,178,57), null, null, (88,158,165) },
        { (240,180,41), null, null, null, null, null, null, null, null, (104,139,64), (109,152,65), null, (146,152,125), (151,159,124), null, (129,44,37), (202,42,28), null, null, null, (199,61,23), null, null, (157,166,107), null, (180,121,33), null, null, (159,168,108), null, (80,169,119), null, null, (73,165,134), null, null, null, null, null, null, null, (239,120,31), null, null, (91,125,157), null, null, null, null, (203,175,74), null, null, (110,161,148) },
        { (240,181,42), null, null, (114,164,69), (185,50,37), (185,50,37), (185,50,37), (185,50,37), (146,59,51), (97,123,59), (107,152,60), (96,120,74), (134,148,130), (137,155,132), null, (129,44,37), (202,42,28), null, null, null, (211,47,21), null, null, (177,169,92), null, (191,105,31), null, null, (179,171,92), null, (75,165,134), null, null, (67,161,150), null, null, null, null, null, null, null, (238,104,28), null, null, (69,139,179), null, null, null, null, (183,172,89), null, null, (130,165,132) },
        { (240,181,42), null, null, (104,179,59), (217,34,19), (217,34,19), (217,34,19), (226,43,21), (183,67,48), null, (106,151,65), (102,143,66), (121,143,134), (122,152,140), null, (130,46,38), (202,44,29), null, null, (113,73,51), (221,35,20), null, null, (196,172,78), null, (201,91,29), null, null, (199,174,77), null, (68,163,148), null, null, (60,158,167), null, null, null, null, null, null, null, (237,90,26), null, null, (46,152,198), null, null, null, null, (165,169,104), null, null, (151,168,114) },
        { (240,181,42), null, null, (101,160,157), null, null, null, (198,165,88), (212,175,87), null, (106,152,65), (103,143,66), (107,138,139), (106,147,147), null, (136,57,44), (203,54,30), null, (104,91,62), (240,167,41), (221,35,20), null, null, (218,175,60), null, (211,75,26), null, null, (222,177,58), null, (61,160,165), null, null, (46,152,198), null, null, null, null, null, null, null, (237,75,25), null, null, (46,152,198), null, null, null, null, (144,165,122), null, null, (173,169,96) },
        { (240,181,42), null, null, (75,156,176), (42,41,39), null, null, (192,159,81), (212,175,87), null, (105,151,68), (101,142,68), (97,128,138), (96,137,149), null, (134,50,42), (202,42,28), null, (104,91,62), (240,167,41), (221,36,20), null, null, (235,179,44), null, (224,57,22), null, null, (240,181,42), null, (55,156,181), null, null, (46,152,199), null, null, null, null, null, null, null, (235,57,21), null, null, (46,152,198), null, null, null, (240,181,42), (116,109,134), null, null, (197,174,79) },
        { (240,181,42), null, null, (46,152,198), (188,174,83), (213,177,65), (232,180,49), (230,125,34), (172,70,47), null, (94,145,87), (92,137,83), (155,59,51), (179,56,44), null, (92,109,58), (151,169,41), (240,181,42), (238,164,39), (221,34,18), null, (92,84,63), (240,181,42), (236,179,44), null, (235,37,20), null, null, (240,181,42), null, (46,152,199), null, null, (109,184,45), (109,184,45), (109,184,45), (124,165,42), (142,146,38), (119,109,130), (144,94,107), null, (235,37,20), null, null, (144,182,44), (240,181,42), (240,181,42), (240,181,42), (235,37,20), null, null, null, (222,177,58) },
        { (240,181,42), null, null, null, (184,68,66), (208,54,46), (227,43,28), (197,49,35), null, null, (85,141,105), (83,132,100), (155,59,51), (179,56,44), null, null, null, (235,37,20), (229,37,21), null, null, (91,84,63), (240,181,42), (236,179,44), null, (235,37,20), null, null, (240,181,42), null, (235,37,20), null, null, null, null, null, null, null, null, (46,152,198), null, (46,152,198), null, null, null, (235,57,21), (235,37,20), (235,37,20), null, null, null, (240,181,42), null },
        { (240,181,42), null, null, null, null, null, null, null, (105,166,50), (176,182,45), (196,161,72), null, null, (74,142,145), (72,141,142), null, null, null, null, null, null, (90,82,63), (232,176,42), null, null, (235,37,20), null, null, (240,181,42), null, (235,37,20), null, null, null, null, null, null, null, null, (46,152,198), null, (46,152,198), null, null, null, null, null, null, null, null, (240,181,42), (240,181,42), null },
        { (241,169,39), (235,37,20), (191,64,61), (167,79,84), (144,94,107), (119,109,130), (95,122,153), (69,139,179), (46,152,198), (40,131,175), null, null, null, null, null, (124,105,126), (145,92,106), (167,78,83), (190,65,61), (214,49,38), (220,45,33), (44,147,192), null, null, null, (235,37,20), (236,37,20), (109,184,45), (46,152,198), null, (240,149,36), (239,130,32), (239,111,29), (237,92,27), (237,75,25), (235,57,21), (235,37,20), (235,37,20), (235,37,20), null, null, (216,49,37), (191,64,61), (167,79,84), (144,94,107), (119,109,130), (95,122,152), (71,138,177), (46,152,198), (46,152,198), (240,181,42), null, null }
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
    }
}
