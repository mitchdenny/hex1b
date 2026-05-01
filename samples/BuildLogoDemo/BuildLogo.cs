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
        { (46,152,198), (46,152,198), (46,152,198), (71,138,177), (94,123,154), (115,111,134), (138,98,113), (159,84,92), (181,71,71), (176,57,55), null, null, (64,135,166), (46,152,198), (46,152,198), (46,152,198), (58,146,184), null, null, (174,44,34), (235,37,20), (235,37,20), (214,45,30), null, (136,106,38), (240,181,42), (240,181,42), (232,175,40), null, null, (46,152,198), (46,152,198), (48,145,187), null, null, null, null, null, null, null, null, (235,37,20), (235,37,20), (235,37,20), (219,56,23), (203,74,24), (188,93,29), (172,112,32), (156,130,34), (141,184,44), (122,183,44), null, null },
        { (46,152,198), null, null, null, null, null, null, null, (196,76,34), (220,62,24), (204,52,26), null, (209,170,78), (219,174,67), null, (185,146,57), (227,179,63), null, null, (100,150,61), (111,167,60), null, (70,145,177), (62,145,181), (86,105,36), (140,180,50), null, (49,101,123), (49,146,189), null, (110,184,47), null, null, (45,149,194), null, null, null, null, null, null, null, (240,181,42), null, null, null, null, null, null, null, (235,37,20), (122,183,44), null, null },
        { (229,41,26), null, null, null, null, null, null, null, null, (175,62,41), (193,52,26), null, (209,170,78), (219,174,67), null, (184,146,57), (227,179,64), null, null, (107,138,57), (120,154,56), null, (70,145,177), (62,145,181), (86,105,36), (140,180,50), null, (50,101,123), (49,146,189), null, (110,184,47), null, null, (45,149,194), null, null, null, null, null, null, null, (240,181,41), null, null, null, null, null, null, null, null, null, (106,179,43), null },
        { (236,58,22), null, null, (99,160,47), (195,54,23), (211,34,19), (211,34,19), (211,35,19), (188,42,29), null, (114,157,71), (97,146,67), (208,170,78), (219,174,67), null, (184,146,57), (227,179,64), null, null, (117,126,55), (131,140,54), null, (70,145,177), (62,145,181), (86,105,36), (140,180,50), null, (50,101,123), (49,146,189), null, (110,184,47), null, null, (105,180,52), null, null, null, null, null, null, null, (240,181,42), null, null, (111,180,51), (111,180,51), (111,180,51), (127,180,51), (143,179,51), null, null, null, (101,168,44) },
        { (238,87,27), null, null, (96,167,79), (152,94,58), (180,39,29), (180,39,29), (209,115,45), (217,143,55), null, (113,169,66), (101,151,59), (208,170,78), (219,174,67), null, (172,63,35), (214,73,39), null, null, (126,113,51), (141,125,50), null, (86,148,168), (79,148,170), (86,105,36), (140,179,50), null, (60,103,117), (67,149,176), null, (110,184,47), null, null, (102,178,61), null, null, null, null, null, null, null, (240,180,41), null, null, (162,96,71), (106,124,69), (109,149,73), (119,150,76), (224,181,42), (223,41,25), null, null, (56,157,172) },
        { (239,115,31), null, null, (98,169,76), null, null, null, (211,166,61), (222,175,63), null, (114,169,64), (102,152,58), (199,167,85), (208,171,76), null, (168,41,29), (209,46,31), null, null, (135,101,48), (151,111,47), null, (105,153,157), (98,152,156), (90,100,35), (147,169,48), null, (73,106,110), (89,153,158), null, (104,180,62), null, null, (96,174,79), null, null, null, null, null, null, null, (241,170,37), null, null, (155,87,96), null, null, null, (197,146,34), (239,147,37), null, null, (46,152,198) },
        { (240,141,35), null, null, (139,173,56), (202,168,51), (213,164,49), (213,164,49), (208,164,59), (208,169,75), null, (114,169,64), (103,152,58), (182,163,95), (190,167,89), null, (169,42,30), (209,46,31), null, null, (143,88,45), (161,97,44), null, (123,157,146), (118,157,144), (94,91,33), (155,154,45), null, (86,109,103), (110,157,143), null, (97,176,80), null, null, (88,170,97), null, null, null, null, null, null, null, (240,154,36), null, null, (133,101,117), null, null, null, null, (240,181,42), null, null, (62,154,186) },
        { (241,168,38), null, null, (132,144,67), (183,154,69), (190,155,69), (190,155,69), (177,148,75), null, (108,158,61), (105,156,58), (95,126,68), (165,159,106), (171,163,101), null, (170,43,31), (209,46,31), null, null, (152,75,42), (171,83,40), null, (141,162,136), (137,161,131), (111,90,29), (166,137,42), null, (98,113,95), (132,160,126), null, (90,173,97), null, null, (81,166,114), null, null, null, null, null, null, null, (239,137,34), null, null, (110,115,139), null, null, null, null, (226,178,54), null, null, (85,157,167) },
        { (241,179,41), null, null, null, null, null, null, null, null, (111,159,61), (104,161,48), null, (148,155,116), (152,160,114), null, (170,43,31), (209,46,31), null, null, (160,64,39), (181,69,37), null, (159,166,125), (156,165,118), (129,86,25), (175,121,39), null, (111,116,88), (153,164,109), null, (83,170,114), null, null, (73,163,131), null, null, null, null, null, null, null, (239,121,31), null, null, (88,127,160), null, null, null, null, (205,175,72), null, null, (108,161,149) },
        { (240,181,42), null, null, (104,157,58), (188,60,32), (202,42,28), (202,42,28), (202,42,28), (177,49,37), (106,146,59), (110,161,59), (106,143,72), (131,151,127), (133,156,128), null, (171,43,31), (209,46,31), null, null, (169,52,36), (191,54,34), null, (177,170,114), (177,168,105), (149,79,24), (186,104,36), null, (124,119,80), (175,167,92), null, (76,166,132), null, null, (66,159,149), null, null, null, null, null, null, null, (238,103,28), null, null, (64,143,183), null, null, null, null, (182,172,90), null, null, (131,165,131) },
        { (240,181,42), null, null, (104,167,80), (193,61,33), (212,33,19), (212,33,19), (222,59,26), (209,80,39), null, (114,169,64), (102,151,57), (114,147,136), (114,152,140), null, (172,48,33), (210,50,32), null, null, (175,78,36), (197,45,32), null, (195,174,104), (195,173,93), (163,69,21), (196,89,33), null, (135,122,73), (197,170,77), null, (69,163,149), null, null, (58,155,171), null, null, null, null, null, null, null, (237,87,26), null, null, (46,152,199), null, null, null, null, (161,169,107), null, null, (154,168,112) },
        { (240,181,42), null, null, (94,152,154), null, null, null, (211,166,61), (222,175,63), null, (114,169,64), (102,151,58), (96,142,146), (94,148,152), null, (176,57,37), (211,57,33), null, (165,130,48), (239,137,36), (197,45,32), null, (211,179,92), (214,177,80), (159,53,20), (205,72,29), null, (146,125,66), (217,174,61), null, (61,159,167), null, null, (46,150,195), null, null, null, null, null, null, null, (237,70,24), null, null, (46,152,199), null, null, null, (192,149,55), (137,152,124), null, null, (178,171,92) },
        { (240,181,42), null, null, (71,148,171), (109,127,106), null, null, (215,156,53), (217,156,58), null, (111,168,70), (99,150,63), (110,114,122), (113,118,126), null, (165,60,38), (202,65,32), null, (178,126,42), (232,123,36), (179,48,36), null, (220,179,81), (224,179,71), null, (216,55,27), null, (154,128,61), (230,177,50), null, (54,155,185), null, null, (57,155,167), null, null, null, null, null, null, null, (235,53,21), null, null, (62,157,177), (141,116,57), null, null, (239,158,39), (111,104,126), null, null, (202,175,74) },
        { (240,181,42), null, null, (46,138,178), (166,165,98), (205,170,68), (227,173,51), (225,118,32), (186,59,32), null, (101,162,91), (90,145,80), (181,50,38), (199,47,33), null, (104,129,50), (168,171,44), (241,181,44), (239,136,37), (220,40,24), null, (170,137,58), (240,181,42), (225,180,70), null, (224,40,24), null, (154,127,61), (230,177,50), null, (73,136,173), null, null, (111,186,48), (111,186,48), (113,184,48), (128,165,44), (144,148,42), (121,110,131), (142,94,107), null, (234,37,21), null, null, (147,184,47), (241,182,44), (241,181,44), (241,182,44), (235,38,21), null, null, null, (204,165,61) },
        { (240,180,41), null, null, null, (173,72,71), (202,59,52), (224,46,33), (209,44,29), null, null, (88,149,111), (74,134,105), (176,48,37), (187,48,35), null, null, (208,44,30), (235,37,20), (228,37,20), null, null, (170,137,58), (237,179,42), (218,174,68), null, (224,41,24), null, (154,127,61), (230,177,49), null, (235,38,21), null, null, null, null, null, null, null, null, (45,151,197), null, (46,152,198), null, null, null, (235,57,22), (235,37,20), (235,37,20), null, null, null, (240,181,42), null },
        { (240,181,42), null, null, null, null, null, null, null, (113,172,62), (173,183,44), (211,163,49), null, null, (62,146,149), (70,144,147), null, null, null, null, null, null, (164,135,66), (230,180,61), null, null, (220,41,26), null, (149,127,73), (230,177,49), null, (232,40,23), null, null, null, null, null, null, null, null, (45,151,197), null, (46,152,198), null, null, null, null, null, null, null, null, (240,181,42), (240,181,42), null },
        { (241,169,39), (235,37,20), (190,65,62), (164,81,87), (140,97,111), (115,111,134), (89,126,159), (63,142,183), (46,152,198), (42,136,181), null, null, null, null, (115,109,130), (124,104,126), (148,90,103), (171,76,80), (195,62,56), (219,47,34), (178,71,73), (48,154,200), null, null, null, (235,37,20), (214,60,24), (98,178,72), (49,146,189), null, (240,146,36), (239,127,32), (239,108,29), (237,90,26), (237,73,25), (235,55,21), (235,37,20), (235,37,20), (230,36,20), null, null, (216,49,37), (191,64,61), (167,79,84), (144,94,107), (119,109,130), (95,122,152), (71,138,177), (46,152,198), (46,152,198), (240,181,42), null, null }
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
