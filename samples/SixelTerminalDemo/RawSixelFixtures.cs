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
            "a 240x60 red rectangle (about 24x3 cells with 10x20 metrics)",
            "0;0q\"1;1;240;60#1;2;100;0;0#1" +
                RepeatBands("!240~", 10)),
        new(
            "RGB rounding",
            "three 240x6 bars at 0%, 50%, and 100% red; 50% rounds to 128, not 127",
            "0;1q\"1;1;240;18#1;2;0;0;0#2;2;50;0;0#3;2;100;0;0" +
                "#1!240~-#2!240~-#3!240~"),
        new(
            "Two-color carriage return",
            "a 240x36 block with obvious green/red stripes overprinted using DECGCR",
            "0;1q\"1;1;240;36#1;2;100;0;0#2;2;0;100;0" +
                RepeatBands("#1!240w$#2!240B", 6)),
        new(
            "Overprint wins last",
            "red paints first and green overprints the same pixels through DECGCR",
            "0;1q\"1;1;240;6#1;2;100;0;0#2;2;0;100;0#1!240~$#2!240~"),
        new(
            "HLS color",
            "a 240x24 block defined with HLS coordinates",
            "0;0q\"1;1;240;24#3;1;0;50;100#3" +
                RepeatBands("!240~", 4)),
        new(
            "DEC HLS hue wheel",
            "hue 0 is blue, 120 is red, and 240 is green on the DEC wheel",
            "0;1q\"1;1;240;18#1;1;0;50;100#2;1;120;50;100#3;1;240;50;100" +
                "#1!240~-#2!240~-#3!240~"),
        new(
            "Palette persistence",
            "the second sequence selects register 5 without defining it and stays red",
            "0;1q\"1;1;240;6#5!240~",
            SetupPayload: "0;1q\"1;1;240;6#5;2;100;0;0#5!240~"),
        new(
            "Transparent background",
            "a 240x24 field with thick red rules and transparent gaps",
            "0;1q\"1;1;240;24#1;2;100;0;0#1" +
                RepeatBands("!240N", 4)),
        new(
            "Opaque background",
            "P2=0 fills every unpainted pixel of the canvas with the captured terminal background",
            "0;0q\"1;1;240;24#1;2;100;0;0#1!240@"),
        new(
            "DEC default aspect macro",
            "omitted P1 selects 2:1 pixels; 120 complete columns have 120x12 logical geometry",
            "q#1;2;100;0;0#1!120~"),
        new(
            "Square aspect macro",
            "P1=7 selects 1:1 pixels, so the same six rows render half as tall as the 2:1 default",
            "7;1q#1;2;100;0;0#1!120~"),
        new(
            "DECGRA Pan and Pad",
            "Pan=3 and Pad=1 override the P1 macro and render six logical rows as eighteen",
            "7;1q\"3;1;240;6#1;2;100;0;0#1!240~"),
        new(
            "Declared extent is a hint",
            "an 80x24 green declared region is followed by red overflow; the model grows to 240x60",
            "7q\"1;1;80;24#1;2;100;0;0#2;2;0;100;0" +
                RepeatBands("#2!80~#1!160~", 10)),
        new(
            "Partial band",
            "the second band paints only its top two rows, so the painted height is exactly eight",
            "7;1q\"1;1;240;8#1;2;100;0;0#1!240~-!240B"),
        new(
            "Transparent geometry",
            "240 transparent columns set geometry; DECGCR then paints only the leftmost 80",
            "7;1q!240?$#1;2;100;0;100#1!80~"),
        new(
            "Geometry only",
            "a declared canvas beyond the raster policy keeps geometry but allocates no pixels",
            "7;1q\"1;1;999999999;999999999#1;2;100;0;0#1!240~"),
    ];

    private static string RepeatBands(string band, int count) =>
        string.Join('-', Enumerable.Repeat(band, count));
}
