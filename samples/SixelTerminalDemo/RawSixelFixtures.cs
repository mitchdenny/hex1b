using System.Text;

internal sealed record RawSixelFixture(
    string Name,
    string Expected,
    string Payload,
    string? SetupPayload = null)
{
    public byte[] StandardDcsBytes =>
        Encoding.ASCII.GetBytes($"\x1bP{Payload}\x1b\\");

    /// <summary>
    /// An optional sequence sent before <see cref="Payload"/> so a scene can
    /// depend on terminal-scoped Sixel state such as persistent color registers.
    /// </summary>
    public byte[]? SetupDcsBytes =>
        SetupPayload is null ? null : Encoding.ASCII.GetBytes($"\x1bP{SetupPayload}\x1b\\");
}

internal static class RawSixelFixtures
{
    // Explicitly select each register after defining it. DEC DECGCI defines and
    // selects in one operation, but some modern terminals only define it.
    public static IReadOnlyList<RawSixelFixture> All { get; } =
    [
        new(
            "Solid RGB block",
            "a solid pure-red (#FF0000) rectangle, 240x60px = 24 cells wide by 3 cells tall",
            "0;0q\"1;1;240;60#1;2;100;0;0#1" +
                RepeatBands("!240~", 10)),
        new(
            "RGB rounding",
            "three stacked full-width bars, each 240x6px (24 cells wide, 1 cell tall):\n  black #000000 on top, mid-red #800000 in the middle, pure red #FF0000 at the bottom.\n  The middle bar is 128, not 127: 50% rounds up",
            "0;1q\"1;1;240;18#1;2;0;0;0#2;2;50;0;0#3;2;100;0;0" +
                "#1!240~-#2!240~-#3!240~"),
        new(
            "Two-color carriage return",
            "a 240x36px block (24 cells wide, ~2 cells tall) of six horizontal stripe pairs.\n  DECGCR returns to the left margin so green #00FF00 overprints red #FF0000:\n  the top edge reads green, the bottom edge red",
            "0;1q\"1;1;240;36#1;2;100;0;0#2;2;0;100;0" +
                RepeatBands("#1!240w$#2!240B", 6)),
        new(
            "Overprint wins last",
            "a single thin 240x6px band (24 cells wide, well under one cell tall).\n  Red paints first, then DECGCR rewinds and green paints the same pixels,\n  so the band appears entirely green #00FF00: the last write wins",
            "0;1q\"1;1;240;6#1;2;100;0;0#2;2;0;100;0#1!240~$#2!240~"),
        new(
            "HLS color",
            "a solid 240x24px block (24 cells wide, ~1 cell tall) in pure blue #0000FF,\n  specified in HLS rather than RGB: hue 0 on the DEC wheel is blue, not red",
            "0;0q\"1;1;240;24#3;1;0;50;100#3" +
                RepeatBands("!240~", 4)),
        new(
            "DEC HLS hue wheel",
            "three stacked full-width bars, each 240x6px (24 cells wide), showing the DEC HLS wheel:\n  hue 0 = blue #0000FF on top, hue 120 = red #FF0000 in the middle,\n  hue 240 = green #00FF00 at the bottom",
            "0;1q\"1;1;240;18#1;1;0;50;100#2;1;120;50;100#3;1;240;50;100" +
                "#1!240~-#2!240~-#3!240~"),
        new(
            "Palette persistence",
            "one thin 240x6px band (24 cells wide) in pure red #FF0000.\n  An earlier sequence defined register 5 as red; this sequence only selects it.\n  The band is red because color registers persist across DCS sequences",
            "0;1q\"1;1;240;6#5!240~",
            SetupPayload: "0;1q\"1;1;240;6#5;2;100;0;0#5!240~"),
        new(
            "Transparent background",
            "four red #FF0000 horizontal rules across 240x24px (24 cells wide, ~1 cell tall).\n  The gaps between them are transparent, so whatever is behind shows through",
            "0;1q\"1;1;240;24#1;2;100;0;0#1" +
                RepeatBands("!240N", 4)),
        new(
            "Opaque background",
            "a 240x24px block (24 cells wide, ~1 cell tall) with a red #FF0000 top rule.\n  Unlike the transparent screen, P2=0 fills every unpainted pixel with the\n  terminal background, so the rest of the block is solid, not see-through",
            "0;0q\"1;1;240;24#1;2;100;0;0#1!240@"),
        new(
            "DEC default aspect macro",
            "a red #FF0000 bar 120px wide (12 cells) that renders 12px tall from only 6 raster rows:\n  omitting P1 selects the DEC 2:1 default, so each pixel is twice as tall as it is wide.\n  Compare directly with the next screen",
            "q#1;2;100;0;0#1!120~"),
        new(
            "Square aspect macro",
            "the same 120px-wide (12 cells) red #FF0000 bar and the same 6 raster rows as the\n  previous screen, but only 6px tall instead of 12px: P1=7 selects square 1:1 pixels.\n  This bar should look half the height of the one before it",
            "7;1q#1;2;100;0;0#1!120~"),
        new(
            "DECGRA Pan and Pad",
            "a red #FF0000 bar 240px wide (24 cells) rendered 18px tall from 6 raster rows.\n  DECGRA Pan=3/Pad=1 overrides the P1 macro, stretching each pixel 3x vertically,\n  so this is three times taller than the square-aspect screen",
            "7;1q\"3;1;240;6#1;2;100;0;0#1!240~"),
        new(
            "Declared extent is a hint",
            "a two-color block: a green #00FF00 stripe down the left 80px (8 cells) and\n  red #FF0000 filling the remaining 160px, 240x60px overall (24 cells wide, 3 tall).\n  The sequence declared only 80x24px, so the image is far larger than declared:\n  the declared extent is a hint, not a limit",
            "7q\"1;1;80;24#1;2;100;0;0#2;2;0;100;0" +
                RepeatBands("#2!80~#1!160~", 10)),
        new(
            "Partial band",
            "a red #FF0000 block 240px wide (24 cells). The first band is a full 6px tall;\n  the second paints only its top 2 rows, so the red stops at 8px even though the\n  band occupies 12px. The bottom 4px are transparent",
            "7;1q\"1;1;240;8#1;2;100;0;0#1!240~-!240B"),
        new(
            "Transparent geometry",
            "a magenta #FF00FF bar covering only the leftmost 80px (8 cells) of a 240px-wide\n  (24 cell) canvas. 240 transparent columns established the full width first,\n  so the image is 3x wider than the visible magenta",
            "7;1q!240?$#1;2;100;0;100#1!80~"),
        new(
            "Geometry only",
            "nothing visible. The sequence declares an absurd 999999999x999999999px canvas,\n  which exceeds the raster policy, so geometry is recorded but no pixels are\n  allocated and nothing is painted. The screen should stay blank",
            "7;1q\"1;1;999999999;999999999#1;2;100;0;0#1!240~"),
    ];

    private static string RepeatBands(string band, int count) =>
        string.Join('-', Enumerable.Repeat(band, count));
}
