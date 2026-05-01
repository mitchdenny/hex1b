using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace BuildLogoDemo;

internal static class BuildLogo
{
    private const char UpperHalf = '▀';
    private const int Cols = 56;
    private const int Rows = 17;
    private static readonly (byte R, byte G, byte B)?[,] Pixels = new (byte, byte, byte)?[17, 56]
    {
        { (46,152,198), (46,152,198), (46,152,198), (71,138,177), (95,122,152), (104,117,145), (119,109,130), (144,94,107), (167,79,84), (191,64,61), null, null, null, (46,152,198), (46,152,198), (46,152,198), (46,152,198), (94,118,128), null, null, null, (235,37,20), (235,37,20), (235,37,20), null, null, (240,181,42), (240,181,42), (240,181,42), null, null, (46,152,198), (46,152,198), (46,152,198), (43,153,202), null, null, null, null, null, null, null, null, (235,37,20), (235,37,20), (235,37,20), (219,56,23), (202,73,23), (189,92,29), (181,96,29), (172,112,32), (156,130,34), (141,184,44), (122,183,44), null, null },
        { (46,152,198), null, null, null, null, null, null, null, null, (206,72,26), (236,52,21), null, null, (240,181,42), null, null, (240,181,42), (173,160,128), null, null, null, (109,184,45), null, null, (46,152,198), null, (139,183,43), null, null, (46,152,198), null, (109,184,45), null, null, (119,119,119), (46,152,198), null, null, null, null, null, null, null, (240,181,42), null, null, null, null, null, null, null, null, (235,37,20), (122,183,44), null, null },
        { (235,37,20), null, null, null, null, null, null, null, null, null, (223,53,21), null, null, (240,181,42), null, null, (240,181,42), (173,160,128), null, null, null, (122,168,41), null, null, (46,152,198), null, (139,183,43), null, null, (46,152,198), null, (109,184,45), null, null, (119,119,119), (46,152,198), null, null, null, null, null, null, null, (240,181,42), null, null, null, null, null, null, null, null, null, null, (109,184,45), null },
        { (236,58,22), null, null, (108,183,44), (225,45,19), (235,37,20), (235,37,20), (235,37,20), (235,37,20), null, null, (106,181,58), null, (240,181,42), null, null, (240,181,42), (173,160,128), null, null, null, (139,150,39), null, null, (46,152,198), null, (139,183,43), null, null, (46,152,198), null, (109,184,45), null, null, (137,137,137), (109,184,45), null, null, null, null, null, null, null, (240,181,42), null, null, (119,181,64), (109,184,45), (109,184,45), (109,184,45), (126,183,45), (144,182,44), null, null, null, (109,184,45) },
        { (237,88,27), null, null, (92,174,87), (125,138,125), null, null, null, (240,181,42), null, null, (112,182,45), null, (240,181,42), null, null, (236,36,20), (116,71,64), null, null, null, (156,130,34), null, null, (70,155,179), null, (139,183,43), null, null, (70,155,179), null, (109,184,45), null, null, (135,135,135), (102,180,65), null, null, null, null, null, null, null, (240,181,42), null, null, (162,91,95), (169,82,86), null, null, (172,172,172), (240,181,42), (235,37,20), null, null, (46,152,198) },
        { (239,118,31), null, null, (100,180,69), (130,142,122), null, null, null, (240,181,42), null, null, (112,182,45), null, (222,177,58), null, null, (236,36,20), (116,71,64), null, null, null, (172,112,32), null, null, (95,159,158), null, (149,168,41), null, null, (95,159,158), null, (103,180,61), null, null, (134,134,134), (95,175,84), null, null, null, null, null, null, null, (241,166,36), null, null, (143,105,118), (144,94,107), null, null, null, null, (240,181,42), null, null, (46,152,198) },
        { (240,147,35), null, null, (162,183,44), (237,182,44), (240,181,42), (240,181,42), (240,181,42), null, null, null, (112,182,45), null, (197,174,79), null, null, (236,36,20), (116,71,64), null, null, null, (188,93,29), null, null, (121,163,140), null, (160,149,37), null, null, (121,163,140), null, (95,176,80), null, null, (132,132,132), (86,172,104), null, null, null, null, null, null, null, (240,149,36), null, null, (125,115,134), (121,111,133), null, null, null, null, (240,181,42), null, null, (70,155,179) },
        { (241,177,39), null, null, (123,125,125), (129,129,131), (135,137,136), (135,137,136), (133,136,135), null, null, (112,182,45), (116,116,116), null, (171,169,97), null, null, (236,36,20), (116,71,64), null, null, null, (203,74,24), null, null, (145,167,119), null, (174,131,34), null, null, (145,166,121), null, (87,172,100), null, null, (128,128,128), (78,167,123), null, null, null, null, null, null, null, (239,130,32), null, null, (104,128,152), (99,126,155), null, null, null, null, (217,177,61), null, null, (95,159,158) },
        { (240,181,42), null, null, null, null, null, null, null, null, null, (112,182,45), null, null, (145,167,119), null, null, (236,36,20), (116,71,64), null, null, null, (219,56,23), null, null, (171,169,97), null, (185,112,32), null, null, (171,169,97), null, (79,169,120), null, null, (127,127,129), (69,163,143), null, null, null, null, null, null, null, (239,111,29), null, null, (87,137,168), (79,136,174), null, null, null, null, (193,173,82), null, null, (121,163,140) },
        { (240,181,42), null, null, (110,183,45), (224,43,20), (235,38,18), (235,38,18), (235,38,18), (235,38,18), null, (4,38,0), (112,185,42), null, (122,162,140), null, null, (236,36,20), (116,71,64), null, null, null, (235,37,20), null, null, (197,173,81), null, (199,93,29), null, null, (196,175,79), null, (72,164,140), null, null, (123,125,127), (62,159,163), null, null, null, null, null, null, null, (237,92,27), null, null, (79,141,179), (68,144,184), null, null, null, null, (168,170,101), null, null, (145,167,119) },
        { (240,181,42), null, null, (80,153,135), (77,4,1), (86,0,0), (86,0,0), (86,0,0), (255,166,55), null, null, (112,182,45), null, (97,159,156), null, null, (243,59,30), (124,80,72), null, (79,79,79), (245,194,75), (235,37,20), null, null, (218,180,65), null, (207,76,25), null, null, (217,178,66), null, (63,161,160), null, null, (119,118,121), (49,152,190), null, null, null, null, null, null, null, (236,76,23), null, null, (63,153,191), (50,154,201), null, null, null, null, (146,165,117), null, null, (170,170,99) },
        { (240,181,42), null, null, (96,159,163), (124,135,136), null, null, null, (240,181,42), null, null, (112,182,45), null, (85,157,164), null, null, (239,53,22), (122,75,70), null, (78,78,78), (240,181,42), (235,37,20), null, null, (221,177,55), null, (215,72,27), null, null, (221,176,57), null, (53,156,181), null, null, (119,119,119), (46,152,198), null, null, null, null, null, null, null, (239,71,26), null, null, (63,153,191), (50,154,201), null, null, (141,141,141), (73,44,0), (144,152,127), null, null, (184,174,91) },
        { (241,182,43), null, null, (76,158,183), (117,126,116), null, null, null, (240,181,40), null, null, (110,182,49), null, (186,80,61), null, null, (236,36,20), (115,70,62), null, (78,78,78), (239,182,40), (235,38,18), null, null, (240,181,40), null, (222,57,22), null, null, (240,181,40), null, (53,155,189), null, null, (119,119,119), (46,152,200), null, null, null, null, null, null, null, (235,57,19), null, null, (63,153,191), (50,154,201), null, null, (172,172,172), (240,181,40), (116,109,134), null, null, (194,174,76) },
        { (240,181,42), null, null, (46,152,198), (182,175,93), (188,174,83), (213,177,64), (239,181,44), (219,57,21), null, null, (96,175,80), null, (235,37,20), null, null, (125,166,41), (240,188,48), (240,181,42), (240,181,42), (236,36,22), null, null, (240,181,42), (240,181,42), null, (235,37,20), null, null, (240,181,42), null, (68,145,187), null, null, (126,126,126), (109,184,45), (109,184,45), (109,184,45), (124,165,42), (142,146,40), (121,107,132), (144,94,107), null, (236,36,20), null, null, (159,185,67), (150,179,42), (240,181,42), (240,181,42), (241,182,43), (235,37,20), null, null, (164,156,125), (223,183,67) },
        { (240,181,42), null, null, null, (185,69,69), (184,68,66), (209,53,45), (234,39,21), null, null, null, (85,170,109), null, (235,37,20), null, null, null, (239,45,29), (235,37,20), (235,37,20), null, null, null, (240,181,42), (240,181,42), null, (235,37,20), null, null, (240,181,42), null, (235,37,20), null, null, null, null, null, null, null, null, null, (46,152,198), null, (46,152,198), null, null, null, null, (235,57,19), (235,37,20), (235,38,18), null, null, null, (240,181,42), null },
        { (240,181,42), null, null, null, null, null, null, null, null, (109,184,45), (240,181,42), null, null, null, (62,159,163), (94,94,94), null, null, null, null, null, null, null, (240,181,42), null, null, (235,37,20), null, null, (240,181,42), null, (235,37,20), null, null, null, null, null, null, null, null, null, (46,152,198), null, (46,152,198), null, null, null, null, null, null, null, null, null, (240,181,42), (240,181,42), null },
        { (241,169,39), (235,37,20), (191,64,61), (167,79,84), (144,94,107), (119,109,130), (95,122,152), (71,138,177), (46,152,198), (46,152,198), null, null, null, null, null, (116,109,134), (139,96,111), (162,81,90), (166,81,87), (187,67,64), (211,51,41), (235,37,20), (46,152,198), null, null, null, (235,37,20), (235,37,20), (109,184,45), (46,152,198), null, (240,149,36), (239,130,32), (237,111,31), (243,112,35), (237,92,27), (237,75,25), (235,57,21), (235,37,20), (235,37,20), (235,37,20), null, null, (216,49,37), (191,64,61), (167,79,84), (144,94,107), (119,109,130), (98,121,150), (92,125,157), (71,138,177), (46,152,198), (46,152,198), (240,181,42), null, null }
    };

    public const int WidthCells = Cols;
    public const int HeightCells = (Rows + 1) / 2; // 9 rows

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
