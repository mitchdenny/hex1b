using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace BuildLogoDemo;

internal static class BuildLogo
{
    private const char UpperHalf = '▀';
    private const int Cols = 35;
    private const int Rows = 17;
    private static readonly (byte R, byte G, byte B)?[,] Pixels = new (byte, byte, byte)?[17, 35]
    {
        { (46,152,198), (46,152,198), (46,152,198), (71,138,177), (95,122,152), (119,109,130), (119,109,130), (144,94,107), (191,64,61), (46,152,198), (46,152,198), null, null, null, (235,37,20), (235,37,20), (240,181,42), (240,181,42), null, (46,152,198), (46,152,198), (46,152,198), null, null, null, null, (235,37,20), (235,37,20), (235,37,20), (219,56,23), (203,74,24), (188,93,29), (172,112,32), (151,179,49), (122,183,44) },
        { (46,152,198), null, null, null, null, null, null, null, (208,72,26), null, (240,181,42), null, null, null, (109,184,45), null, (40,59,0), null, (46,152,198), (109,184,45), null, null, (46,152,198), null, null, null, (240,181,42), null, null, null, null, null, null, (233,51,33), (122,183,44) },
        { (235,37,20), null, null, null, null, null, null, null, null, null, (240,181,42), null, null, null, (122,168,41), null, (40,59,0), null, (46,152,198), (109,184,45), null, null, (46,152,198), null, null, null, (240,181,42), null, null, null, null, null, null, null, null },
        { (236,58,22), null, null, (108,183,44), (225,45,19), (235,37,20), (235,37,20), (235,37,20), (118,78,76), null, (240,181,42), null, null, null, (139,150,39), null, (40,59,0), null, (46,152,198), (109,184,45), null, null, (109,184,45), null, null, null, (240,181,42), null, null, (109,184,45), (109,184,45), (109,184,45), (126,183,45), null, null },
        { (237,88,27), null, null, (92,174,87), null, null, null, null, (182,173,146), null, (235,37,20), null, null, null, (156,130,34), null, (40,59,0), null, (70,155,179), (109,184,45), null, null, (102,180,65), null, null, null, (240,181,42), null, null, (167,79,84), null, null, null, (210,44,0), null },
        { (239,118,31), null, null, (100,180,69), (130,142,122), null, null, null, (184,171,148), null, (235,37,20), null, null, null, (172,112,32), null, (43,50,0), null, (95,159,158), (103,180,61), null, null, (95,175,84), null, null, null, (239,167,34), null, null, (141,94,105), null, null, null, (248,202,67), null },
        { (240,147,35), null, null, (162,183,44), (237,182,44), (240,181,42), (240,181,42), (240,181,42), null, null, (235,37,20), null, null, null, (188,93,29), null, (56,41,0), null, (121,163,140), (95,176,80), null, null, (86,172,104), null, null, null, (240,149,36), null, null, (138,96,113), null, null, null, (248,201,69), null },
        { (241,177,39), null, null, null, null, null, null, null, null, null, (235,37,20), null, null, null, (203,74,24), null, (68,33,0), null, (148,167,120), (87,172,100), null, null, (79,165,125), null, null, null, (238,129,31), null, null, (119,109,130), null, null, null, (238,199,89), null },
        { (240,181,40), null, null, null, null, null, null, null, null, null, (235,37,20), null, null, null, (209,71,26), null, (68,28,0), null, (148,166,112), (79,169,120), null, null, (77,167,130), null, null, null, (240,125,34), null, null, (95,122,152), null, null, null, (229,198,97), null },
        { (240,181,42), null, null, null, null, null, null, null, null, null, (235,37,20), null, null, null, (219,56,23), null, (80,24,0), null, (171,169,97), (75,164,134), null, null, (69,163,143), null, null, null, (239,111,29), null, null, (71,138,177), null, null, null, (212,195,105), null },
        { (240,181,42), null, null, (109,184,45), (224,43,20), (235,37,20), (235,37,20), (235,37,22), (118,80,77), null, (236,36,22), null, null, null, (235,37,20), null, (90,15,0), null, (199,173,79), (73,164,140), null, null, (62,159,163), null, null, null, (237,92,27), null, null, (46,152,198), null, null, null, (190,190,119), null },
        { (240,181,42), null, null, (101,160,156), null, null, null, null, (184,172,146), null, (236,52,21), null, null, (240,181,42), (235,37,20), null, (97,4,0), null, (222,177,58), (63,161,160), null, null, (46,152,198), null, null, null, (237,75,25), null, null, (46,152,198), null, null, null, (166,185,137), null },
        { (240,181,42), null, null, (75,156,176), null, null, null, null, (184,172,146), null, (235,37,20), null, null, (240,181,42), (235,37,20), null, (107,0,0), null, (240,181,42), (55,156,179), null, null, (46,152,198), null, null, null, (235,57,21), null, null, (46,152,198), null, null, null, (126,102,85), null },
        { (240,181,42), null, null, (46,152,198), (184,174,93), (208,178,72), (213,177,64), (239,181,44), (119,88,83), null, (124,165,42), (240,181,42), (240,181,42), (235,37,20), null, null, (120,0,0), null, (240,181,42), (46,152,198), null, null, (109,184,45), (109,184,45), (109,184,45), (142,146,38), (235,37,20), null, null, (144,182,44), (240,181,42), (240,181,42), (240,181,42), (81,0,0), null },
        { (240,181,42), null, null, null, (185,69,69), (198,58,53), (209,53,45), (234,39,21), null, null, null, (235,37,20), (235,37,20), null, null, null, (120,0,0), null, (240,181,42), (235,37,20), null, null, null, null, null, null, (46,152,198), null, null, null, (235,57,21), (235,37,20), (235,37,20), null, null },
        { (240,181,42), null, null, null, null, null, null, null, (110,183,45), null, null, null, null, null, null, null, (120,0,0), null, (240,181,42), (235,37,20), null, null, null, null, null, null, (46,152,198), null, null, null, null, null, null, null, (240,181,42) },
        { (241,169,39), (235,37,20), (191,64,61), (167,79,84), (144,94,107), (119,109,130), (95,122,152), (71,138,177), (46,152,198), (116,109,134), (139,96,111), (163,80,88), (187,67,64), (211,51,41), (235,37,20), (46,152,198), (231,35,19), (109,184,45), (46,152,198), (240,149,36), (239,130,32), (239,111,29), (237,92,27), (237,75,25), (235,57,21), (235,37,20), (216,49,37), (191,64,61), (167,79,84), (144,94,107), (119,109,130), (95,122,152), (71,138,177), (46,152,198), (240,181,42) }
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
