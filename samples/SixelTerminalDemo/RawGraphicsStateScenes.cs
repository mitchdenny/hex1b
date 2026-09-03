using System.Text;

/// <summary>
/// A cell position an <see cref="RawGraphicsStateScene"/> wants inspected after
/// its script runs, so the headless transcript can show exactly which raster
/// (if any) a given cell resolves to.
/// </summary>
/// <param name="Column">Zero-based column to probe with <c>GetSixelDataAt</c>.</param>
/// <param name="Row">Zero-based row to probe with <c>GetSixelDataAt</c>.</param>
/// <param name="Label">What the probe is expected to show, for the transcript.</param>
internal readonly record struct GraphicsStateProbe(int Column, int Row, string Label);

/// <summary>
/// A hand-authored scene that exercises Hex1b's independent Sixel raster
/// storage and placement lifetime state (stage #451): every byte is written by
/// hand, using raw CUP/DCS/alternate-screen control sequences with no
/// <c>SixelWidget</c> or <c>SixelEncoder</c> involved, so the scene stays an
/// independent contract probe of <c>SixelGraphicsState</c>.
/// </summary>
/// <param name="Name">The scene name, also used by <c>--scene</c>.</param>
/// <param name="Expected">What the graphics-state model should retain.</param>
/// <param name="Script">The complete raw byte script for the scene.</param>
/// <param name="Probes">Cell positions to report via <c>GetSixelDataAt</c> after the script runs.</param>
internal sealed record RawGraphicsStateScene(
    string Name,
    string Expected,
    string Script,
    IReadOnlyList<GraphicsStateProbe>? Probes = null)
{
    public byte[] Bytes => Encoding.ASCII.GetBytes(Script);
}

/// <summary>
/// Scenes demonstrating the independent placement/image ownership introduced
/// by stage #451: shared raster dedup, overlapping placements, geometry-only
/// retention, origin-cell overwrite survival, and main/alternate screen
/// isolation. See <c>tests/Hex1b.Tests/Sixel/SixelPlacementLifetimeTests.cs</c>
/// for the equivalent assertions run against the terminal directly.
/// </summary>
internal static class RawGraphicsStateScenes
{
    // A solid square, aspect 1:1, so its declared and rendered geometry match
    // exactly. Register 1 is defined fresh each time it appears so a scene
    // never depends on palette state left behind by an earlier scene.
    private static string RedSquare40 =>
        "7;1q\"1;1;40;40#1;2;100;0;0#1!40~-!40~-!40~-!40~-!40~-!40~-!40~";

    private static string GreenSquare40 =>
        "7;1q\"1;1;40;40#2;2;0;100;0#2!40~-!40~-!40~-!40~-!40~-!40~-!40~";

    private static string BlueSquare40 =>
        "7;1q\"1;1;40;40#3;1;240;50;100#3!40~-!40~-!40~-!40~-!40~-!40~-!40~";

    // Two cells wide (20px at a 10px cell) and one band tall, so a probe can
    // overwrite the left cell's text glyph while the right cell keeps
    // resolving to the same placement.
    private const string RedTwoCellBand = "7;1q#1;2;100;0;0#1!20~";

    // Declares an absurd canvas that exceeds the raster allocation policy, so
    // geometry is recorded but no pixels are ever allocated.
    private const string GeometryOnly =
        "7;1q\"1;1;999999999;999999999#1;2;100;0;0#1!240~";

    private static string Dcs(string payload) => $"\x1bP{payload}\x1b\\";
    private static string Cup(int row, int column) => $"\x1b[{row};{column}H";
    private const string EnterAlternateScreen = "\x1b[?1049h";
    private const string ExitAlternateScreen = "\x1b[?1049l";

    public static IReadOnlyList<RawGraphicsStateScene> All { get; } =
    [
        new(
            "Graphics state: two placements share one raster image",
            "two identical red #FF0000 squares (40x40px = 4x2 cells) at different\n  rows on screen. They are two independent placements backed by one shared\n  raster image (identical payload bytes deduplicate by content hash):\n  1 image, 2 placements. They are vertically separated because some native\n  Sixel renderers collapse or obscure multiple graphics that begin on the\n  same terminal row",
            Cup(2, 2) + Dcs(RedSquare40) + Cup(8, 2) + Dcs(RedSquare40)),
        new(
            "Graphics state: overlapping placements are both retained",
            "a red #FF0000 square at column 2 and a green #00FF00 square at column 5,\n  overlapping by one cell. The overlap cell shows green (the later write wins\n  the query), but the red placement is not erased: it is still fully retained\n  and still resolves correctly outside the overlap",
            Cup(2, 2) + Dcs(RedSquare40) + Cup(2, 5) + Dcs(GreenSquare40),
            Probes:
            [
                new(2, 1, "red-only column: should resolve to the red placement"),
                new(5, 1, "overlap column: should resolve to the green placement (written last)"),
            ]),
        new(
            "Graphics state: geometry-only placement is retained, not dropped",
            "nothing visible. The declared canvas is far larger than the raster\n  allocation policy allows, so no pixels are ever allocated, but the placement\n  itself is still retained (geometry-only), not silently discarded",
            Cup(2, 2) + Dcs(GeometryOnly)),
        new(
            "Graphics state: origin cell overwrite does not release the image",
            "a red #FF0000 two-cell band at row 2. The left cell is then overwritten\n  with the letter X. The image is still fully reachable afterwards: only the\n  left cell's text glyph changed, not the graphics ownership, so the right\n  cell still resolves to the same placement",
            Cup(2, 2) + Dcs(RedTwoCellBand) + Cup(2, 2) + "X",
            Probes: [new(2, 1, "right cell of the band: should still resolve to the placement")]),
        new(
            "Graphics state: alternate screen owns independent placements",
            "a red #FF0000 square drawn on the main screen, then the alternate screen\n  is entered and a blue #0000FF square is drawn there instead. While the\n  alternate screen is active its graphics state holds only the blue square:\n  the red square belongs to the main screen and is not visible here",
            Cup(2, 2) + Dcs(RedSquare40) + EnterAlternateScreen + Cup(2, 2) + Dcs(BlueSquare40)),
        new(
            "Graphics state: main screen survives an alternate-screen visit",
            "the same red #FF0000 square as the previous screen, still on the main\n  screen after a full round trip through the alternate screen (where a blue\n  square was drawn and then left behind). The main screen's graphics were\n  never touched by what happened on the alternate screen",
            Cup(2, 2) + Dcs(RedSquare40) + EnterAlternateScreen + Cup(2, 2) + Dcs(BlueSquare40) + ExitAlternateScreen),
        new(
            "Graphics state: repeated alternate-screen entry resets only the alternate state",
            "nothing visible. The alternate screen is entered and a green #00FF00\n  square is drawn, then the alternate screen is entered again (already\n  active). Re-entry resets only the alternate graphics state, so the earlier\n  alternate placement is gone even though nothing here ever touched the main\n  screen",
            Cup(2, 2) + Dcs(RedSquare40) + EnterAlternateScreen + Cup(2, 2) + Dcs(GreenSquare40) + EnterAlternateScreen),
    ];
}
