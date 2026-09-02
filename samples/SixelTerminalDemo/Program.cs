using System.Text;
using Hex1b;

var headless = args.Contains("--headless", StringComparer.OrdinalIgnoreCase);
var sceneFilter = GetOption(args, "--scene");
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

var chunks = BuildDemoOutput(
    fixtures,
    modelDescriptions,
    cursorScenes,
    includeTransportScenes: sceneFilter is null);
var workload = new DemoWorkloadAdapter(chunks);
IHex1bTerminalPresentationAdapter presentation = headless
    ? new HeadlessPresentationAdapter(80, 24, capabilities)
    : new ConsolePresentationAdapter(enableMouse: false);

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithWorkload(workload)
    .WithPresentation(presentation)
    .WithDimensions(80, 24)
    .Build();

if (!headless)
{
    Console.Error.WriteLine(
        "SixelTerminalDemo sends standard ESC-framed, independently authored fixtures through Hex1bTerminal.");
    Console.Error.WriteLine("The labels describe the DEC VT340 model outcome expected from a native Sixel terminal.");
    Console.Error.WriteLine("Use --scene <name> to run one enlarged fixture, for example --scene \"Declared extent\".");
}

await terminal.RunAsync();

if (headless)
{
    using var snapshot = terminal.CreateSnapshot();
    Console.WriteLine("Hex1b authoritative Sixel model inspection:");
    Console.WriteLine($"  tracked Sixel origins: {CountSixelOrigins(snapshot)}");
    Console.WriteLine($"  cursor: ({snapshot.CursorX}, {snapshot.CursorY})");
    Console.WriteLine($"  cell metrics: {snapshot.CellPixelWidth}x{snapshot.CellPixelHeight}px");
    Console.WriteLine();
    Console.WriteLine("Deterministic grammar and geometry scenes:");
    for (var index = 0; index < fixtures.Count; index++)
    {
        Console.WriteLine($"  {fixtures[index].Name}: {modelDescriptions[index]}");
    }
    Console.WriteLine();
    Console.WriteLine("Cursor, DECSDM, and margin scenes:");
    for (var index = 0; index < cursorScenes.Count; index++)
    {
        Console.WriteLine($"  {cursorScenes[index].Name}: {cursorObservations[index]}");
    }
    Console.WriteLine("Run without --headless in a native Sixel terminal to inspect the presentation outcome.");
}

static IReadOnlyList<byte[]> BuildDemoOutput(
    IReadOnlyList<RawSixelFixture> fixtures,
    IReadOnlyList<string> modelDescriptions,
    IReadOnlyList<RawCursorScene> cursorScenes,
    bool includeTransportScenes)
{
    var chunks = new List<byte[]>
    {
        Encoding.ASCII.GetBytes("\x1b[2J\x1b[HHex1b terminal-first Sixel behavior demo\r\n"),
        Encoding.ASCII.GetBytes("Fixtures use standard ESC P ... ESC \\\\ framing; no SixelWidget or encoder.\r\n\r\n"),
    };

    for (var index = 0; index < fixtures.Count; index++)
    {
        var fixture = fixtures[index];
        chunks.Add(Encoding.ASCII.GetBytes($"[{fixture.Name}] Expected: {fixture.Expected}\r\n"));
        chunks.Add(Encoding.ASCII.GetBytes($"Model: {modelDescriptions[index]}\r\n"));
        if (fixture.SetupDcsBytes is { } setup)
        {
            chunks.Add(Encoding.ASCII.GetBytes(
                $"Setup: {fixture.SetupPayload}\r\n"));
            chunks.Add(setup);
            chunks.Add(Encoding.ASCII.GetBytes("\r\n"));
        }

        chunks.Add(fixture.StandardDcsBytes);
        chunks.Add(Encoding.ASCII.GetBytes("\r\n\r\n"));
    }

    AddCursorScenes(chunks, cursorScenes);

    if (!includeTransportScenes)
    {
        chunks.Add(Encoding.ASCII.GetBytes(
            "Stage 5: the same parser and rasterizer drive cursor, DECSDM, margin, and metric semantics.\r\n"));
        return chunks;
    }

    var framingFixture = fixtures[0].StandardDcsBytes;
    chunks.Add(Encoding.ASCII.GetBytes(
        "[Framing] Two consecutive DCS images with no transport boundary between them.\r\n"));
    chunks.Add(
    [
        .. fixtures[0].StandardDcsBytes,
        .. fixtures[1].StandardDcsBytes,
    ]);
    chunks.Add(Encoding.ASCII.GetBytes("\r\n\r\n"));

    chunks.Add(Encoding.ASCII.GetBytes(
        "[Split write] The introducer, payload, and ESC-backslash terminator arrive in separate reads.\r\n"));
    AddChunks(chunks, framingFixture, [1, 1, 5, framingFixture.Length - 9, 1, 1]);
    chunks.Add(Encoding.ASCII.GetBytes("\r\n\r\n"));

    chunks.Add(Encoding.ASCII.GetBytes(
        "[Native passthrough] One-byte workload reads still form the original image upstream.\r\n"));
    foreach (var value in fixtures[3].StandardDcsBytes)
        chunks.Add([value]);
    chunks.Add(Encoding.ASCII.GetBytes("\r\n\r\n"));

    chunks.Add(Encoding.ASCII.GetBytes(
        "Stage 5: the same parser and rasterizer drive cursor, DECSDM, margin, and metric semantics.\r\n"));
    return chunks;
}

static void AddCursorScenes(List<byte[]> chunks, IReadOnlyList<RawCursorScene> scenes)
{
    foreach (var scene in scenes)
    {
        // Each scene owns a clean screen so margins and the scrolling region can
        // be observed without the previous scene's output interfering.
        chunks.Add(Encoding.ASCII.GetBytes(
            $"{RawCursorScene.ResetSequence}\x1b[2J\x1b[H[{scene.Name}] Expected: {scene.Expected}\r\n"));
        chunks.Add(scene.Bytes);
        chunks.Add(Encoding.ASCII.GetBytes("\x1b[24;1H"));
    }
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

static void AddChunks(List<byte[]> chunks, byte[] bytes, IReadOnlyList<int> sizes)
{
    var offset = 0;
    foreach (var size in sizes)
    {
        chunks.Add(bytes.AsSpan(offset, size).ToArray());
        offset += size;
    }

    if (offset != bytes.Length)
    {
        throw new InvalidOperationException("Demo split sizes must consume the complete DCS.");
    }
}

static int CountSixelOrigins(Hex1b.Automation.Hex1bTerminalSnapshot snapshot)
{
    var count = 0;
    for (var y = 0; y < snapshot.Height; y++)
    {
        for (var x = 0; x < snapshot.Width; x++)
        {
            if (snapshot.GetCell(x, y).SixelData is not null)
                count++;
        }
    }
    return count;
}

internal sealed class DemoWorkloadAdapter(IReadOnlyList<byte[]> chunks) : IHex1bTerminalWorkloadAdapter
{
    private readonly object _eventLock = new();
    private Action? _disconnected;
    private int _nextChunk;
    private bool _completed;

    public event Action? Disconnected
    {
        add
        {
            var invokeNow = false;
            lock (_eventLock)
            {
                _disconnected += value;
                invokeNow = _completed;
            }
            if (invokeNow)
                value?.Invoke();
        }
        remove
        {
            lock (_eventLock)
                _disconnected -= value;
        }
    }

    public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
    {
        if (_nextChunk < chunks.Count)
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(chunks[_nextChunk++]);

        Action? disconnected;
        lock (_eventLock)
        {
            _completed = true;
            disconnected = _disconnected;
        }
        disconnected?.Invoke();
        return ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
    }

    public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        => ValueTask.CompletedTask;

    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
        => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
