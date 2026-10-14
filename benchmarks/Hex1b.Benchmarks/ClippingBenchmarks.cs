using BenchmarkDotNet.Attributes;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;

namespace Hex1b.Benchmarks;

/// <summary>
/// Benchmarks for the clipping, ANSI-aware slicing and surface write path that every
/// rendered text line goes through. Each line is exactly 80 columns wide. The plain
/// variants clip against a rectangle that contains the line, which is the common case for
/// text blocks. The Clipped variants clip against a 40 column rectangle starting at
/// column 20, so the slicing path runs with a non-zero start and an early stop.
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("Rendering", "Clipping")]
public class ClippingBenchmarks
{
    private const int LineWidth = 80;
    private const int ClippedStart = 20;
    private const int ClippedWidth = 40;

    private string _asciiLine = null!;
    private string _graphemeLine = null!;
    private Surface _surface = null!;
    private SurfaceRenderContext _context = null!;
    private RectLayoutProvider _clip = null!;
    private RectLayoutProvider _narrowClip = null!;
    private SurfaceRenderContext _narrowContext = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Coloured ASCII line: SGR prefix, 80 printable columns, SGR reset.
        _asciiLine = "\x1b[38;2;200;120;40;48;2;20;20;20m"
            + string.Concat(Enumerable.Repeat("The quick brown fox jumps over ", 3))[..LineWidth]
            + "\x1b[0m";

        // Grapheme-heavy line: wide CJK, combining marks, emoji and a ZWJ sequence.
        // One unit is 10 columns; eight units fill the line.
        const string unit = "\u4F60\u597D" + "e\u0301" + "\U0001F600" + "\U0001F468\u200D\U0001F469\u200D\U0001F467" + "a\u0308";
        _graphemeLine = "\x1b[38;2;80;200;120m"
            + string.Concat(Enumerable.Repeat(unit, 8))
            + "\x1b[0m";

        // Fail loudly if the width model disagrees, so the baseline stays comparable.
        AssertWidth(_asciiLine);
        AssertWidth(_graphemeLine);

        _surface = new Surface(100, 4);
        _context = new SurfaceRenderContext(_surface);
        _clip = new RectLayoutProvider(new Rect(0, 0, LineWidth, 1));
        _context.CurrentLayoutProvider = _clip;

        _narrowClip = new RectLayoutProvider(new Rect(ClippedStart, 0, ClippedWidth, 1));
        _narrowContext = new SurfaceRenderContext(_surface);
        _narrowContext.CurrentLayoutProvider = _narrowClip;
    }

    private static void AssertWidth(string line)
    {
        var width = AnsiString.VisibleLength(line);
        if (width != LineWidth)
            throw new InvalidOperationException($"Benchmark line is {width} columns wide, expected {LineWidth}.");
    }

    // ClipString

    [Benchmark]
    public (int, string) ClipString_Ascii() => LayoutProviderHelper.ClipString(_clip, 0, 0, _asciiLine);

    [Benchmark]
    public (int, string) ClipString_Graphemes() => LayoutProviderHelper.ClipString(_clip, 0, 0, _graphemeLine);

    [Benchmark]
    public (int, string) ClipString_Ascii_Clipped() => LayoutProviderHelper.ClipString(_narrowClip, 0, 0, _asciiLine);

    [Benchmark]
    public (int, string) ClipString_Graphemes_Clipped() => LayoutProviderHelper.ClipString(_narrowClip, 0, 0, _graphemeLine);

    // SliceByDisplayWidthWithAnsi

    [Benchmark]
    public (string, int, int, int) Slice_Ascii() => DisplayWidth.SliceByDisplayWidthWithAnsi(_asciiLine, 0, LineWidth);

    [Benchmark]
    public (string, int, int, int) Slice_Graphemes() => DisplayWidth.SliceByDisplayWidthWithAnsi(_graphemeLine, 0, LineWidth);

    [Benchmark]
    public (string, int, int, int) Slice_Ascii_Clipped() => DisplayWidth.SliceByDisplayWidthWithAnsi(_asciiLine, ClippedStart, ClippedWidth);

    [Benchmark]
    public (string, int, int, int) Slice_Graphemes_Clipped() => DisplayWidth.SliceByDisplayWidthWithAnsi(_graphemeLine, ClippedStart, ClippedWidth);

    // Surface write only (no clipping): SurfaceRenderContext.Write -> WriteToSurface

    [Benchmark]
    public void SurfaceWrite_Ascii()
    {
        _context.SetCursorPosition(0, 0);
        _context.Write(_asciiLine);
    }

    [Benchmark]
    public void SurfaceWrite_Graphemes()
    {
        _context.SetCursorPosition(0, 0);
        _context.Write(_graphemeLine);
    }

    // Whole per-line path: ClipString followed by the surface write

    [Benchmark]
    public void WriteClipped_Ascii() => _context.WriteClipped(0, 0, _asciiLine);

    [Benchmark]
    public void WriteClipped_Graphemes() => _context.WriteClipped(0, 0, _graphemeLine);

    [Benchmark]
    public void WriteClipped_Ascii_Clipped() => _narrowContext.WriteClipped(0, 0, _asciiLine);

    [Benchmark]
    public void WriteClipped_Graphemes_Clipped() => _narrowContext.WriteClipped(0, 0, _graphemeLine);
}
