using System.Text;

internal sealed record RawSixelFixture(
    string Name,
    string Expected,
    string Payload)
{
    public byte[] StandardDcsBytes =>
        Encoding.ASCII.GetBytes($"\x1bP{Payload}\x1b\\");
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
            "Two-color carriage return",
            "a 240x36 block with obvious green/red stripes overprinted using DECGCR",
            "0;1q\"1;1;240;36#1;2;100;0;0#2;2;0;100;0" +
                RepeatBands("#1!240w$#2!240B", 6)),
        new(
            "HLS color",
            "a 240x24 block defined with HLS coordinates",
            "0;0q\"1;1;240;24#3;1;0;50;100#3" +
                RepeatBands("!240~", 4)),
        new(
            "Transparent background",
            "a 240x24 field with thick red rules and transparent gaps",
            "0;1q\"1;1;240;24#1;2;100;0;0#1" +
                RepeatBands("!240N", 4)),
        new(
            "DEC default aspect macro",
            "omitted P1 selects 2:1 pixels; 120 complete columns have 120x12 logical geometry",
            "q#1;2;100;0;0#1!120~"),
        new(
            "Declared extent is a hint",
            "an 80x24 green declared region is followed by red overflow; the model grows to 240x60",
            "7q\"1;1;80;24#1;2;100;0;0#2;2;0;100;0" +
                RepeatBands("#2!80~#1!160~", 10)),
        new(
            "Transparent geometry",
            "240 transparent columns set geometry; DECGCR then paints only the leftmost 80",
            "7;1q!240?$#1;2;100;0;100#1!80~"),
    ];

    private static string RepeatBands(string band, int count) =>
        string.Join('-', Enumerable.Repeat(band, count));
}
