using System.Text;

/// <summary>
/// A hand-authored scene that exercises Hex1b's Sixel cursor, DECSDM, and margin
/// semantics. Every byte is written by hand; no <c>SixelWidget</c> or encoder is
/// involved, so the scene stays an independent contract probe.
/// </summary>
/// <param name="Name">The scene name, also used by <c>--scene</c>.</param>
/// <param name="Expected">What a DEC-compatible terminal should show.</param>
/// <param name="Setup">Control sequences applied before the graphic.</param>
/// <param name="Payload">The Sixel payload, without DCS framing.</param>
/// <param name="Probe">Text written immediately after the graphic completes, so the final cursor position is visible.</param>
internal sealed record RawCursorScene(
    string Name,
    string Expected,
    string Setup,
    string Payload,
    string Probe)
{
    /// <summary>
    /// Restores every mode a scene may change, so scenes stay independent.
    /// </summary>
    public const string ResetSequence = "\x1b[?80h\x1b[?8452l\x1b[?69l\x1b[r\x1b[?6l";

    /// <summary>
    /// Gets the scene without its trailing reset, so an inspection can observe the
    /// final cursor position the Sixel sequence itself produced.
    /// </summary>
    public byte[] SceneBytes =>
        Encoding.ASCII.GetBytes($"{Setup}\x1bP{Payload}\x1b\\{Probe}");

    /// <summary>
    /// Gets the complete scene, including setup, graphic, probe text, and reset.
    /// </summary>
    public byte[] Bytes =>
        Encoding.ASCII.GetBytes($"{Setup}\x1bP{Payload}\x1b\\{Probe}{ResetSequence}");
}

internal static class RawCursorScenes
{
    // A 40x40 red square: four columns of complete bands, seven bands tall, with a
    // square aspect so the rendered extent matches the declared extent exactly.
    private const string Square40 = "7;1q\"1;1;40;40#1;2;100;0;0#1" +
        "!40~-!40~-!40~-!40~-!40~-!40~-!40~";

    // A deliberately wide graphic used to show horizontal margin clipping.
    private const string Wide200 = "7;1q\"1;1;200;20#1;2;0;100;0#1" +
        "!200~-!200~-!200~-!200~";

    public static IReadOnlyList<RawCursorScene> All { get; } =
    [
        new(
            "Cursor scrolling mode",
            "a red #FF0000 square, 40x40px = 4 cells wide by 3 cells tall, anchored at row 3\n  column 5. The probe text starts on the row directly below the square, back at\n  column 5: in scrolling mode the cursor returns to the column it started in",
            "\x1b[?80h\x1b[3;5H",
            Square40,
            "<- cursor returned to the original column"),
        new(
            "Cursor non-scrolling mode",
            "the same 4x3 cell red #FF0000 square, but drawn at the top-left page origin\n  even though the cursor was at row 6 column 20. The probe text appears at row 6\n  column 20, untouched: DECSDM off anchors the graphic to the page, not the cursor",
            "\x1b[?80l\x1b[6;20H",
            Square40,
            "<- cursor never moved"),
        new(
            "Cursor mode 8452",
            "the same 4x3 cell red #FF0000 square at row 3 column 5, but the probe text now\n  starts immediately to the RIGHT of the square rather than below-left of it.\n  Mode 8452 leaves the cursor beside the graphic",
            "\x1b[?80h\x1b[?8452h\x1b[3;5H",
            Square40,
            "<- cursor left to the right of the graphic"),
        new(
            "Margin clipping",
            "a green #00FF00 bar that is 200px (20 cells) wide in its source raster but is\n  visibly cut off at column 25 by the left/right margins, so only columns 10-24\n  are painted. The raster is unchanged; only the painting is clipped",
            "\x1b[?80h\x1b[?69h\x1b[10;25s\x1b[2;12r\x1b[4;10H",
            Wide200,
            "clipped"),
        new(
            "Bottom margin completion",
            "a 4x3 cell red #FF0000 square placed on the last row of a 5-row scrolling region\n  (rows 10-14). The region scrolls up to make room, so the square finishes inside\n  the region and the probe text stays within rows 10-14, never below it",
            "\x1b[?80h\x1b[10;14r\x1b[14;3H",
            Square40,
            "<- cursor stayed inside the region"),
        new(
            "Explicit repositioning",
            "a 4x3 cell red #FF0000 square at row 18 column 5, with the probe text at row 20\n  column 40, far from where the graphic ended. Hex1b never assumes where an\n  upstream terminal leaves the cursor, so managed output issues an explicit CUP first",
            "\x1b[?80h\x1b[18;5H",
            Square40,
            "\x1b[20;40Hexplicitly positioned"),
    ];
}
