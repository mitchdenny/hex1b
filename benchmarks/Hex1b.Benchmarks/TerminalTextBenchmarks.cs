using BenchmarkDotNet.Attributes;
using Hex1b.Reflow;
using Hex1b.Tokens;

namespace Hex1b.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("Terminal", "Text")]
public class TerminalTextBenchmarks
{
    private Hex1bTerminal _terminal = null!;
    private IReadOnlyList<AnsiToken> _output = null!;
    private Hwt1RenderProjection _projection = null!;
    private readonly TerminalCapabilities _capabilities = new();

    [GlobalSetup]
    public void Setup()
    {
        _terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(80, 24).WithScrollback(1000)
            .WithReflow(GhosttyReflowStrategy.Instance).Build();
        _output = AnsiTokenizer.Tokenize(string.Concat(Enumerable.Repeat(
            "The quick brown fox jumps over the lazy dog. 0123456789\r\n", 100)));
        _terminal.ApplyTokens(_output);
        _projection = new Hwt1RenderProjection();
    }

    [GlobalCleanup]
    public void Cleanup() => _terminal.Dispose();

    [Benchmark]
    public void PlainOutput() => _terminal.ApplyTokens(_output);

    [Benchmark]
    public void Snapshot()
    {
        using var snapshot = _terminal.CreateSnapshot();
    }

    [Benchmark]
    public void Reflow()
    {
        _terminal.Resize(60, 24);
        _terminal.Resize(80, 24);
    }

    [Benchmark]
    public int FullBrowserFrame()
    {
        using var snapshot = _terminal.CreateSnapshot();
        return _projection.Encode(snapshot, _capabilities, 0, 0, 0, forceFull: true).Length;
    }
}
