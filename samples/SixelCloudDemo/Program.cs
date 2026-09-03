using Hex1b;

// SixelCloudDemo drives a haze of Sixel motes that drift back toward the centre of
// the screen, exercising Hex1b's Sixel placement handling under a continuous storm of
// small, overlapping, independently positioned placements.
//
// Every escape sequence is hand-authored raw DCS. Nothing here goes through
// SixelWidget or SixelEncoder, so what reaches the terminal is independent of the
// code the demo is meant to exercise.

var moteCount = ReadIntOption(args, "--motes") ?? 260;
var frameMilliseconds = ReadIntOption(args, "--frame-ms") ?? 33;
var maxFrames = ReadIntOption(args, "--frames");
var seed = ReadIntOption(args, "--seed") ?? 20260218;
var headless = args.Contains("--headless");
var useRaster = args.Contains("--raster");

if (headless)
{
    // Headless mode paints into an in-memory terminal so the demo can be exercised
    // in CI, where there is no Sixel-capable console to render into.
    WriteHeadlessTranscript(moteCount, frameMilliseconds, maxFrames ?? 24, seed, useRaster);
    return;
}

var presentation = new ConsolePresentationAdapter(enableMouse: true);
var capabilities = presentation.Capabilities;

var workload = new SixelCloudWorkloadAdapter(
    moteCount,
    capabilities.CellPixelWidth,
    capabilities.CellPixelHeight,
    TimeSpan.FromMilliseconds(frameMilliseconds),
    maxFrames,
    seed,
    useRaster);

Console.Error.WriteLine("SixelCloudDemo fills the terminal with a drifting cloud of raw-DCS Sixel motes.");
Console.Error.WriteLine("Move the mouse to bend the orbits; press q or Escape to quit.");

// No WithDimensions: the console presentation reports the live terminal size, so the
// cloud fills whatever space is actually available and follows resizes.
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithWorkload(workload)
    .WithPresentation(presentation)
    .Build();

await terminal.RunAsync();

static void WriteHeadlessTranscript(int moteCount, int frameMilliseconds, int frames, int seed, bool useRaster)
{
    const int Columns = 80;
    const int Rows = 24;
    const int CellPixelWidth = 10;
    const int CellPixelHeight = 20;

    Console.WriteLine("Hex1b SixelCloudDemo (headless)");
    Console.WriteLine($"  motes:      {moteCount}");
    Console.WriteLine($"  frames:     {frames}");
    Console.WriteLine($"  frame time: {frameMilliseconds}ms");
    Console.WriteLine($"  viewport:   {Columns}x{Rows} cells ({CellPixelWidth}x{CellPixelHeight} px per cell)");
    Console.WriteLine();

    var cloud = new DustCloud(seed);
    cloud.Reset(
        Columns * CellPixelWidth,
        Rows * CellPixelHeight,
        moteCount);

    var renderer = new SixelCloudRenderer(CellPixelWidth, CellPixelHeight, useRaster);
    var frameSeconds = frameMilliseconds / 1000.0;

    Console.WriteLine("Frame  Bytes  Placements tracked after frame");
    for (var frame = 1; frame <= frames; frame++)
    {
        cloud.Advance(frameSeconds);
        var bytes = renderer.RenderFrame(cloud, Columns, Rows);

        // A fresh terminal per frame reports the placements a single frame leaves
        // behind, which is the number a managed presentation would have to carry.
        var placements = CountPlacements(bytes, Columns, Rows);
        Console.WriteLine($"{frame,5}  {bytes.Length,5}  {placements,5}");
    }

    Console.WriteLine();
    Console.WriteLine(useRaster
        ? "Raster mode: each frame issues ED and then one full-viewport DCS raster, so\nplacement counts stay bounded at one but the image is expensive to decode."
        : "Placement mode: each frame issues ED and then one small cursor-positioned DCS\nper visible mote, so placement counts track the cloud and reset every frame.");
}

static int CountPlacements(byte[] frameBytes, int columns, int rows)
{
    using var terminal = Hex1bTerminal.CreateBuilder()
        .WithWorkload(new ReplayWorkloadAdapter(frameBytes))
        .WithPresentation(new HeadlessPresentationAdapter(columns, rows))
        .WithDimensions(columns, rows)
        .Build();

    terminal.RunAsync().GetAwaiter().GetResult();

    return terminal.CreateSnapshot().SixelPlacements.Count;
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
