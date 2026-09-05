using Hex1b;

// KgpCloudDemo drives the same haze of mutually attracting motes as SixelCloudDemo,
// on the same physics and the same palette, but paints it with KGP instead of Sixel.
//
// The point of running the two side by side is the shape of the traffic. Sixel has no
// reusable image, so every mote's pixels are re-encoded and re-sent on every frame.
// KGP splits the image from the placement: a dozen 3x3 sprites are transmitted once,
// and a frame after that is a delete plus a few hundred placements carrying no pixel
// data at all.
//
// Every escape sequence is hand-authored raw APC. Nothing here goes through
// KgpImageWidget or Hex1bRenderContext, so what reaches the terminal is independent of
// the code the demo is meant to exercise.

var moteCount = ReadIntOption(args, "--motes") ?? 700;
var frameMilliseconds = ReadIntOption(args, "--frame-ms") ?? 33;
var maxFrames = ReadIntOption(args, "--frames");
var seed = ReadIntOption(args, "--seed") ?? 20260218;
var scaleCells = Math.Max(0, ReadIntOption(args, "--scale") ?? 0);
var headless = args.Contains("--headless");

if (headless)
{
    // Headless mode reports the byte accounting so the demo can be exercised in CI,
    // where there is no KGP-capable console to render into.
    WriteHeadlessTranscript(moteCount, frameMilliseconds, maxFrames ?? 24, seed, scaleCells);
    return;
}

var presentation = new ConsolePresentationAdapter(enableMouse: true);
var capabilities = presentation.Capabilities;

var workload = new CloudWorkloadAdapter(
    moteCount,
    capabilities.CellPixelWidth,
    capabilities.CellPixelHeight,
    TimeSpan.FromMilliseconds(frameMilliseconds),
    maxFrames,
    seed,
    (cellWidth, cellHeight) => new KgpCloudRenderer(cellWidth, cellHeight, scaleCells));

Console.Error.WriteLine("KgpCloudDemo fills the terminal with a drifting cloud of raw-APC KGP placements.");
Console.Error.WriteLine("Requires a terminal with kitty graphics support (kitty, WezTerm, Ghostty, Konsole).");
Console.Error.WriteLine("Move the mouse to gather the cloud; press q or Escape to quit.");

// No WithDimensions: the console presentation reports the live terminal size, so the
// cloud fills whatever space is actually available and follows resizes.
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithWorkload(workload)
    .WithPresentation(presentation)
    .Build();

await terminal.RunAsync();

static void WriteHeadlessTranscript(int moteCount, int frameMilliseconds, int frames, int seed, int scaleCells)
{
    const int Columns = 80;
    const int Rows = 24;
    const int CellPixelWidth = 10;
    const int CellPixelHeight = 20;

    Console.WriteLine("Hex1b KgpCloudDemo (headless)");
    Console.WriteLine($"  motes:      {moteCount}");
    Console.WriteLine($"  frames:     {frames}");
    Console.WriteLine($"  frame time: {frameMilliseconds}ms");
    Console.WriteLine($"  mote size:  {(scaleCells > 0 ? $"{scaleCells}x{scaleCells} cells (scaled)" : "3x3 pixels (native)")}");
    Console.WriteLine($"  viewport:   {Columns}x{Rows} cells ({CellPixelWidth}x{CellPixelHeight} px per cell)");
    Console.WriteLine();

    var cloud = new DustCloud(seed);
    cloud.Reset(
        Columns * CellPixelWidth,
        Rows * CellPixelHeight,
        moteCount);

    var renderer = new KgpCloudRenderer(CellPixelWidth, CellPixelHeight, scaleCells);
    var frameSeconds = frameMilliseconds / 1000.0;
    var totalBytes = 0L;

    Console.WriteLine("Frame  Bytes  Placements emitted");
    for (var frame = 1; frame <= frames; frame++)
    {
        cloud.Advance(frameSeconds);
        var bytes = renderer.RenderFrame(cloud, Columns, Rows);
        totalBytes += bytes.Length;

        // Counting the APC introducers in the emitted bytes keeps the transcript
        // independent of Hex1b's own parser, matching the rule that this demo's
        // evidence never round-trips through the code it exercises.
        var placements = CountPlacements(bytes);
        Console.WriteLine($"{frame,5}  {bytes.Length,5}  {placements,5}");
    }

    var spriteBytes = renderer.PaletteTransmissionBytes;
    Console.WriteLine();
    Console.WriteLine($"Sprites transmitted once, inside frame 1: {spriteBytes} bytes total.");
    Console.WriteLine($"Every later frame is placements only: no pixel data is ever resent.");
    Console.WriteLine($"Pixel data is {spriteBytes * 100.0 / totalBytes:F2}% of the {totalBytes} bytes written.");
}

/// <summary>
/// Counts the KGP placements a frame emits by scanning for <c>a=p</c> APC introducers.
/// </summary>
static int CountPlacements(byte[] frameBytes)
{
    ReadOnlySpan<byte> introducer = "\x1b_Ga=p"u8;

    var count = 0;
    for (var index = 0; index <= frameBytes.Length - introducer.Length; index++)
    {
        if (frameBytes.AsSpan(index, introducer.Length).SequenceEqual(introducer))
            count++;
    }

    return count;
}

static int? ReadIntOption(string[] args, string name)
{
    for (var index = 0; index < args.Length - 1; index++)
    {
        if (string.Equals(args[index], name, StringComparison.Ordinal)
            && int.TryParse(args[index + 1], out var value))
        {
            return value;
        }
    }

    return null;
}
