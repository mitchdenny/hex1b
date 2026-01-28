using BenchmarkDotNet.Attributes;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;

namespace Hex1b.Benchmarks;

/// <summary>
/// Benchmarks for Surface API performance.
/// </summary>
[MemoryDiagnoser]
public class SurfaceBenchmarks
{
    private Surface _smallSurface = null!;
    private Surface _mediumSurface = null!;
    private Surface _largeSurface = null!;
    private Surface _previousSurface = null!;
    private Surface _currentSurface = null!;
    private Surface _sparseDiffPrevious = null!;
    private Surface _sparseDiffCurrent = null!;
    private SurfaceDiff _diff = null!;
    private CompositeSurface _compositeSurface = null!;

    private const string ShortText = "Hello";
    private const string MediumText = "The quick brown fox jumps over the lazy dog.";
    private const string LongText = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.";
    private const string WideCharText = "你好世界，这是一个测试。";

    [GlobalSetup]
    public void Setup()
    {
        // Pre-create surfaces for benchmarks
        _smallSurface = new Surface(80, 24);
        _mediumSurface = new Surface(160, 48);
        _largeSurface = new Surface(320, 96);

        // Create surfaces for diff benchmarks - full difference
        _previousSurface = new Surface(80, 24);
        _previousSurface.Fill(new Rect(0, 0, 80, 24), new SurfaceCell("A", Hex1bColor.White, Hex1bColor.Black));
        
        _currentSurface = new Surface(80, 24);
        _currentSurface.Fill(new Rect(0, 0, 80, 24), new SurfaceCell("B", Hex1bColor.Red, Hex1bColor.Blue));

        // Create surfaces for sparse diff (only 10% changed)
        _sparseDiffPrevious = new Surface(80, 24);
        _sparseDiffPrevious.Fill(new Rect(0, 0, 80, 24), new SurfaceCell(" ", Hex1bColor.White, Hex1bColor.Black));
        
        _sparseDiffCurrent = new Surface(80, 24);
        _sparseDiffCurrent.Fill(new Rect(0, 0, 80, 24), new SurfaceCell(" ", Hex1bColor.White, Hex1bColor.Black));
        // Change ~10% of cells
        for (int y = 0; y < 24; y += 3)
        {
            for (int x = 0; x < 80; x += 3)
            {
                _sparseDiffCurrent[x, y] = new SurfaceCell("X", Hex1bColor.Red, null);
            }
        }

        // Pre-compute a diff for token generation benchmarks
        _diff = SurfaceComparer.Compare(_previousSurface, _currentSurface);

        // Setup composite surface with layers
        _compositeSurface = new CompositeSurface(80, 24);
        var layer1 = new Surface(80, 24);
        layer1.Fill(new Rect(0, 0, 80, 24), new SurfaceCell(" ", null, Hex1bColor.Blue));
        var layer2 = new Surface(40, 12);
        layer2.Fill(new Rect(0, 0, 40, 12), new SurfaceCell("█", Hex1bColor.White, Hex1bColor.Red));
        var layer3 = new Surface(20, 6);
        layer3.WriteText(0, 0, "Dialog Box Content", Hex1bColor.Black, Hex1bColor.White);
        
        _compositeSurface.AddLayer(layer1, 0, 0);
        _compositeSurface.AddLayer(layer2, 20, 6);
        _compositeSurface.AddLayer(layer3, 30, 9);
    }

    #region Surface Creation

    [Benchmark]
    public Surface CreateSurface_Small() => new Surface(80, 24);

    [Benchmark]
    public Surface CreateSurface_Medium() => new Surface(160, 48);

    [Benchmark]
    public Surface CreateSurface_Large() => new Surface(320, 96);

    [Benchmark]
    public Surface CreateSurface_4K() => new Surface(480, 135); // ~4K terminal

    #endregion

    #region WriteText

    [Benchmark]
    public void WriteText_Short()
    {
        _smallSurface.WriteText(0, 0, ShortText, Hex1bColor.White, null);
    }

    [Benchmark]
    public void WriteText_Medium()
    {
        _smallSurface.WriteText(0, 0, MediumText, Hex1bColor.White, null);
    }

    [Benchmark]
    public void WriteText_Long()
    {
        _smallSurface.WriteText(0, 0, LongText, Hex1bColor.White, null);
    }

    [Benchmark]
    public void WriteText_WideChars()
    {
        _smallSurface.WriteText(0, 0, WideCharText, Hex1bColor.White, null);
    }

    [Benchmark]
    public void WriteText_FillScreen()
    {
        for (int y = 0; y < 24; y++)
        {
            _smallSurface.WriteText(0, y, MediumText, Hex1bColor.White, null);
        }
    }

    #endregion

    #region Fill

    [Benchmark]
    public void Fill_SmallRect()
    {
        _smallSurface.Fill(new Rect(10, 5, 20, 10), new SurfaceCell("█", Hex1bColor.Red, Hex1bColor.Blue));
    }

    [Benchmark]
    public void Fill_FullScreen()
    {
        _smallSurface.Fill(new Rect(0, 0, 80, 24), new SurfaceCell(" ", null, Hex1bColor.Black));
    }

    [Benchmark]
    public void Fill_LargeScreen()
    {
        _largeSurface.Fill(new Rect(0, 0, 320, 96), new SurfaceCell(" ", null, Hex1bColor.Black));
    }

    #endregion

    #region Composite

    [Benchmark]
    public void Composite_SmallOntoSmall()
    {
        var source = new Surface(20, 10);
        source.Fill(new Rect(0, 0, 20, 10), new SurfaceCell("X", Hex1bColor.Red, null));
        _smallSurface.Composite(source, 10, 5);
    }

    [Benchmark]
    public void Composite_MediumOntoLarge()
    {
        var source = new Surface(80, 24);
        source.Fill(new Rect(0, 0, 80, 24), new SurfaceCell("X", Hex1bColor.Red, null));
        _largeSurface.Composite(source, 50, 20);
    }

    #endregion

    #region CompositeSurface

    [Benchmark]
    public Surface CompositeSurface_Flatten_3Layers()
    {
        return _compositeSurface.Flatten();
    }

    [Benchmark]
    public SurfaceCell CompositeSurface_GetCell_Resolved()
    {
        // Access a cell that requires layer resolution
        return _compositeSurface.GetCell(35, 10);
    }

    [Benchmark]
    public void CompositeSurface_GetAllCells()
    {
        for (int y = 0; y < 24; y++)
        {
            for (int x = 0; x < 80; x++)
            {
                _ = _compositeSurface.GetCell(x, y);
            }
        }
    }

    #endregion

    #region Diff Comparison

    [Benchmark]
    public SurfaceDiff Compare_FullDiff()
    {
        return SurfaceComparer.Compare(_previousSurface, _currentSurface);
    }

    [Benchmark]
    public SurfaceDiff Compare_SparseDiff()
    {
        return SurfaceComparer.Compare(_sparseDiffPrevious, _sparseDiffCurrent);
    }

    [Benchmark]
    public SurfaceDiff Compare_NoDiff()
    {
        return SurfaceComparer.Compare(_previousSurface, _previousSurface);
    }

    [Benchmark]
    public SurfaceDiff CompareToEmpty()
    {
        return SurfaceComparer.CompareToEmpty(_currentSurface);
    }

    #endregion

    #region Token Generation

    [Benchmark]
    public IReadOnlyList<Hex1b.Tokens.AnsiToken> ToTokens_FullDiff()
    {
        return SurfaceComparer.ToTokens(_diff);
    }

    [Benchmark]
    public string ToAnsiString_FullDiff()
    {
        return SurfaceComparer.ToAnsiString(_diff);
    }

    [Benchmark]
    public string ToAnsiString_SparseDiff()
    {
        var diff = SurfaceComparer.Compare(_sparseDiffPrevious, _sparseDiffCurrent);
        return SurfaceComparer.ToAnsiString(diff);
    }

    #endregion

    #region Clone

    [Benchmark]
    public Surface Clone_Small()
    {
        return _smallSurface.Clone();
    }

    [Benchmark]
    public Surface Clone_Large()
    {
        return _largeSurface.Clone();
    }

    #endregion

    #region Sixel Encode/Decode

    private SixelPixelBuffer _smallPixelBuffer = null!;
    private SixelPixelBuffer _mediumPixelBuffer = null!;
    private SixelPixelBuffer _largePixelBuffer = null!;
    private string _encodedSmallSixel = null!;
    private string _encodedMediumSixel = null!;
    private CompositeSurface _compositeWithSixel = null!;
    private CompositeSurface _compositeWithOccludedSixel = null!;

    [GlobalSetup(Target = nameof(SixelEncode_Small))]
    public void SetupSixelBenchmarks()
    {
        // Create pixel buffers for encoding benchmarks
        _smallPixelBuffer = CreateTestPixelBuffer(100, 60);
        _mediumPixelBuffer = CreateTestPixelBuffer(500, 300);
        _largePixelBuffer = CreateTestPixelBuffer(1000, 600);

        // Pre-encode for decode benchmarks
        _encodedSmallSixel = SixelEncoder.Encode(_smallPixelBuffer);
        _encodedMediumSixel = SixelEncoder.Encode(_mediumPixelBuffer);

        // Setup composite with sixel (using a surface without actual sixel tracking
        // since TrackedObjectStore is internal - we test the encoding/decoding separately)
        var metrics = new CellMetrics(10, 20);
        _compositeWithSixel = new CompositeSurface(80, 24, metrics);
        var sixelLayer = new Surface(80, 24, metrics);
        // Fill with some content to simulate sixel region
        sixelLayer.Fill(new Rect(5, 5, 10, 3), new SurfaceCell("█", Hex1bColor.Blue, null));
        _compositeWithSixel.AddLayer(sixelLayer, 0, 0);

        // Setup composite with occluded content
        _compositeWithOccludedSixel = new CompositeSurface(80, 24, metrics);
        var bgLayer = new Surface(80, 24, metrics);
        bgLayer.Fill(new Rect(0, 0, 20, 10), new SurfaceCell("░", Hex1bColor.Gray, null));
        _compositeWithOccludedSixel.AddLayer(bgLayer, 0, 0);
        
        var dialogLayer = new Surface(6, 3, metrics);
        dialogLayer.Fill(new Rect(0, 0, 6, 3), new SurfaceCell(" ", null, Hex1bColor.Blue));
        _compositeWithOccludedSixel.AddLayer(dialogLayer, 2, 0);
    }

    private static SixelPixelBuffer CreateTestPixelBuffer(int width, int height)
    {
        var buffer = new SixelPixelBuffer(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // Create a gradient pattern
                buffer[x, y] = new Rgba32(
                    (byte)(x * 255 / width),
                    (byte)(y * 255 / height),
                    128,
                    255);
            }
        }
        return buffer;
    }

    [Benchmark]
    public string SixelEncode_Small()
    {
        return SixelEncoder.Encode(_smallPixelBuffer);
    }

    [Benchmark]
    public string SixelEncode_Medium()
    {
        return SixelEncoder.Encode(_mediumPixelBuffer);
    }

    [Benchmark]
    public string SixelEncode_Large()
    {
        return SixelEncoder.Encode(_largePixelBuffer);
    }

    [Benchmark]
    public SixelImage SixelDecode_Small()
    {
        return SixelDecoder.Decode(_encodedSmallSixel);
    }

    [Benchmark]
    public SixelImage SixelDecode_Medium()
    {
        return SixelDecoder.Decode(_encodedMediumSixel);
    }

    [Benchmark]
    public SixelPixelBuffer SixelCrop_Half()
    {
        // Crop to left half
        return _mediumPixelBuffer.Crop(0, 0, 250, 300);
    }

    [Benchmark]
    public string SixelRoundTrip_Small()
    {
        // Decode returns SixelImage, encode from pixel buffer
        var decoded = SixelDecoder.Decode(_encodedSmallSixel);
        if (decoded == null) return "";
        
        // Create a buffer from the decoded image (Pixels is RGBA byte array)
        var buffer = new SixelPixelBuffer(decoded.Width, decoded.Height);
        var pixels = decoded.Pixels;
        var idx = 0;
        for (var y = 0; y < decoded.Height; y++)
        {
            for (var x = 0; x < decoded.Width; x++)
            {
                buffer[x, y] = new Rgba32(pixels[idx], pixels[idx + 1], pixels[idx + 2], pixels[idx + 3]);
                idx += 4;
            }
        }
        return SixelEncoder.Encode(buffer);
    }

    #endregion
}
