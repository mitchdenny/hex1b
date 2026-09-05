using System.Security.Cryptography;
using System.Text;
using Hex1b;
using Hex1b.Automation;

var headless = args.Contains("--headless", StringComparer.OrdinalIgnoreCase);
var sceneFilter = GetOption(args, "--scene");
var screenOption = GetOption(args, "--screen");
// Wide enough for the longest description line, and tall enough that the cursor
// scenes (which paint as low as row 20) never reach the description block.
const int Width = 100;
const int Height = 32;

var fixtures = sceneFilter is null
    ? RawSixelFixtures.All
    : RawSixelFixtures.All
        .Where(fixture => fixture.Name.Contains(sceneFilter, StringComparison.OrdinalIgnoreCase))
        .ToArray();
var cursorScenes = sceneFilter is null
    ? RawCursorScenes.All
    : RawCursorScenes.All
        .Where(scene => scene.Name.Contains(sceneFilter, StringComparison.OrdinalIgnoreCase))
        .ToArray();
var graphicsStateScenes = sceneFilter is null
    ? RawGraphicsStateScenes.All
    : RawGraphicsStateScenes.All
        .Where(scene => scene.Name.Contains(sceneFilter, StringComparison.OrdinalIgnoreCase))
        .ToArray();
var scrollHistoryReflowScenes = sceneFilter is null
    ? RawScrollHistoryReflowScenes.All
    : RawScrollHistoryReflowScenes.All
        .Where(scene => scene.Name.Contains(sceneFilter, StringComparison.OrdinalIgnoreCase))
        .ToArray();
var snapshotExportReplayScenes = sceneFilter is null
    ? RawSnapshotExportReplayScenes.All
    : RawSnapshotExportReplayScenes.All
        .Where(scene => scene.Name.Contains(sceneFilter, StringComparison.OrdinalIgnoreCase))
        .ToArray();
if (fixtures.Count == 0 && cursorScenes.Count == 0 && graphicsStateScenes.Count == 0
    && scrollHistoryReflowScenes.Count == 0 && snapshotExportReplayScenes.Count == 0)
{
    throw new ArgumentException($"No Sixel demo scene contains '{sceneFilter}'.", nameof(args));
}
var capabilities = new TerminalCapabilities
{
    SupportsSixel = true,
    SupportsTrueColor = true,
    Supports256Colors = true,
    CellPixelWidth = 10,
    CellPixelHeight = 20,
};

var modelDescriptions = new string[fixtures.Count];
for (var index = 0; index < fixtures.Count; index++)
{
    modelDescriptions[index] = await InspectModelAsync(fixtures[index]);
}

var cursorObservations = new string[cursorScenes.Count];
for (var index = 0; index < cursorScenes.Count; index++)
{
    cursorObservations[index] = await InspectCursorSceneAsync(cursorScenes[index]);
}

var graphicsStateObservations = new string[graphicsStateScenes.Count];
for (var index = 0; index < graphicsStateScenes.Count; index++)
{
    graphicsStateObservations[index] = await InspectGraphicsStateSceneAsync(graphicsStateScenes[index]);
}

var scrollHistoryReflowObservations = new string[scrollHistoryReflowScenes.Count];
for (var index = 0; index < scrollHistoryReflowScenes.Count; index++)
{
    scrollHistoryReflowObservations[index] = await InspectScrollHistoryReflowSceneAsync(scrollHistoryReflowScenes[index]);
}

var snapshotExportReplayObservations = new string[snapshotExportReplayScenes.Count];
for (var index = 0; index < snapshotExportReplayScenes.Count; index++)
{
    snapshotExportReplayObservations[index] = await InspectSnapshotExportReplaySceneAsync(snapshotExportReplayScenes[index]);
}

// Capability discovery (#455) is a wire-protocol probing concern with no
// visual/raster component, so it has no DemoScreen and only ever runs headless.
var capabilityDiscoveryObservations = sceneFilter is null
    ? await CapabilityDiscoveryScenarios.RunAllAsync()
    : [];

// Routing, translation, and sanitization (#458) is also a headless-only,
// no-real-terminal concern: every route/policy combination is independent of
// which paged screen (if any) is being viewed.
var routingTranslationObservations = sceneFilter is null
    ? await RoutingTranslationScenarios.RunAllAsync()
    : [];

var allScreens = DemoScreens.Build(
    fixtures,
    modelDescriptions,
    cursorScenes,
    graphicsStateScenes,
    graphicsStateObservations,
    scrollHistoryReflowScenes,
    scrollHistoryReflowObservations,
    snapshotExportReplayScenes,
    snapshotExportReplayObservations,
    includeTransportScenes: sceneFilter is null);

// --screen selects one numbered screen. The number keeps its original value so a
// screen referenced in review still identifies the same subject when run alone.
var screens = allScreens;
if (screenOption is not null)
{
    if (!int.TryParse(screenOption, out var requested))
    {
        throw new ArgumentException($"--screen expects a number, not '{screenOption}'.", nameof(args));
    }

    screens = allScreens.Where(screen => screen.Number == requested).ToArray();
    if (screens.Count == 0)
    {
        throw new ArgumentException(
            $"No screen numbered {requested}; the demo has {allScreens.Count}.",
            nameof(args));
    }
}

if (headless)
{
    WriteHeadlessTranscript(
        allScreens,
        screens,
        fixtures,
        modelDescriptions,
        cursorScenes,
        cursorObservations,
        graphicsStateScenes,
        graphicsStateObservations,
        scrollHistoryReflowScenes,
        scrollHistoryReflowObservations,
        snapshotExportReplayScenes,
        snapshotExportReplayObservations,
        capabilityDiscoveryObservations,
        routingTranslationObservations);
    return;
}

var workload = new PagedScreenWorkloadAdapter(screens, allScreens.Count, promptRow: Height);
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithWorkload(workload)
    .WithPresentation(new ConsolePresentationAdapter(enableMouse: false))
    .WithDimensions(Width, Height)
    .Build();

Console.Error.WriteLine(
    "SixelTerminalDemo pages through numbered screens of independently authored, ESC-framed fixtures.");
Console.Error.WriteLine("Enter or Space advances, p goes back, and q quits.");
Console.Error.WriteLine($"Use --screen <number> to open one screen directly (1-{allScreens.Count}).");

await terminal.RunAsync();

static void WriteHeadlessTranscript(
    IReadOnlyList<DemoScreen> allScreens,
    IReadOnlyList<DemoScreen> selected,
    IReadOnlyList<RawSixelFixture> fixtures,
    IReadOnlyList<string> modelDescriptions,
    IReadOnlyList<RawCursorScene> cursorScenes,
    IReadOnlyList<string> cursorObservations,
    IReadOnlyList<RawGraphicsStateScene> graphicsStateScenes,
    IReadOnlyList<string> graphicsStateObservations,
    IReadOnlyList<RawScrollHistoryReflowScene> scrollHistoryReflowScenes,
    IReadOnlyList<string> scrollHistoryReflowObservations,
    IReadOnlyList<RawSnapshotExportReplayScene> snapshotExportReplayScenes,
    IReadOnlyList<string> snapshotExportReplayObservations,
    IReadOnlyList<(string Title, string Observation)> capabilityDiscoveryObservations,
    IReadOnlyList<(string Title, string Observation)> routingTranslationObservations)
{
    Console.WriteLine($"Hex1b Sixel demo: {allScreens.Count} numbered screens.");
    Console.WriteLine("Run without --headless to page through them; --screen <number> opens one.");
    Console.WriteLine();

    Console.WriteLine("Screens:");
    foreach (var screen in selected)
    {
        Console.WriteLine($"  {screen.Number,3}. {screen.Title}");
        Console.WriteLine($"       expected: {screen.Expected}");
    }

    Console.WriteLine();
    Console.WriteLine("Deterministic grammar and geometry model:");
    for (var index = 0; index < fixtures.Count; index++)
    {
        Console.WriteLine($"  {fixtures[index].Name}: {modelDescriptions[index]}");
    }

    Console.WriteLine();
    Console.WriteLine("Cursor, DECSDM, and margin observations:");
    for (var index = 0; index < cursorScenes.Count; index++)
    {
        Console.WriteLine($"  {cursorScenes[index].Name}: {cursorObservations[index]}");
    }

    Console.WriteLine();
    Console.WriteLine("Independent graphics-state ownership observations (#451):");
    for (var index = 0; index < graphicsStateScenes.Count; index++)
    {
        Console.WriteLine($"  {graphicsStateScenes[index].Name}: {graphicsStateObservations[index]}");
    }

    Console.WriteLine();
    Console.WriteLine("Scrolling, history, and resize observations (#452):");
    for (var index = 0; index < scrollHistoryReflowScenes.Count; index++)
    {
        Console.WriteLine($"  {scrollHistoryReflowScenes[index].Name}: {scrollHistoryReflowObservations[index]}");
    }

    Console.WriteLine();
    Console.WriteLine("Snapshot, export, and recording/replay observations (#456):");
    for (var index = 0; index < snapshotExportReplayScenes.Count; index++)
    {
        Console.WriteLine($"  {snapshotExportReplayScenes[index].Name}: {snapshotExportReplayObservations[index]}");
    }

    Console.WriteLine();
    Console.WriteLine("Capability discovery and query ownership observations (#455):");
    foreach (var (title, observation) in capabilityDiscoveryObservations)
    {
        Console.WriteLine($"  {title}: {observation}");
    }

    Console.WriteLine();
    Console.WriteLine("Routing, translation, and sanitization observations (#458):");
    foreach (var (title, observation) in routingTranslationObservations)
    {
        Console.WriteLine($"  {title}: {observation}");
    }
}

static string? GetOption(string[] arguments, string option)
{
    for (var index = 0; index < arguments.Length - 1; index++)
    {
        if (string.Equals(arguments[index], option, StringComparison.OrdinalIgnoreCase))
        {
            return arguments[index + 1];
        }
    }

    return null;
}

static async Task<string> InspectCursorSceneAsync(RawCursorScene scene)
{
    var capabilities = new TerminalCapabilities
    {
        SupportsSixel = true,
        SupportsTrueColor = true,
        Supports256Colors = true,
        CellPixelWidth = 10,
        CellPixelHeight = 20,
    };
    // The reset sequence is omitted here: DECSTBM homes the cursor, which would
    // hide the very position this scene exists to demonstrate.
    var workload = new DemoWorkloadAdapter([scene.SceneBytes]);
    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithWorkload(workload)
        .WithPresentation(new HeadlessPresentationAdapter(80, 24, capabilities))
        .WithDimensions(80, 24)
        .Build();
    await terminal.RunAsync();

    using var snapshot = terminal.CreateSnapshot();
    var builder = new StringBuilder();
    var origins = new List<string>();
    var occupiedColumns = new SortedSet<int>();
    var occupiedRows = new SortedSet<int>();
    foreach (var placement in snapshot.SixelPlacements)
    {
        if (!placement.HasPaintedExtent)
            continue;

        for (var y = placement.PaintedTop; y <= placement.PaintedBottom; y++)
            occupiedRows.Add(y);
        for (var x = placement.PaintedLeft; x <= placement.PaintedRight; x++)
            occupiedColumns.Add(x);

        origins.Add(
            $"({placement.Column},{placement.Row}) {placement.Image.WidthInCells}x{placement.Image.HeightInCells} cells");
    }

    builder.Append(origins.Count == 0 ? "no placement" : string.Join("; ", origins));
    if (occupiedColumns.Count > 0)
    {
        builder.Append($", painted columns {occupiedColumns.Min}-{occupiedColumns.Max}");
        builder.Append($", painted rows {occupiedRows.Min}-{occupiedRows.Max}");
    }

    builder.Append($", cursor ({snapshot.CursorX}, {snapshot.CursorY})");
    return builder.ToString();
}

static async Task<string> InspectGraphicsStateSceneAsync(RawGraphicsStateScene scene)
{
    var capabilities = new TerminalCapabilities
    {
        SupportsSixel = true,
        SupportsTrueColor = true,
        Supports256Colors = true,
        CellPixelWidth = 10,
        CellPixelHeight = 20,
    };
    var workload = new DemoWorkloadAdapter([scene.Bytes]);
    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithWorkload(workload)
        .WithPresentation(new HeadlessPresentationAdapter(80, 24, capabilities))
        .WithDimensions(80, 24)
        .Build();
    await terminal.RunAsync();

    var builder = new StringBuilder();
    builder.Append($"{terminal.SixelPlacementCount} placement(s) on the active screen, ");
    builder.Append($"{terminal.TrackedSixelCount} distinct image(s)");

    foreach (var placement in terminal.SixelPlacements)
    {
        builder.Append($"; ({placement.Column},{placement.Row}) {placement.WidthInCells}x{placement.HeightInCells} cells");
        builder.Append(placement.IsGeometryOnly
            ? " geometry-only"
            : $" {DescribeSixelColor(placement.Image)}");
    }

    if (scene.Probes is { Count: > 0 } probes)
    {
        foreach (var probe in probes)
        {
            var resolved = terminal.GetSixelDataAt(probe.Column, probe.Row);
            var description = resolved is null ? "no placement" : DescribeSixelColor(resolved);
            builder.Append($"; probe ({probe.Column},{probe.Row}) [{probe.Label}] -> {description}");
        }
    }

    return builder.ToString();
}

static string DescribeSixelColor(SixelData image)
{
    // Registers are defined with a fixed color per demo scene (see
    // RawGraphicsStateScenes), so the payload text itself identifies which
    // scripted square/band produced a given image without needing to decode
    // the raster.
    if (image.Payload.Contains("100;0;0"))
        return "red";
    if (image.Payload.Contains("0;100;0"))
        return "green";
    if (image.Payload.Contains("240;50;100"))
        return "blue";
    return "unrecognized color";
}

static async Task<string> InspectScrollHistoryReflowSceneAsync(RawScrollHistoryReflowScene scene)
{
    var capabilities = new TerminalCapabilities
    {
        SupportsSixel = true,
        SupportsTrueColor = true,
        Supports256Colors = true,
        CellPixelWidth = 10,
        CellPixelHeight = 20,
    };
    var workload = new DemoWorkloadAdapter([scene.Bytes]);
    var terminalBuilder = Hex1bTerminal.CreateBuilder()
        .WithWorkload(workload)
        .WithPresentation(new HeadlessPresentationAdapter(80, 24, capabilities))
        .WithDimensions(80, 24);
    if (scene.ScrollbackCapacity > 0)
    {
        terminalBuilder = terminalBuilder.WithScrollback(scene.ScrollbackCapacity);
    }

    await using var terminal = terminalBuilder.Build();
    await terminal.RunAsync();

    var builder = new StringBuilder();
    builder.Append(DescribeScrollHistoryReflowState(terminal, "after script"));

    if (scene.ResizeSteps is { Count: > 0 } steps)
    {
        foreach (var (width, height) in steps)
        {
            terminal.Resize(width, height);
            builder.Append("; ");
            builder.Append(DescribeScrollHistoryReflowState(terminal, $"after resize to {width}x{height}"));
        }
    }

    return builder.ToString();
}

static string DescribeScrollHistoryReflowState(Hex1bTerminal terminal, string label)
{
    var builder = new StringBuilder();
    builder.Append(label);
    builder.Append(": ");
    builder.Append($"{terminal.ScrollbackCount} scrollback line(s), {terminal.SixelPlacementCount} active placement(s), {terminal.TrackedSixelCount} tracked image(s)");

    foreach (var placement in terminal.SixelPlacements)
    {
        builder.Append(
            $"; ({placement.Column},{placement.Row}) declared {placement.WidthInCells}x{placement.HeightInCells} cells, painted {placement.PaintedColumnCount}x{placement.PaintedRowCount}");
    }

    // The declared/painted geometry above never changes just because the
    // viewport got smaller (see SixelGraphicsState.ClipActivePlacementsToViewport):
    // only what the *current* viewport can actually observe narrows. Walking a
    // fresh snapshot's placements the same way SixelTestTerminal.Observe does in
    // the test suite makes that distinction visible here too.
    using var snapshot = terminal.CreateSnapshot(scrollbackLines: terminal.ScrollbackCount);
    builder.Append($" [snapshot: {snapshot.SixelPlacements.Count} placement(s), scrollbackLineCount={snapshot.ScrollbackLineCount}]");
    var mainScreenRows = new SortedSet<int>();
    var mainScreenColumns = new SortedSet<int>();
    foreach (var placement in snapshot.SixelPlacements)
    {
        if (!placement.HasPaintedExtent)
            continue;

        var top = Math.Max(placement.PaintedTop, snapshot.ScrollbackLineCount);
        var bottom = Math.Min(placement.PaintedBottom, snapshot.Height - 1);
        var left = Math.Max(placement.PaintedLeft, 0);
        var right = Math.Min(placement.PaintedRight, snapshot.Width - 1);
        for (var row = top; row <= bottom; row++)
        {
            for (var column = left; column <= right; column++)
            {
                if (!placement.CoversCell(row, column))
                    continue;

                mainScreenRows.Add(row - snapshot.ScrollbackLineCount);
                mainScreenColumns.Add(column);
            }
        }
    }

    if (mainScreenRows.Count > 0)
        builder.Append($"; viewport rows observed: {string.Join(",", mainScreenRows)}");
    if (mainScreenColumns.Count > 0)
        builder.Append($"; viewport columns observed: {string.Join(",", mainScreenColumns)}");

    // Placements that live purely in history (their painted top already
    // scrolled past the visible viewport) are not walked above, since the
    // live-viewport loop only reports what a real presentation adapter could
    // draw. Reporting their own origin-cell coverage here is the direct,
    // authoritative evidence that #453 destructive damage survives the
    // history/snapshot projection (SixelPlacement.SliceHistoryRows), not just
    // that the row count matches.
    foreach (var placement in snapshot.SixelPlacements)
    {
        if (!placement.HasPaintedExtent || placement.PaintedTop >= snapshot.ScrollbackLineCount)
            continue;

        var originCovered = placement.CoversCell(placement.PaintedTop, placement.PaintedLeft);
        builder.Append(
            $"; history placement ({placement.PaintedLeft},{placement.PaintedTop}) painted {placement.PaintedColumnCount}x{placement.PaintedRowCount}, origin cell covered: {originCovered}");
    }

    return builder.ToString();
}

static async Task<string> InspectModelAsync(RawSixelFixture fixture)
{
    var capabilities = new TerminalCapabilities
    {
        SupportsSixel = true,
        SupportsTrueColor = true,
        Supports256Colors = true,
        CellPixelWidth = 1,
        CellPixelHeight = 1,
    };
    var chunks = new List<byte[]>();
    if (fixture.SetupDcsBytes is { } setup)
    {
        chunks.Add(setup);
        chunks.Add(Encoding.ASCII.GetBytes("\r\n"));
    }

    chunks.Add(fixture.StandardDcsBytes);
    var workload = new DemoWorkloadAdapter(chunks);
    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithWorkload(workload)
        .WithPresentation(new HeadlessPresentationAdapter(80, 24, capabilities))
        .WithDimensions(80, 24)
        .Build();
    await terminal.RunAsync();

    using var snapshot = terminal.CreateSnapshot();
    SixelData? inspected = null;
    foreach (var placement in snapshot.SixelPlacements)
    {
        if (placement.Image.Payload == fixture.Payload)
        {
            inspected = placement.Image;
            break;
        }
    }

    if (inspected is null)
    {
        return "no placement";
    }

    return DescribeModel(inspected);
}

static string DescribeModel(SixelData sixel)
{
    var builder = new StringBuilder();
    builder.Append(sixel.PixelWidth > 0 && sixel.PixelHeight > 0
        ? $"declared {sixel.PixelWidth}x{sixel.PixelHeight}px"
        : "no declared extent");

    // The inspection terminal uses 1x1 cell metrics, so the occupied cell span is
    // the aspect-scaled rendered extent in pixels.
    builder.Append($", rendered {sixel.WidthInCells}x{sixel.HeightInCells}px");

    var raster = sixel.GetPixels();
    if (raster is null)
    {
        builder.Append(", raster geometry-only (no pixels allocated)");
        return builder.ToString();
    }

    builder.Append($", logical raster {raster.Width}x{raster.Height}");
    builder.Append($", top-left {Describe(raster[0, 0])}");
    builder.Append($", bottom-right {Describe(raster[raster.Width - 1, raster.Height - 1])}");
    var distinct = CountDistinctColors(raster);
    builder.Append($", {distinct} distinct color{(distinct == 1 ? "" : "s")}");
    return builder.ToString();

    static string Describe(Hex1b.Surfaces.Rgba32 pixel) =>
        pixel.A == 0
            ? "transparent"
            : $"#{pixel.R:X2}{pixel.G:X2}{pixel.B:X2}";

    static int CountDistinctColors(Hex1b.Surfaces.SixelPixelBuffer raster)
    {
        var seen = new HashSet<uint>();
        for (var y = 0; y < raster.Height; y++)
        {
            for (var x = 0; x < raster.Width; x++)
            {
                var pixel = raster[x, y];
                seen.Add(((uint)pixel.R << 24) | ((uint)pixel.G << 16) | ((uint)pixel.B << 8) | pixel.A);
            }
        }

        return seen.Count;
    }
}

// #456: snapshot model, SVG/HTML export, and HMP1 recording/replay evidence.
// Each RawSnapshotExportReplayScene.Kind needs materially different
// follow-up work beyond just replaying the script, so this dispatches to one
// focused inspector per kind rather than one function trying to do all of it.
static async Task<string> InspectSnapshotExportReplaySceneAsync(RawSnapshotExportReplayScene scene) =>
    scene.Kind switch
    {
        SnapshotExportReplayScenarioKind.SnapshotSharing => await InspectSnapshotSharingAsync(scene),
        SnapshotExportReplayScenarioKind.Projections => await InspectProjectionsAsync(scene),
        SnapshotExportReplayScenarioKind.GeometryOnlyExport => await InspectGeometryOnlyExportAsync(scene),
        SnapshotExportReplayScenarioKind.DeterministicExport => await InspectDeterministicExportAsync(scene),
        SnapshotExportReplayScenarioKind.RecordReplayWithDamage => await InspectRecordReplayWithDamageAsync(scene),
        SnapshotExportReplayScenarioKind.MainAlternateIndependence => await InspectMainAlternateIndependenceAsync(scene),
        SnapshotExportReplayScenarioKind.MalformedFailures => await InspectMalformedFailuresAsync(scene),
        _ => throw new ArgumentOutOfRangeException(nameof(scene)),
    };

static Hex1bTerminalBuilder CreateSnapshotExportReplayTerminalBuilder(byte[] script, int scrollbackCapacity)
{
    var capabilities = new TerminalCapabilities
    {
        SupportsSixel = true,
        SupportsTrueColor = true,
        Supports256Colors = true,
        CellPixelWidth = 10,
        CellPixelHeight = 20,
    };
    var builder = Hex1bTerminal.CreateBuilder()
        .WithWorkload(new DemoWorkloadAdapter([script]))
        .WithPresentation(new HeadlessPresentationAdapter(80, 24, capabilities))
        .WithDimensions(80, 24);
    return scrollbackCapacity > 0 ? builder.WithScrollback(scrollbackCapacity) : builder;
}

static async Task<string> InspectSnapshotSharingAsync(RawSnapshotExportReplayScene scene)
{
    await using var terminal = CreateSnapshotExportReplayTerminalBuilder(scene.Bytes, scene.ScrollbackCapacity).Build();
    await terminal.RunAsync();

    // Not `using`, since the double-Dispose() below is the point of this
    // scene: it must be safe and must not affect snapshotB's independent
    // lifetime, matching SixelSnapshotSharingTests' disposal contract.
    var snapshotA = terminal.CreateSnapshot();
    using var snapshotB = terminal.CreateSnapshot();

    var placementsA = snapshotA.SixelPlacements;
    var placementsB = snapshotB.SixelPlacements;
    var sharedWithinSnapshot = placementsA.Count >= 2 && ReferenceEquals(placementsA[0].Image, placementsA[1].Image);
    var sharedAcrossSnapshots = placementsA.Count > 0 && placementsB.Count > 0
        && ReferenceEquals(placementsA[0].Image, placementsB[0].Image);

    snapshotA.Dispose();
    snapshotA.Dispose();

    var builder = new StringBuilder();
    builder.Append($"{placementsA.Count} placement(s); both anchors reference the same image instance: {sharedWithinSnapshot}");
    builder.Append($"; a second independent snapshot observes that identical shared instance: {sharedAcrossSnapshots}");
    builder.Append($"; snapshot B still resolves pixels after snapshot A was disposed twice: {snapshotB.ContainsSixelData()}");
    return builder.ToString();
}

static async Task<string> InspectProjectionsAsync(RawSnapshotExportReplayScene scene)
{
    await using var terminal = CreateSnapshotExportReplayTerminalBuilder(scene.Bytes, scene.ScrollbackCapacity).Build();
    await terminal.RunAsync();

    using var viewportOnly = terminal.CreateSnapshot(scrollbackLines: 0);
    using var historyInclusive = terminal.CreateSnapshot(scrollbackLines: terminal.ScrollbackCount);
    using var currentWidth = terminal.CreateSnapshot(scrollbackLines: terminal.ScrollbackCount, scrollbackWidth: ScrollbackWidth.CurrentTerminal);
    using var originalWidth = terminal.CreateSnapshot(scrollbackLines: terminal.ScrollbackCount, scrollbackWidth: ScrollbackWidth.Original);

    var builder = new StringBuilder();
    builder.Append($"viewport-only: {viewportOnly.SixelPlacements.Count} placement(s), scrollbackLineCount={viewportOnly.ScrollbackLineCount}");
    builder.Append($"; history-inclusive: {historyInclusive.SixelPlacements.Count} placement(s), scrollbackLineCount={historyInclusive.ScrollbackLineCount}");
    builder.Append($"; current-width projection: {currentWidth.SixelPlacements.Count} placement(s)");
    builder.Append($"; original-width projection: {originalWidth.SixelPlacements.Count} placement(s)");
    return builder.ToString();
}

static async Task<string> InspectGeometryOnlyExportAsync(RawSnapshotExportReplayScene scene)
{
    await using var terminal = CreateSnapshotExportReplayTerminalBuilder(scene.Bytes, scene.ScrollbackCapacity).Build();
    await terminal.RunAsync();

    using var snapshot = terminal.CreateSnapshot();
    var placement = snapshot.SixelPlacements.Count > 0 ? snapshot.SixelPlacements[0] : null;
    var svg = snapshot.ToSvg();
    var html = snapshot.ToHtml();

    var builder = new StringBuilder();
    builder.Append($"placement geometry-only: {placement?.IsGeometryOnly ?? false}");
    builder.Append($"; SVG diagnostic placeholder present ('sixel-geometry-only'): {svg.Contains("sixel-geometry-only", StringComparison.Ordinal)}");
    builder.Append($"; HTML geometry-only metadata present ('\"geometryOnly\":true'): {html.Contains("\"geometryOnly\":true", StringComparison.Ordinal)}");
    return builder.ToString();
}

static async Task<string> InspectDeterministicExportAsync(RawSnapshotExportReplayScene scene)
{
    await using var terminal = CreateSnapshotExportReplayTerminalBuilder(scene.Bytes, scene.ScrollbackCapacity).Build();
    await terminal.RunAsync();

    using var snapshot = terminal.CreateSnapshot();
    var svgFirst = snapshot.ToSvg();
    var svgSecond = snapshot.ToSvg();
    var htmlFirst = snapshot.ToHtml();
    var htmlSecond = snapshot.ToHtml();

    var builder = new StringBuilder();
    builder.Append($"SVG export byte-identical across repeats: {string.Equals(svgFirst, svgSecond, StringComparison.Ordinal)}");
    builder.Append($"; HTML export byte-identical across repeats: {string.Equals(htmlFirst, htmlSecond, StringComparison.Ordinal)}");
    builder.Append($"; SVG SHA-256 (first 16 hex chars): {Sha256Hex(svgFirst)}");
    return builder.ToString();
}

static async Task<string> InspectRecordReplayWithDamageAsync(RawSnapshotExportReplayScene scene)
{
    await using var terminal = CreateSnapshotExportReplayTerminalBuilder(scene.Bytes, scene.ScrollbackCapacity).Build();
    await terminal.RunAsync();

    using var snapshot = terminal.CreateSnapshot(scrollbackLines: terminal.ScrollbackCount);
    var recorded = Hmp1SixelRecording.Serialize(snapshot.SixelPlacements);
    var deserialized = Hmp1SixelRecording.Deserialize(recorded);
    var replayScript = deserialized.BuildReplayEscapeSequence();

    await using var viewer = CreateSnapshotExportReplayTerminalBuilder(
        Encoding.ASCII.GetBytes(replayScript), scene.ScrollbackCapacity).Build();
    await viewer.RunAsync();

    using var replayed = viewer.CreateSnapshot(scrollbackLines: viewer.ScrollbackCount);

    var builder = new StringBuilder();
    builder.Append($"original: {snapshot.SixelPlacements.Count} placement(s), {recorded.Length} recorded byte(s)");
    builder.Append($"; replayed: {replayed.SixelPlacements.Count} placement(s)");

    var pixelsMatch = snapshot.SixelPlacements.Count == replayed.SixelPlacements.Count;
    var damageMatches = pixelsMatch;
    for (var index = 0; pixelsMatch && index < snapshot.SixelPlacements.Count; index++)
    {
        var original = snapshot.SixelPlacements[index];
        var replayedPlacement = replayed.SixelPlacements[index];
        if (!PixelsEqual(original.Image.GetPixels(), replayedPlacement.Image.GetPixels()))
        {
            pixelsMatch = false;
        }

        if (original.PaintedRowCount != replayedPlacement.PaintedRowCount
            || original.PaintedColumnCount != replayedPlacement.PaintedColumnCount)
        {
            damageMatches = false;
        }
    }

    builder.Append($"; surviving pixels match after record/replay: {pixelsMatch}");
    builder.Append($"; painted (post-damage) extent matches after record/replay: {damageMatches}");
    return builder.ToString();
}

static async Task<string> InspectMainAlternateIndependenceAsync(RawSnapshotExportReplayScene scene)
{
    if (scene.Checkpoints is not { Count: > 0 } checkpoints)
    {
        throw new InvalidOperationException($"'{scene.Name}' requires at least one checkpoint.");
    }

    var labels = new[] { "main only", "alternate active", "back to main" };
    var builder = new StringBuilder();
    for (var index = 0; index < checkpoints.Count; index++)
    {
        var script = Encoding.ASCII.GetBytes(checkpoints[index]);
        await using var terminal = CreateSnapshotExportReplayTerminalBuilder(script, 0).Build();
        await terminal.RunAsync();

        using var snapshot = terminal.CreateSnapshot();
        var recorded = Hmp1SixelRecording.Serialize(snapshot.SixelPlacements);
        var deserialized = Hmp1SixelRecording.Deserialize(recorded);
        var replayScript = deserialized.BuildReplayEscapeSequence();

        await using var viewer = CreateSnapshotExportReplayTerminalBuilder(
            Encoding.ASCII.GetBytes(replayScript), 0).Build();
        await viewer.RunAsync();
        using var replayed = viewer.CreateSnapshot();

        var pixelsMatch = snapshot.SixelPlacements.Count == replayed.SixelPlacements.Count
            && snapshot.SixelPlacements.Count > 0
            && PixelsEqual(snapshot.SixelPlacements[0].Image.GetPixels(), replayed.SixelPlacements[0].Image.GetPixels());

        if (index > 0)
        {
            builder.Append("; ");
        }

        var label = index < labels.Length ? labels[index] : $"checkpoint {index}";
        builder.Append(
            $"{label}: in alternate screen={snapshot.InAlternateScreen}, {snapshot.SixelPlacements.Count} placement(s) recorded, replay pixel match={pixelsMatch}");
    }

    return builder.ToString();
}

static async Task<string> InspectMalformedFailuresAsync(RawSnapshotExportReplayScene scene)
{
    await using var terminal = CreateSnapshotExportReplayTerminalBuilder(scene.Bytes, scene.ScrollbackCapacity).Build();
    await terminal.RunAsync();

    using var snapshot = terminal.CreateSnapshot();
    var baseline = Hmp1SixelRecording.Serialize(snapshot.SixelPlacements);

    var wrongMagic = new byte[16];
    Encoding.ASCII.GetBytes("XXXX").CopyTo(wrongMagic, 0);

    var truncated = baseline[..^8];

    using var versionStream = new MemoryStream();
    using (var writer = new BinaryWriter(versionStream, Encoding.UTF8, leaveOpen: true))
    {
        writer.Write("SXRC"u8.ToArray());
        writer.Write(Hmp1SixelRecording.CurrentVersion + 1);
        writer.Write(0); // placementCount
        writer.Write(0); // imageCount
    }

    var builder = new StringBuilder();
    builder.Append($"baseline recording: {baseline.Length} byte(s)");
    builder.Append($"; wrong magic marker -> {DescribeRecordingFailure(wrongMagic)}");
    builder.Append($"; truncated mid-record -> {DescribeRecordingFailure(truncated)}");
    builder.Append($"; unsupported version -> {DescribeRecordingFailure(versionStream.ToArray())}");
    return builder.ToString();
}

static string DescribeRecordingFailure(byte[] data)
{
    try
    {
        Hmp1SixelRecording.Deserialize(data);
        return "unexpectedly succeeded";
    }
    catch (Hmp1SixelRecordingException ex)
    {
        return ex.Reason.ToString();
    }
}

static bool PixelsEqual(Hex1b.Surfaces.SixelPixelBuffer? a, Hex1b.Surfaces.SixelPixelBuffer? b)
{
    if (a is null || b is null)
    {
        return a is null && b is null;
    }

    if (a.Width != b.Width || a.Height != b.Height)
    {
        return false;
    }

    for (var y = 0; y < a.Height; y++)
    {
        for (var x = 0; x < a.Width; x++)
        {
            if (!a[x, y].Equals(b[x, y]))
            {
                return false;
            }
        }
    }

    return true;
}

static string Sha256Hex(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
