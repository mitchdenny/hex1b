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
            "a 24x12 red rectangle",
            "0;0q\"1;1;24;12#1;2;100;0;0#1!24~-!24~"),
        new(
            "Two-color carriage return",
            "green top rows and red bottom rows overprinted in the same sixel band",
            "0;1q\"1;1;24;6#1;2;100;0;0#1!24w$#2;2;0;100;0#2!24B"),
        new(
            "HLS color",
            "a 24x6 blue block defined with HLS coordinates",
            "0;0q\"1;1;24;6#3;1;0;50;100#3!24~"),
        new(
            "Transparent background",
            "red top pixels with untouched background below",
            "0;1q\"1;1;24;6#1;2;100;0;0#1!24@"),
        new(
            "DEC default aspect macro",
            "omitted P1 selects 2:1 pixels; four complete columns have 4x12 logical geometry",
            "q#1;2;100;0;0#1!4~"),
        new(
            "Declared extent is a hint",
            "a 2x3 declaration grows to the 4x12 data and painted extent",
            "7q\"1;1;2;3#2;2;0;100;0#2!4~-!4~"),
        new(
            "Transparent geometry",
            "four transparent columns advance data geometry without painted bounds",
            "7;1q????"),
        new(
            "Metadata-only raster",
            "DECGRA and DECGCI retain a 10x7 logical canvas without raster data",
            "7;1;42q\"1;1;10;7#9;2;25;50;75"),
    ];
}
