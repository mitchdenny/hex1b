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
            "the graphic is anchored at row 3 column 5 and the probe text starts on the row below it, in the same column",
            "\x1b[?80h\x1b[3;5H",
            Square40,
            "<- cursor returned to the original column"),
        new(
            "Cursor non-scrolling mode",
            "the graphic is drawn at the page origin and the probe text stays exactly where the cursor already was",
            "\x1b[?80l\x1b[6;20H",
            Square40,
            "<- cursor never moved"),
        new(
            "Cursor mode 8452",
            "mode 8452 leaves the cursor to the right of the graphic instead of at the original column",
            "\x1b[?80h\x1b[?8452h\x1b[3;5H",
            Square40,
            "<- cursor left to the right of the graphic"),
        new(
            "Margin clipping",
            "left/right margins clip the 200px graphic to columns 10-24 without changing its source raster",
            "\x1b[?80h\x1b[?69h\x1b[10;25s\x1b[2;12r\x1b[4;10H",
            Wide200,
            "clipped"),
        new(
            "Bottom margin completion",
            "the graphic starts on the last row of a small scrolling region, so the region scrolls and the cursor stays inside it",
            "\x1b[?80h\x1b[10;14r\x1b[14;3H",
            Square40,
            "<- cursor stayed inside the region"),
        new(
            "Explicit repositioning",
            "Hex1b never assumes where an upstream terminal leaves the cursor: managed output re-positions with CUP first",
            "\x1b[?80h\x1b[18;5H",
            Square40,
            "\x1b[20;40Hexplicitly positioned"),
    ];
}
