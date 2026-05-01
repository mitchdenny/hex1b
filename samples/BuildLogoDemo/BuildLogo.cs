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
        { (46,152,198), (46,152,198), (46,152,198), (71,138,177), (95,122,152), (119,109,130), (144,94,107), (167,79,84), (191,64,61), null, null, null, (46,152,198), (46,152,198), (46,152,198), (4,55,76), null, null, (235,37,20), (235,37,20), (235,37,20), null, null, (240,181,42), (240,181,42), (240,181,42), null, null, (46,152,198), (46,152,198), (46,152,198), null, null, null, null, null, null, null, (235,37,20), (235,37,20), (235,37,20), (219,56,23), (203,74,24), (188,93,29), (172,112,32), (156,130,34), (141,184,44), (122,183,44), null, null },
        { (46,152,198), null, null, null, null, null, null, null, (206,72,26), (236,52,21), null, null, (240,181,42), null, (109,93,64), (88,60,0), null, null, (109,184,45), null, null, (46,152,198), null, (139,183,43), null, null, (46,152,198), null, (109,184,45), null, null, (46,152,198), null, null, null, null, null, null, (240,181,42), null, null, null, null, null, null, null, (235,37,20), (122,183,44), null, null },
        { (235,37,20), null, null, null, null, null, null, null, null, (223,53,21), null, null, (240,181,42), null, (109,93,64), (88,60,0), null, null, (122,168,41), null, null, (46,152,198), null, (139,183,43), null, null, (46,152,198), null, (109,184,45), null, null, (46,152,198), null, null, null, null, null, null, (240,181,42), null, null, null, null, null, null, null, null, null, (109,184,45), null },
        { (236,58,22), null, null, (109,184,45), (235,37,20), (235,37,20), (235,37,20), (235,37,20), null, null, (106,181,58), null, (240,181,42), null, (109,93,64), (88,60,0), null, null, (139,150,39), null, null, (46,152,198), null, (139,183,43), null, null, (46,152,198), null, (109,184,45), null, null, (109,184,45), null, null, null, null, null, null, (240,181,42), null, null, (109,184,45), (109,184,45), (109,184,45), (126,183,45), (144,182,44), null, null, null, (109,184,45) },
        { (237,88,27), null, null, (92,174,87), null, null, null, (240,181,42), null, null, (112,182,45), null, (240,181,42), null, (80,38,34), (102,6,0), null, null, (156,130,34), null, null, (70,155,179), null, (139,183,43), null, null, (70,155,179), null, (109,184,45), null, null, (102,180,65), null, null, null, null, null, null, (240,181,42), null, null, (167,79,84), null, null, null, (240,181,42), (235,37,20), null, null, (46,152,198) },
        { (239,118,31), null, null, (100,180,69), null, null, null, (240,181,42), null, null, (112,183,43), null, (222,177,58), null, (82,39,35), (102,6,0), null, null, (172,112,32), null, null, (93,160,158), null, (148,169,41), null, null, (95,159,158), null, (103,180,61), null, null, (95,175,84), null, null, null, null, null, null, (239,167,34), null, null, (141,94,105), null, null, null, null, (240,181,42), null, null, (46,152,198) },
        { (240,147,35), null, null, (162,183,44), (240,181,42), (240,181,42), (240,181,42), null, null, null, (112,182,45), null, (197,174,79), null, (86,44,40), (102,6,0), null, null, (188,93,29), null, null, (121,163,140), null, (160,149,37), null, null, (121,163,140), null, (95,176,80), null, null, (86,172,104), null, null, null, null, null, null, (240,149,36), null, null, (138,96,113), null, null, null, null, (240,181,42), null, null, (70,155,179) },
        { (241,177,39), null, null, null, null, null, null, null, null, (109,182,41), null, null, (171,169,97), null, (86,44,40), (102,6,0), null, null, (203,74,24), null, null, (147,166,117), null, (174,131,34), null, null, (148,167,120), null, (87,172,100), null, null, (76,165,124), null, null, null, null, null, null, (238,129,31), null, null, (119,109,130), null, null, null, null, (217,177,61), null, null, (97,158,158) },
        { (240,181,40), null, null, null, null, null, null, null, null, (112,180,44), null, null, (153,164,110), null, (87,45,39), (102,6,0), null, null, (209,71,26), null, null, (150,166,112), null, (178,127,35), null, null, (148,166,112), null, (79,169,120), null, null, (79,167,130), null, null, null, null, null, null, (240,125,34), null, null, (95,122,152), null, null, null, null, (207,177,69), null, null, (103,158,153) },
        { (240,181,42), null, null, null, null, null, null, null, null, (112,182,45), null, null, (145,167,119), null, (87,45,41), (102,6,0), null, null, (219,56,23), null, null, (171,169,97), null, (185,112,32), null, null, (171,169,97), null, (75,164,134), null, null, (69,163,143), null, null, null, null, null, null, (239,111,29), null, null, (71,138,177), null, null, null, null, (193,173,82), null, null, (121,163,140) },
        { (240,181,42), null, null, (109,184,45), (235,37,20), (235,37,20), (235,37,22), (235,37,22), null, null, (114,181,45), null, (122,162,140), null, (89,44,41), (102,6,0), null, null, (235,37,20), null, null, (197,173,81), null, (199,93,29), null, null, (199,173,79), null, (73,164,140), null, null, (63,158,163), null, null, null, null, null, null, (237,92,27), null, null, (46,152,198), null, null, null, null, (168,170,101), null, null, (145,167,119) },
        { (240,181,42), null, null, (101,160,156), null, null, null, (240,181,42), null, null, (112,182,45), null, (95,159,158), null, (97,58,49), (101,7,0), null, (240,181,42), (235,37,20), null, null, (222,177,58), null, (210,75,25), null, null, (222,177,58), null, (63,161,160), null, null, (46,152,198), null, null, null, null, null, null, (237,75,25), null, null, (46,152,198), null, null, null, null, (143,166,121), null, null, (171,169,97) },
        { (240,181,42), null, null, (75,156,176), null, null, null, (240,181,42), null, null, (110,182,49), null, (70,155,179), null, (95,55,48), (102,6,0), null, (240,181,42), (235,37,20), null, null, (240,181,42), null, (224,56,22), null, null, (240,181,42), null, (55,156,179), null, null, (46,152,198), null, null, null, null, null, null, (235,57,21), null, null, (46,152,198), null, null, null, (240,181,42), (116,109,134), null, null, (197,174,79) },
        { (240,181,42), null, null, (46,152,198), (188,174,83), (213,177,64), (239,181,44), (219,57,21), null, null, (96,175,80), null, (235,37,20), null, (69,78,52), (207,180,43), (240,181,42), (235,37,20), null, null, (240,181,42), (240,181,42), null, (235,37,20), null, null, (240,181,42), null, (46,152,198), null, null, (109,184,45), (109,184,45), (125,166,43), (142,146,38), (119,109,130), (144,94,107), null, (235,37,20), null, null, (144,182,44), (240,181,42), (240,181,42), (240,181,42), (235,37,20), null, null, null, (222,177,58) },
        { (240,181,42), null, null, null, (184,68,66), (209,53,45), (234,39,21), null, null, null, (83,170,113), null, (235,37,20), null, null, (184,40,27), (235,37,20), null, null, null, (240,181,42), (240,181,42), null, (235,37,20), null, null, (240,181,42), null, (235,37,20), null, null, null, null, null, null, null, (46,152,198), null, (46,152,198), null, null, null, (235,57,21), (235,37,20), (235,37,20), null, null, null, (240,181,42), null },
        { (240,181,42), null, null, null, null, null, null, null, (109,184,45), (240,181,42), null, null, null, (62,159,163), null, null, null, null, null, null, (240,181,42), null, null, (235,37,20), null, null, (240,181,42), null, (235,37,20), null, null, null, null, null, null, null, (46,152,198), null, (46,152,198), null, null, null, null, null, null, null, null, (240,181,42), (240,181,42), null },
        { (241,169,39), (235,37,20), (191,64,61), (168,79,84), (119,109,130), (95,122,152), (71,138,177), (46,152,198), (46,152,198), null, null, null, null, null, (120,109,133), (156,86,96), (187,67,64), (211,51,41), (235,37,20), (46,152,198), null, null, null, (235,37,20), (235,37,20), (109,184,45), (46,152,198), null, (240,149,36), (239,130,32), (239,111,29), (237,93,25), (240,73,27), (233,38,20), (235,37,20), (235,37,20), null, null, (216,49,37), (191,64,61), (167,79,84), (144,94,107), (119,109,130), (95,122,152), (71,138,177), (46,152,198), (46,152,198), (240,181,42), null, null }
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
