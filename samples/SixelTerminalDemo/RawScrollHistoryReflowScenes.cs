using System.Text;

/// <summary>
/// A hand-authored scene that exercises Hex1b's independent Sixel placements
/// interacting with terminal scrolling, main-screen scrollback history,
/// viewport clipping, and resize (issue #452): every byte is written by hand,
/// using raw CUP/DCS/DECSTBM/DECLRMM/DECSDM/alternate-screen control
/// sequences with no <c>SixelWidget</c> or <c>SixelEncoder</c> involved, so
/// the scene stays an independent contract probe of the placement/scrolling
/// integration, mirroring <c>tests/Hex1b.Tests/Sixel/SixelScrollHistoryReflowTests.cs</c>.
/// </summary>
/// <param name="Name">The scene name, also used by <c>--scene</c>.</param>
/// <param name="Expected">What the terminal model should retain after the script (and any resize steps) run.</param>
/// <param name="Script">The complete raw byte script for the scene, run before any <see cref="ResizeSteps"/>.</param>
/// <param name="ScrollbackCapacity">
/// Rows of main-screen scrollback the demo terminal is built with. Zero
/// means the scene never expects a placement to survive being scrolled off
/// (scrolled-off content is simply discarded, matching a terminal with no
/// history buffer configured at all).
/// </param>
/// <param name="ResizeSteps">
/// Width/height pairs applied via <c>Hex1bTerminal.Resize</c>, in order,
/// after <see cref="Script"/> runs. Each step's resulting placement/viewport
/// state is folded into the headless observation note. Resize can only be
/// demonstrated headlessly here: the interactive demo pages through screens
/// on one fixed-size terminal, so an interactive run shows the scene's
/// pre-resize state only, with the headless note carrying the authoritative
/// evidence for what the resize itself does (see
/// <c>docs/sixel-terminal-behavior.md</c>'s "#452" section).
/// </param>
internal sealed record RawScrollHistoryReflowScene(
    string Name,
    string Expected,
    string Script,
    int ScrollbackCapacity = 0,
    IReadOnlyList<(int Width, int Height)>? ResizeSteps = null)
{
    public byte[] Bytes => Encoding.ASCII.GetBytes(Script);
}

/// <summary>
/// Scenes demonstrating independent Sixel placements moving through
/// scrolling, main-screen scrollback history, viewport clipping, and resize
/// (#452): LF/IND/RI/SU/SD, partial vertical and DECLRMM horizontal margins,
/// DECSDM, alternate-screen isolation, damage persisting across scroll, and
/// resize's deliberately non-destructive clip-without-reflow behavior. See
/// <c>tests/Hex1b.Tests/Sixel/SixelScrollHistoryReflowTests.cs</c> for the
/// equivalent integration assertions this scene set mirrors.
/// </summary>
internal static class RawScrollHistoryReflowScenes
{
    // One raster band is 6 pixels tall by protocol. At this demo's 10x20px
    // cell metrics (see Program.cs's TerminalCapabilities), a band count is
    // chosen so the declared pixel height lands just past a cell boundary:
    // 4 bands = 24px -> ceil(24/20) = 2 rows; 8 bands = 48px -> 3 rows; 1
    // band = 6px -> 1 row. Declaring an explicit raster header ("1;1;W;H)
    // keeps the geometry exact instead of depending on the default 2:1
    // aspect doubling a bare payload would get.
    private static string SolidBand(int pixelWidth, int bandCount, int register, string colorDefinition) =>
        $"7;1q\"1;1;{pixelWidth};{bandCount * 6}#{register};{colorDefinition}#{register}{string.Join("-", Enumerable.Repeat("!" + pixelWidth + "~", bandCount))}";

    // 1 cell wide, 2 cells tall (10x24px). Used for LF/IND/RI/SD scroll probes.
    private static string RedOneColTwoRow => SolidBand(10, 4, 1, "2;100;0;0");

    // 1 cell wide, 3 cells tall (10x48px). Used for the progressive-crop probe.
    private static string RedOneColThreeRow => SolidBand(10, 8, 1, "2;100;0;0");

    // 3 cells wide, 1 cell tall (30x6px). Used for the DECLRMM horizontal-margin probe.
    private static string GreenThreeColOneRow => SolidBand(30, 1, 2, "2;0;100;0");

    // 2 cells wide, 1 cell tall (20x6px). Used for the damage-persistence probe, so only
    // one of the two columns is overwritten with text.
    private static string BlueTwoColOneRow => SolidBand(20, 1, 3, "1;240;50;100");

    private static string Dcs(string payload) => $"\x1bP{payload}\x1b\\";
    private static string Cup(int row, int column) => $"\x1b[{row};{column}H";
    private static string Margins(int top, int bottom) => $"\x1b[{top};{bottom}r";
    private const string EnableDeclrmm = "\x1b[?69h";
    private static string HorizontalMargins(int left, int right) => $"\x1b[{left};{right}s";
    private const string EnableDecsdm = "\x1b[?80h";
    private const string EnterAlternateScreen = "\x1b[?1049h";
    private const string ExitAlternateScreen = "\x1b[?1049l";
    private const string ReverseIndex = "\x1bM";
    private const string ScrollDown = "\x1b[T";
    private const string ScrollUp = "\x1b[S";

    public static IReadOnlyList<RawScrollHistoryReflowScene> All { get; } =
    [
        new(
            "Scrolling/history: LF at the bottom margin moves a departing row into history",
            "a one-row-tall remainder of what was a two-row-tall red band: the top row\n  scrolled off into main-screen history (1 scrollback line), while the bottom\n  row is still an active, independently retained placement. IND (ESC D)\n  produces the identical result - both are ordinary bottom-margin scroll-ups",
            Margins(1, 3) + Cup(1, 1) + Dcs(RedOneColTwoRow) + Cup(3, 1) + "\n",
            ScrollbackCapacity: 3),
        new(
            "Scrolling: RI at the top margin shifts a placement down, never resurrecting departed rows",
            "the same red band shifted down by one row within its region after a\n  reverse-index (ESC M) at the region's top margin. Reverse scrolling only\n  ever shifts what is still active - it can never resurrect a row that has\n  already departed into history on an earlier forward scroll",
            Margins(2, 4) + Cup(4, 1) + Dcs(RedOneColTwoRow) + Cup(2, 1) + ReverseIndex),
        new(
            "Scrolling: SD (CSI T) shifts a placement down within an explicit vertical margin",
            "the same red band shifted down by one row, this time via the explicit\n  CSI Ps T \"scroll down\" sequence rather than reverse-index - both reverse\n  scroll into the region's headroom identically",
            Margins(2, 4) + Cup(4, 1) + Dcs(RedOneColTwoRow) + Cup(2, 1) + ScrollDown),
        new(
            "Scrolling/history: repeated scroll-up progressively crops a placement before removing it",
            "nothing: a three-row-tall red band inside a partial vertical margin is\n  cropped by one row on each of two successive scroll-ups (3 painted rows,\n  then 2, then 1), and a third scroll-up removes the fully cropped remainder\n  entirely. Progressive crop never restores a row once it has scrolled past\n  the margin - each step is a permanent, one-way reduction",
            Margins(2, 4) + Cup(2, 1) + Dcs(RedOneColThreeRow) + ScrollUp + ScrollUp + ScrollUp),
        new(
            "Scrolling: DECLRMM horizontal margins clip painting, and SU shifts a wholly-contained placement",
            "a green band, three cells wide, shifted up by one row after scroll-up.\n  DECLRMM (CSI ?69h) plus a CSI Ps;Ps s horizontal-margin declaration clips\n  Sixel painting unconditionally to the declared columns; a placement whose\n  declared footprint is wholly inside those columns is shifted by ordinary\n  vertical scrolling exactly like one with no horizontal margins at all",
            EnableDeclrmm + HorizontalMargins(4, 8) + Cup(2, 5) + Dcs(GreenThreeColOneRow) + ScrollUp),
        new(
            "Scrolling: DECSDM (Sixel Display Mode) does not gate ordinary scrolling",
            "a one-row-tall remainder in main-screen history exactly as in the plain\n  LF scene: enabling DECSDM (CSI ?80h) changes where a Sixel graphic is\n  positioned on write, not whether later ordinary line-feed/index scrolling\n  moves it into history - the two behaviors are independent",
            EnableDecsdm + Margins(1, 3) + Cup(1, 1) + Dcs(RedOneColTwoRow) + Cup(3, 1) + "\n",
            ScrollbackCapacity: 3),
        new(
            "Scrolling: alternate-screen scrolling never creates or affects main-screen history",
            "the same red band's one-row-tall main-screen remainder as the plain LF\n  scene, unaffected by four line feeds sent after entering, then a fourth\n  line feed and exiting, the alternate screen. Alternate-screen scrolling is\n  fully isolated from main-screen history: it never adds scrollback lines\n  and never touches the main screen's placements",
            Margins(1, 3) + Cup(1, 1) + Dcs(RedOneColTwoRow) + Cup(3, 1) + "\n"
                + EnterAlternateScreen + "\n\n\n\n" + ExitAlternateScreen,
            ScrollbackCapacity: 4),
        new(
            "Damage (#453): destructive text damage applied before a scroll survives the scroll and any snapshot",
            "nothing on the main screen: the entire one-row-tall blue band scrolled\n  fully into history. Its left column was overwritten with X before the\n  scroll, so that cell's pixels are permanently gone (destructive damage);\n  the untouched right column is still visible. Both survive the scroll and a\n  history/snapshot round trip unchanged - see the headless note for the\n  per-cell damage evidence",
            Margins(1, 3) + Cup(1, 1) + Dcs(BlueTwoColOneRow) + Cup(1, 1) + "X" + Cup(3, 1) + "\n",
            ScrollbackCapacity: 3),
        new(
            "Resize: shrinking the viewport clips visibility without destroying the placement's own painted state",
            "the same three-row-tall red band as the progressive-crop scene, still\n  declaring all three painted rows internally after a resize down to one row\n  tall - only the *observed* viewport narrows. Resizing back to the original\n  height reveals the untouched rows again: unlike scroll-margin cropping,\n  viewport-only resize never permanently mutates a placement's painted\n  window (see the headless note for the row-by-row evidence)",
            Cup(1, 1) + Dcs(RedOneColThreeRow),
            ResizeSteps: [(20, 1), (20, 3)]),
        new(
            "Resize: narrowing the viewport clips visibility without destroying the placement's own painted state",
            "the same three-cell-wide green band as the DECLRMM scene, still declaring\n  all three painted columns internally after a resize down to one column\n  wide - only the *observed* viewport narrows. Resizing back to the original\n  width reveals the untouched columns again, for the same reason a shorter\n  viewport never destroys painted rows",
            Cup(1, 1) + Dcs(GreenThreeColOneRow),
            ResizeSteps: [(1, 5), (20, 5)]),
    ];
}
