using System.Text;
using BenchmarkDotNet.Attributes;
using Hex1b.Automation;
using Hex1b.Sixel;
using Hex1b.Tokens;

namespace Hex1b.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("Sixel", "Hardening")]
public class SixelHardeningBenchmarks
{
    private readonly byte[] _plainOutput = Encoding.ASCII.GetBytes(new string('X', 4096));
    private readonly byte[] _sixelOutput = Encoding.ASCII.GetBytes(
        $"\x1bPq{new string('~', 4096)}\x1b\\");
    private readonly string _geometryPayload = $"q!{SixelCompatibilityPolicy.Default.MaximumNumericValue}~";
    private readonly string _boundedRasterPayload =
        $"q\"1;1;{SixelCompatibilityPolicy.Default.MaximumNumericValue};" +
        $"{SixelCompatibilityPolicy.Default.MaximumNumericValue}!{SixelCompatibilityPolicy.Default.MaximumNumericValue}~";
    private MemoryStream _passthrough = null!;
    private Hex1bTerminal _preparedTerminal = null!;
    private IReadOnlyList<SixelPlacement> _preparedPlacements = null!;

    [GlobalSetup]
    public void Setup()
    {
        _passthrough = new MemoryStream(_sixelOutput.Length);
        _preparedTerminal = CreateTerminal();
        _preparedTerminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1bPq#1;2;100;0;0#1!64~\x1b\\"));
        _preparedTerminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1HX"));
        using var snapshot = _preparedTerminal.CreateSnapshot();
        _preparedPlacements = snapshot.SixelPlacements.ToArray();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _preparedTerminal.Dispose();
        _passthrough.Dispose();
    }

    [Benchmark(Baseline = true)]
    public long RawPassthrough()
    {
        _passthrough.Position = 0;
        _passthrough.Write(_plainOutput);
        return _passthrough.Position;
    }

    [Benchmark]
    public long RawPassthroughWithSixelObservation()
    {
        _passthrough.Position = 0;
        _passthrough.Write(_sixelOutput);
        var parser = new DcsByteStreamParser();
        var batch = parser.Process(_sixelOutput);
        return _passthrough.Position + batch.Frames.Count;
    }

    [Benchmark]
    public int ParserGeometry() =>
        DcsByteStreamParser.ParseCompleteContent(
            Encoding.ASCII.GetBytes(_geometryPayload)).SixelResult.LogicalCanvasExtent.Width;

    [Benchmark]
    public int BoundedRasterWorstCase() =>
        (int)SixelRasterizer.Rasterize(
            SixelParser.ParsePayload(_boundedRasterPayload),
            SixelRasterEnvironment.CreateDefault()).Status;

    [Benchmark]
    public int PlacementDamageScrollResizeLifecycle()
    {
        using var terminal = CreateTerminal();
        for (var index = 0; index < 16; index++)
        {
            terminal.ApplyTokens(AnsiTokenizer.Tokenize(
                $"\x1b[{(index % 5) + 1};1H\x1bPq#{(index % 8) + 1};2;100;0;0" +
                $"#{(index % 8) + 1}!64~\x1b\\X\x1b[S"));
        }
        terminal.Resize(40, 12);
        terminal.Resize(80, 24);
        using var snapshot = terminal.CreateSnapshot();
        return snapshot.SixelPlacements.Count;
    }

    [Benchmark]
    public int SnapshotAndRecordingRoundTrip()
    {
        using var snapshot = _preparedTerminal.CreateSnapshot();
        var recording = Hmp1SixelRecording.Serialize(snapshot.SixelPlacements);
        return Hmp1SixelRecording.Deserialize(recording).Placements.Count;
    }

    [Benchmark]
    public string SnapshotSvgExport()
    {
        using var snapshot = _preparedTerminal.CreateSnapshot();
        return snapshot.ToSvg();
    }

    [Benchmark]
    public string SnapshotHtmlExport()
    {
        using var snapshot = _preparedTerminal.CreateSnapshot();
        return snapshot.ToHtml();
    }

    [Benchmark]
    public async Task<long> InternalHmp1Replay()
    {
        await using var stream = new MemoryStream();
        var result = await Hmp1SixelStateReplay.WriteAsync(
            stream,
            _preparedPlacements,
            [],
            CancellationToken.None);
        return result.PayloadBytes;
    }

    private static Hex1bTerminal CreateTerminal() =>
        Hex1bTerminal.CreateBuilder()
            .WithDimensions(80, 24)
            .WithScrollback(100)
            .WithWorkload(new NullWorkloadAdapter())
            .WithHeadless(new TerminalCapabilities { SupportsSixel = true })
            .Build();

    private sealed class NullWorkloadAdapter : IHex1bTerminalWorkloadAdapter
    {
        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);

        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
