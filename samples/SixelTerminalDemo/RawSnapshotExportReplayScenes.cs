using System.Text;

/// <summary>
/// Which additional headless evidence a <see cref="RawSnapshotExportReplayScene"/>
/// wants gathered, since issue #456's snapshot/export/recording/replay
/// requirements each need materially different follow-up work beyond just
/// replaying the script (see <c>Program.cs</c>'s
/// <c>InspectSnapshotExportReplaySceneAsync</c>).
/// </summary>
internal enum SnapshotExportReplayScenarioKind
{
    /// <summary>Multiple snapshots safely sharing one raster, with independent, idempotent disposal.</summary>
    SnapshotSharing,

    /// <summary>Viewport-only vs history-inclusive vs current-width vs original-width projections.</summary>
    Projections,

    /// <summary>SVG/HTML export of a geometry-only placement produces an explicit diagnostic placeholder.</summary>
    GeometryOnlyExport,

    /// <summary>Repeated SVG/HTML export of the same snapshot is deterministic.</summary>
    DeterministicExport,

    /// <summary>Record/serialize/replay reconstructs damaged, scrolled-into-history state.</summary>
    RecordReplayWithDamage,

    /// <summary>Recording captures only the active screen, independently across main/alternate transitions.</summary>
    MainAlternateIndependence,

    /// <summary>Deserializing malformed/truncated/mismatched-version data fails explicitly.</summary>
    MalformedFailures,
}

/// <summary>
/// A hand-authored scene that exercises issue #456's snapshot, SVG/HTML
/// export, and HMP1 recording/replay contract: every byte is written by
/// hand, using raw CUP/DCS/alternate-screen control sequences with no
/// <c>SixelWidget</c> or <c>SixelEncoder</c> involved, so the scene stays an
/// independent contract probe of the snapshot/export/recording model,
/// mirroring <c>SixelSnapshotSharingTests.cs</c>, <c>SixelSvgExportTests.cs</c>,
/// <c>SixelHtmlExportTests.cs</c>, and <c>Hmp1SixelRecordingTests.cs</c>.
/// </summary>
/// <param name="Name">The scene name, also used by <c>--scene</c>.</param>
/// <param name="Expected">What the terminal model should retain after the script runs.</param>
/// <param name="Script">The complete raw byte script for the scene.</param>
/// <param name="Kind">Which additional headless evidence this scene wants gathered.</param>
/// <param name="ScrollbackCapacity">Rows of main-screen scrollback the demo terminal is built with.</param>
/// <param name="Checkpoints">
/// Ordered, cumulative script prefixes to snapshot independently, each a
/// complete valid script starting from the beginning. Used only by
/// <see cref="SnapshotExportReplayScenarioKind.MainAlternateIndependence"/>,
/// which needs to inspect state at several points across a screen
/// transition; every other scenario inspects only the full <see cref="Script"/>.
/// </param>
internal sealed record RawSnapshotExportReplayScene(
    string Name,
    string Expected,
    string Script,
    SnapshotExportReplayScenarioKind Kind,
    int ScrollbackCapacity = 0,
    IReadOnlyList<string>? Checkpoints = null)
{
    public byte[] Bytes => Encoding.ASCII.GetBytes(Script);
}

/// <summary>
/// Scenes demonstrating issue #456's snapshot model, SVG/HTML export, and
/// HMP1 recording/replay: raster sharing and disposal safety, the four
/// projection modes, the geometry-only export placeholder, deterministic
/// repeated export, damage/history surviving a record-serialize-replay round
/// trip, main/alternate screen independence under recording, and the
/// recording format's explicit (never success-shaped) failure modes. See
/// <c>docs/sixel-terminal-behavior.md</c>'s "#456" section for the full
/// contract this scene set mirrors.
/// </summary>
internal static class RawSnapshotExportReplayScenes
{
    // One raster band is 6 pixels tall by protocol. At this demo's 10x20px
    // cell metrics (see Program.cs's TerminalCapabilities), a band count is
    // chosen so the declared pixel height lands just past a cell boundary,
    // matching the convention established by RawScrollHistoryReflowScenes:
    // 4 bands = 24px -> ceil(24/20) = 2 rows; 1 band = 6px -> 1 row.
    private static string SolidBand(int pixelWidth, int bandCount, int register, string colorDefinition) =>
        $"7;1q\"1;1;{pixelWidth};{bandCount * 6}#{register};{colorDefinition}#{register}{string.Join("-", Enumerable.Repeat("!" + pixelWidth + "~", bandCount))}";

    // 1 cell wide, 1 row tall. Small and cheap to duplicate at two anchors so
    // a scene can demonstrate two placements sharing one raster.
    private static string RedOneColOneRow => SolidBand(10, 1, 1, "2;100;0;0");

    // 1 cell wide, 2 rows tall. Used for the projection scene, so scrolling
    // one row into history leaves exactly one row still in the viewport.
    private static string RedOneColTwoRow => SolidBand(10, 4, 1, "2;100;0;0");

    // 2 cells wide, 1 row tall, so a damage probe can overwrite one column
    // and leave the other intact.
    private static string BlueTwoColOneRow => SolidBand(20, 1, 1, "1;240;50;100");

    // Declares an absurd canvas that exceeds the raster allocation policy, so
    // geometry is recorded but no pixels are ever allocated (reused verbatim
    // from RawGraphicsStateScenes' GeometryOnly probe).
    private const string GeometryOnly =
        "7;1q\"1;1;999999999;999999999#1;2;100;0;0#1!240~";

    // A solid square declared at 40x40px, aspect 1:1, matching
    // RawGraphicsStateScenes' RedSquare40/GreenSquare40 exactly so this
    // scene set's exported pixels are directly comparable to that one's.
    private static string RedSquare40 =>
        "7;1q\"1;1;40;40#1;2;100;0;0#1!40~-!40~-!40~-!40~-!40~-!40~-!40~";

    private static string GreenSquare40 =>
        "7;1q\"1;1;40;40#2;2;0;100;0#2!40~-!40~-!40~-!40~-!40~-!40~-!40~";

    private static string Dcs(string payload) => $"\x1bP{payload}\x1b\\";
    private static string Cup(int row, int column) => $"\x1b[{row};{column}H";
    private static string Margins(int top, int bottom) => $"\x1b[{top};{bottom}r";
    private const string EnterAlternateScreen = "\x1b[?1049h";
    private const string ExitAlternateScreen = "\x1b[?1049l";

    public static IReadOnlyList<RawSnapshotExportReplayScene> All { get; } =
    [
        new(
            "Snapshot: two placements sharing one raster resolve to the same image instance",
            "the same red band painted at two anchors. Both placements reference\n  the identical decoded raster - the image is retained once per referenced\n  raster, never once per covered cell, and multiple snapshots of this state\n  observe the very same shared instance",
            Cup(1, 1) + Dcs(RedOneColOneRow) + Cup(3, 1) + Dcs(RedOneColOneRow),
            SnapshotExportReplayScenarioKind.SnapshotSharing),
        new(
            "Snapshot: viewport-only, history-inclusive, and width projections all agree on content",
            "a one-row-tall remainder of a two-row-tall red band, its top row\n  scrolled into main-screen history. A viewport-only snapshot omits the\n  scrolled row entirely; a history-inclusive snapshot restores it; current-\n  width and original-width projections describe the identical placement\n  geometry either way, since this scene never changes width",
            Margins(1, 3) + Cup(1, 1) + Dcs(RedOneColTwoRow) + Cup(3, 1) + "\n",
            SnapshotExportReplayScenarioKind.Projections,
            ScrollbackCapacity: 3),
        new(
            "Export: a geometry-only placement gets an explicit diagnostic placeholder, never silent omission",
            "nothing visible: the declared canvas exceeds the raster allocation\n  policy, so no pixels are ever allocated. Both the SVG and HTML exports\n  still describe this placement explicitly - a dashed diagnostic placeholder\n  in the SVG, and geometry-only metadata in the HTML - rather than silently\n  dropping it",
            Cup(2, 2) + Dcs(GeometryOnly),
            SnapshotExportReplayScenarioKind.GeometryOnlyExport),
        new(
            "Export: repeated SVG/HTML export of the same snapshot is byte-identical",
            "a solid red #FF0000 square. Exporting the very same snapshot to SVG\n  and to HTML twice each produces byte-identical output both times: export\n  reads only the authoritative snapshot state, never re-decoding or\n  re-hashing the payload independently per call",
            Cup(2, 2) + Dcs(RedSquare40),
            SnapshotExportReplayScenarioKind.DeterministicExport),
        new(
            "Recording: record/serialize/replay reconstructs damaged, history-bound state without a live terminal",
            "nothing on the main screen: the entire one-row-tall blue band scrolled\n  fully into history, with its left column destructively overwritten by X\n  before the scroll. Serializing that snapshot, deserializing it, and\n  replaying the reconstructed escape sequence into a brand-new terminal\n  reproduces the identical surviving right-column pixels and damage, through\n  the very same parser/raster path a live terminal would use",
            Margins(1, 3) + Cup(1, 1) + Dcs(BlueTwoColOneRow) + Cup(1, 1) + "X" + Cup(3, 1) + "\n",
            SnapshotExportReplayScenarioKind.RecordReplayWithDamage,
            ScrollbackCapacity: 3),
        new(
            "Recording: main and alternate screens record and replay independently across transitions",
            "a red square on the main screen. A recording taken while the\n  alternate screen is active (showing a different green square) reconstructs\n  only the alternate screen's own graphic; switching back to the main screen\n  and recording again reconstructs the original red square, completely\n  unaffected by whatever happened on the alternate screen in between",
            Cup(1, 1) + Dcs(RedSquare40) + EnterAlternateScreen + Cup(1, 1) + Dcs(GreenSquare40) + ExitAlternateScreen,
            SnapshotExportReplayScenarioKind.MainAlternateIndependence,
            Checkpoints:
            [
                Cup(1, 1) + Dcs(RedSquare40),
                Cup(1, 1) + Dcs(RedSquare40) + EnterAlternateScreen + Cup(1, 1) + Dcs(GreenSquare40),
                Cup(1, 1) + Dcs(RedSquare40) + EnterAlternateScreen + Cup(1, 1) + Dcs(GreenSquare40) + ExitAlternateScreen,
            ]),
        new(
            "Recording: malformed, truncated, and version-mismatched data fail explicitly, never silently",
            "a solid red #FF0000 square, recorded successfully once as a baseline.\n  Corrupting the recording's magic marker, truncating it mid-record, and\n  bumping its version number each produce a distinct, explicitly named\n  failure reason - never a broad catch or a success-shaped empty result",
            Cup(2, 2) + Dcs(RedSquare40),
            SnapshotExportReplayScenarioKind.MalformedFailures),
    ];
}
