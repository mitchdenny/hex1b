using System.Text;
using Hex1b;

var headless = args.Contains("--headless", StringComparer.OrdinalIgnoreCase);
var capabilities = new TerminalCapabilities
{
    SupportsSixel = true,
    SupportsTrueColor = true,
    Supports256Colors = true,
    CellPixelWidth = 10,
    CellPixelHeight = 20,
};

var chunks = BuildDemoOutput();
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
}

await terminal.RunAsync();

if (headless)
{
    using var snapshot = terminal.CreateSnapshot();
    Console.WriteLine("Hex1b model inspection (current implementation):");
    Console.WriteLine($"  tracked Sixel origins: {CountSixelOrigins(snapshot)}");
    Console.WriteLine($"  cursor: ({snapshot.CursorX}, {snapshot.CursorY})");
    Console.WriteLine($"  cell metrics: {snapshot.CellPixelWidth}x{snapshot.CellPixelHeight}px");
    Console.WriteLine("Run without --headless in a native Sixel terminal to inspect the presentation outcome.");
}

static IReadOnlyList<byte[]> BuildDemoOutput()
{
    var chunks = new List<byte[]>
    {
        Encoding.ASCII.GetBytes("\x1b[2J\x1b[HHex1b terminal-first Sixel behavior demo\r\n"),
        Encoding.ASCII.GetBytes("Fixtures use standard ESC P ... ESC \\\\ framing; no SixelWidget or encoder.\r\n\r\n"),
    };

    foreach (var fixture in RawSixelFixtures.All)
    {
        chunks.Add(Encoding.ASCII.GetBytes($"[{fixture.Name}] Expected: {fixture.Expected}\r\n"));
        chunks.Add(fixture.StandardDcsBytes);
        chunks.Add(Encoding.ASCII.GetBytes("\r\n\r\n"));
    }

    var framingFixture = RawSixelFixtures.All[0].StandardDcsBytes;
    chunks.Add(Encoding.ASCII.GetBytes(
        "[Framing] Two consecutive DCS images with no transport boundary between them.\r\n"));
    chunks.Add(
    [
        .. RawSixelFixtures.All[0].StandardDcsBytes,
        .. RawSixelFixtures.All[1].StandardDcsBytes,
    ]);
    chunks.Add(Encoding.ASCII.GetBytes("\r\n\r\n"));

    chunks.Add(Encoding.ASCII.GetBytes(
        "[Split write] The introducer, payload, and ESC-backslash terminator arrive in separate reads.\r\n"));
    AddChunks(chunks, framingFixture, [1, 1, 5, framingFixture.Length - 9, 1, 1]);
    chunks.Add(Encoding.ASCII.GetBytes("\r\n\r\n"));

    chunks.Add(Encoding.ASCII.GetBytes(
        "[Native passthrough] One-byte workload reads still form the original image upstream.\r\n"));
    foreach (var value in RawSixelFixtures.All[3].StandardDcsBytes)
        chunks.Add([value]);
    chunks.Add(Encoding.ASCII.GetBytes("\r\n\r\n"));

    chunks.Add(Encoding.ASCII.GetBytes(
        "Stage 2 note: framing is byte-oriented and bounded; Sixel grammar remains owned by #447.\r\n"));
    return chunks;
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
