using System.Text;
using Hex1b;

var headless = args.Contains("--headless", StringComparer.OrdinalIgnoreCase);
var sceneFilter = GetOption(args, "--scene");
var fixtures = sceneFilter is null
    ? RawSixelFixtures.All
    : RawSixelFixtures.All
        .Where(fixture => fixture.Name.Contains(sceneFilter, StringComparison.OrdinalIgnoreCase))
        .ToArray();
if (fixtures.Count == 0)
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

var chunks = BuildDemoOutput(
    fixtures,
    modelDescriptions,
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
    Console.WriteLine("Run without --headless in a native Sixel terminal to inspect the presentation outcome.");
}

static IReadOnlyList<byte[]> BuildDemoOutput(
    IReadOnlyList<RawSixelFixture> fixtures,
    IReadOnlyList<string> modelDescriptions,
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
        chunks.Add(fixture.StandardDcsBytes);
        chunks.Add(Encoding.ASCII.GetBytes("\r\n\r\n"));
    }

    if (!includeTransportScenes)
    {
        chunks.Add(Encoding.ASCII.GetBytes(
            "Stage 3: one incremental parser owns Sixel grammar, geometry, palette metadata, and outcomes.\r\n"));
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
        "Stage 3: one incremental parser owns Sixel grammar, geometry, palette metadata, and outcomes.\r\n"));
    return chunks;
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
    var workload = new DemoWorkloadAdapter([fixture.StandardDcsBytes]);
    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithWorkload(workload)
        .WithPresentation(new HeadlessPresentationAdapter(80, 24, capabilities))
        .WithDimensions(80, 24)
        .Build();
    await terminal.RunAsync();

    using var snapshot = terminal.CreateSnapshot();
    for (var y = 0; y < snapshot.Height; y++)
    {
        for (var x = 0; x < snapshot.Width; x++)
        {
            var sixel = snapshot.GetCell(x, y).SixelData;
            if (sixel is null)
            {
                continue;
            }

            var raster = sixel.GetPixels();
            var rasterDescription = raster is null
                ? "metadata-only"
                : $"retained raster {raster.Width}x{raster.Height}";
            var declaredDescription = sixel.PixelWidth > 0 && sixel.PixelHeight > 0
                ? $"declared {sixel.PixelWidth}x{sixel.PixelHeight}px"
                : "no declared extent";
            return $"{declaredDescription}, logical geometry " +
                $"{sixel.WidthInCells}x{sixel.HeightInCells}px, {rasterDescription}";
        }
    }

    return "no placement";
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
