using System.Text;
using Hex1b;

var headless = args.Contains("--headless", StringComparer.OrdinalIgnoreCase);
var sceneFilter = GetOption(args, "--scene");
var screenOption = GetOption(args, "--screen");
const int Width = 80;
const int Height = 24;

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
if (fixtures.Count == 0 && cursorScenes.Count == 0)
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

var allScreens = DemoScreens.Build(
    fixtures,
    modelDescriptions,
    cursorScenes,
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
        cursorObservations);
    return;
}

var workload = new PagedScreenWorkloadAdapter(screens, promptRow: Height);
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
    IReadOnlyList<string> cursorObservations)
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
    for (var y = 0; y < snapshot.Height; y++)
    {
        for (var x = 0; x < snapshot.Width; x++)
        {
            var cell = snapshot.GetCell(x, y);
            if (!cell.IsSixel)
                continue;

            occupiedColumns.Add(x);
            occupiedRows.Add(y);
            if (cell.SixelData is { } sixel)
            {
                origins.Add($"({x},{y}) {sixel.WidthInCells}x{sixel.HeightInCells} cells");
            }
        }
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
    for (var y = 0; y < snapshot.Height && inspected is null; y++)
    {
        for (var x = 0; x < snapshot.Width; x++)
        {
            var sixel = snapshot.GetCell(x, y).SixelData;
            if (sixel is not null && sixel.Payload == fixture.Payload)
            {
                inspected = sixel;
                break;
            }
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
