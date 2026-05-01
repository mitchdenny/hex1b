using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace BuildLogoDemo;

internal static class BuildLogo
{
    private const char UpperHalf = '▀';
    private const int Cols = 50;
    private const int Rows = 17;
    private static readonly (byte R, byte G, byte B)?[,] Pixels = new (byte, byte, byte)?[17, 50]
    {
        { (46,152,198), (46,152,198), (46,152,198), (71,138,177), (95,122,152), (119,109,130), (144,94,107), (167,79,84), (191,64,61), null, null, null, (46,152,198), (46,152,198), (46,152,198), (46,152,198), null, null, (120,0,0), (235,37,20), (235,37,20), null, null, (240,181,42), (240,181,42), (240,181,42), null, null, (46,152,198), (46,152,198), (46,152,198), null, null, null, null, null, null, null, null, (235,37,20), (235,37,20), (219,56,23), (202,73,23), (188,93,29), (172,112,32), (156,130,34), (141,184,44), (122,183,44), null, null },
        { (46,152,198), null, null, null, null, null, null, null, (206,72,26), (236,52,21), null, null, (240,181,42), null, null, (240,181,42), null, null, (17,66,0), (115,112,90), null, (46,152,198), null, (139,183,43), null, null, (46,152,198), null, (109,184,45), null, null, (46,152,198), null, null, null, null, null, null, null, (241,184,46), null, null, null, null, null, null, (235,37,20), (122,183,44), null, null },
        { (235,37,20), null, null, null, null, null, null, null, null, (223,53,21), null, null, (240,181,42), null, null, (240,181,42), null, null, (24,57,0), (113,110,88), null, (46,152,198), null, (139,183,43), null, null, (46,152,198), null, (109,184,45), null, null, (46,152,198), null, null, null, null, null, null, null, (241,184,46), null, null, null, null, null, null, null, null, (109,184,45), null },
        { (236,58,22), null, null, (109,184,45), (235,37,20), (235,37,20), (235,37,20), (235,37,20), null, null, (106,181,58), null, (240,181,42), null, null, (240,181,42), null, null, (42,49,0), (112,101,86), null, (46,152,198), null, (139,183,43), null, null, (46,152,198), null, (109,184,45), null, null, (109,184,45), null, null, null, null, null, null, null, (241,184,46), null, null, (109,184,45), (109,184,45), (126,183,45), (144,182,44), null, null, null, (109,184,45) },
        { (237,88,27), null, null, (92,174,87), null, null, null, (240,181,42), null, null, (112,182,45), null, (240,181,42), null, null, (235,37,20), null, null, (55,35,0), (109,92,85), null, (70,155,179), null, (139,183,43), null, null, (70,155,179), null, (109,184,45), null, null, (102,180,65), null, null, null, null, null, null, null, (241,184,46), null, null, (169,82,86), null, null, (240,181,42), (235,37,20), null, null, (46,152,198) },
        { (239,118,31), null, null, (100,180,69), null, null, null, (240,181,42), null, null, (112,183,43), null, (222,177,58), null, null, (235,37,20), null, null, (69,28,0), (102,87,79), null, (93,160,158), null, (148,169,41), null, null, (95,159,158), null, (103,180,61), null, null, (95,175,84), null, null, null, null, null, null, null, (241,168,43), null, null, (150,94,104), null, null, null, (240,181,42), null, null, (46,152,198) },
        { (240,147,35), null, null, (162,183,44), (240,181,42), (240,181,42), (240,181,42), null, null, null, (112,182,45), null, (197,174,79), null, null, (235,37,20), null, null, (81,16,0), (100,84,73), null, (121,163,140), null, (159,150,37), null, null, (121,163,140), null, (95,176,80), null, null, (86,172,104), null, null, null, null, null, null, null, (238,151,41), null, null, (138,100,115), null, null, null, (240,181,42), null, null, (70,155,179) },
        { (241,177,39), null, null, null, null, null, null, null, null, (109,182,41), null, null, (171,169,97), null, null, (235,37,20), null, null, (96,8,0), (100,76,73), null, (147,166,117), null, (173,132,34), null, null, (148,167,120), null, (87,172,100), null, null, (78,166,125), null, null, null, null, null, null, null, (238,133,39), null, null, (121,111,133), null, null, null, (217,177,61), null, null, (97,158,158) },
        { (240,181,42), null, null, null, null, null, null, null, null, (110,178,41), null, null, (148,166,117), null, null, (235,37,20), null, null, (106,1,0), (100,73,70), null, (165,171,105), null, (180,115,30), null, null, (161,169,105), null, (79,168,122), null, null, (72,163,137), null, null, null, null, null, null, null, (233,115,32), null, null, (96,125,156), null, null, null, (196,173,78), null, null, (118,164,146) },
        { (241,182,43), null, null, (131,147,116), (122,77,74), (122,77,74), (122,77,74), (122,77,74), null, (112,182,45), (129,139,122), null, (141,166,125), null, null, (235,37,20), null, null, (111,0,0), (93,64,60), null, (172,168,94), null, (190,111,35), null, null, (173,169,95), null, (72,164,142), null, null, (69,163,147), null, null, null, null, null, null, null, (241,114,38), null, null, (72,139,178), null, null, null, (191,175,87), null, null, (123,162,133) },
        { (240,181,42), null, null, (109,184,45), (233,38,20), (233,38,20), (233,38,20), (233,38,20), null, null, (112,182,45), null, (121,163,140), null, null, (235,37,20), null, null, (120,0,0), (95,63,60), null, (197,174,79), null, (199,93,29), null, null, (197,174,79), null, (70,164,144), null, null, (60,160,163), null, null, null, null, null, null, null, (236,94,30), null, null, (50,154,201), null, null, null, (168,170,101), null, null, (145,167,119) },
        { (240,181,42), null, null, (101,160,156), null, null, null, (240,181,42), null, null, (112,182,45), null, (95,159,158), null, null, (236,52,21), null, (84,78,64), (255,140,57), (95,63,60), null, (222,177,58), null, (208,76,25), null, null, (222,177,58), null, (63,161,160), null, null, (46,152,198), null, null, null, null, null, null, null, (235,77,28), null, null, (50,154,201), null, null, null, (143,166,121), null, null, (171,169,97) },
        { (240,181,42), null, null, (75,156,176), null, (22,22,24), (26,26,26), (240,181,44), null, null, (110,182,49), null, (70,155,179), null, null, (235,37,20), null, (84,78,64), (255,140,57), (95,63,60), null, (240,181,44), null, (224,56,22), null, null, (240,181,44), null, (55,156,179), null, null, (46,152,198), null, null, null, null, null, null, null, (233,59,24), null, null, (50,154,199), null, null, (240,181,44), (116,109,134), null, null, (199,173,81) },
        { (240,181,42), null, null, (46,152,198), (188,174,83), (213,177,64), (239,181,44), (219,57,21), null, null, (96,175,80), null, (235,37,20), null, null, (124,165,42), (240,181,42), (242,179,46), (203,31,14), null, (240,181,40), (240,181,42), null, (235,37,20), null, null, (240,181,42), null, (46,152,198), null, null, (109,184,45), (109,184,45), (109,184,45), (124,165,42), (142,146,38), (119,109,130), (144,94,107), null, (232,42,22), null, null, (150,179,42), (240,181,42), (240,181,42), (235,37,20), null, null, null, (222,177,58) },
        { (240,181,42), null, null, null, (184,68,66), (209,53,45), (234,39,21), null, null, null, (83,170,113), null, (235,37,20), null, null, null, (235,37,20), (233,38,22), null, null, (240,181,40), (240,181,42), null, (235,37,20), null, null, (240,181,42), null, (235,37,20), null, null, null, null, null, null, null, null, (46,152,200), null, (50,154,199), null, null, (29,0,0), (235,37,20), (235,37,20), null, null, null, (240,181,42), null },
        { (240,181,42), null, null, null, null, null, null, null, (109,184,45), (240,181,42), null, null, null, (62,159,163), null, null, null, null, null, null, (240,181,40), null, null, (235,37,20), null, null, (240,181,42), null, (235,37,20), null, null, null, null, null, null, null, null, (46,152,200), null, (50,154,199), null, null, null, null, null, null, null, (240,181,42), (240,181,42), null },
        { (241,169,39), (235,37,20), (191,64,61), (167,79,84), (125,106,128), (95,122,152), (71,138,177), (46,152,198), (46,152,198), null, null, null, null, null, (116,109,134), (139,96,111), (163,80,88), (189,66,62), (225,43,27), (57,152,198), null, null, null, (235,37,20), (235,37,20), (109,184,45), (46,152,198), null, (240,149,36), (239,130,32), (239,111,29), (237,92,27), (237,75,25), (235,57,21), (235,37,20), (235,37,20), (235,37,20), null, null, (213,51,39), (178,73,75), (144,94,107), (119,109,130), (95,122,152), (71,138,177), (46,152,198), (46,152,198), (240,181,42), null, null }
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
