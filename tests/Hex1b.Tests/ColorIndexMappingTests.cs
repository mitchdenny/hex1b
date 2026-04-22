using Hex1b.Theming;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for color index boundary correctness — verifying that SGR 30-37 (standard)
/// and SGR 90-97 (bright) map to distinct colors, and that 256-color and RGB
/// pass through correctly. Guards against the index 7/15 swap bug found in psmux.
/// Inspired by psmux's test_issue155_rendering.rs color mapping tests.
/// </summary>
public class ColorIndexMappingTests
{
    private sealed class TestTerminal : IDisposable
    {
        private readonly StreamWorkloadAdapter _workload;
        public Hex1bTerminal Terminal { get; }

        public TestTerminal(int width = 80, int height = 24)
        {
            _workload = StreamWorkloadAdapter.CreateHeadless(width, height);
            Terminal = Hex1bTerminal.CreateBuilder()
                .WithWorkload(_workload).WithHeadless().WithDimensions(width, height).Build();
        }

        public void Write(string text)
        {
            Terminal.ApplyTokens(AnsiTokenizer.Tokenize(text));
        }

        public void Dispose()
        {
            Terminal.Dispose();
            _workload.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    [Fact]
    public void Sgr37_Index7_IsLightGrayNotWhite()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[37mA");

        var snap = t.Terminal.CreateSnapshot();
        var fg = snap.GetCell(0, 0).Foreground;
        Assert.NotNull(fg);

        // Index 7 (SGR 37) should be light gray (192,192,192), NOT white (255,255,255)
        Assert.Equal(192, fg.Value.R);
        Assert.Equal(192, fg.Value.G);
        Assert.Equal(192, fg.Value.B);
        Assert.Equal(Hex1bColorKind.Standard, fg.Value.Kind);
        Assert.Equal(7, fg.Value.AnsiIndex);
    }

    [Fact]
    public void Sgr97_Index15_IsWhiteNotGray()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[97mA");

        var snap = t.Terminal.CreateSnapshot();
        var fg = snap.GetCell(0, 0).Foreground;
        Assert.NotNull(fg);

        // Index 15 (SGR 97) should be white (255,255,255), NOT gray
        Assert.Equal(255, fg.Value.R);
        Assert.Equal(255, fg.Value.G);
        Assert.Equal(255, fg.Value.B);
        Assert.Equal(Hex1bColorKind.Bright, fg.Value.Kind);
        Assert.Equal(7, fg.Value.AnsiIndex);
    }

    [Fact]
    public void Sgr37_And_Sgr97_ProduceDistinctColors()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[37mA\x1b[97mB");

        var snap = t.Terminal.CreateSnapshot();
        var fg37 = snap.GetCell(0, 0).Foreground;
        var fg97 = snap.GetCell(1, 0).Foreground;

        Assert.NotNull(fg37);
        Assert.NotNull(fg97);
        Assert.NotEqual(fg37, fg97);
    }

    [Theory]
    [InlineData(30, 0, 0, 0)]       // Black
    [InlineData(31, 128, 0, 0)]     // Red
    [InlineData(32, 0, 128, 0)]     // Green
    [InlineData(33, 128, 128, 0)]   // Yellow
    [InlineData(34, 0, 0, 128)]     // Blue
    [InlineData(35, 128, 0, 128)]   // Magenta
    [InlineData(36, 0, 128, 128)]   // Cyan
    [InlineData(37, 192, 192, 192)] // White/LightGray
    public void StandardForegroundColors_MapCorrectly(int sgr, byte r, byte g, byte b)
    {
        using var t = new TestTerminal();
        t.Write($"\x1b[{sgr}mX");

        var snap = t.Terminal.CreateSnapshot();
        var fg = snap.GetCell(0, 0).Foreground;
        Assert.NotNull(fg);
        Assert.Equal(r, fg.Value.R);
        Assert.Equal(g, fg.Value.G);
        Assert.Equal(b, fg.Value.B);
        Assert.Equal(Hex1bColorKind.Standard, fg.Value.Kind);
        Assert.Equal(sgr - 30, fg.Value.AnsiIndex);
    }

    [Theory]
    [InlineData(90, 128, 128, 128)]   // Bright Black (Dark Gray)
    [InlineData(91, 255, 0, 0)]       // Bright Red
    [InlineData(92, 0, 255, 0)]       // Bright Green
    [InlineData(93, 255, 255, 0)]     // Bright Yellow
    [InlineData(94, 0, 0, 255)]       // Bright Blue
    [InlineData(95, 255, 0, 255)]     // Bright Magenta
    [InlineData(96, 0, 255, 255)]     // Bright Cyan
    [InlineData(97, 255, 255, 255)]   // Bright White
    public void BrightForegroundColors_MapCorrectly(int sgr, byte r, byte g, byte b)
    {
        using var t = new TestTerminal();
        t.Write($"\x1b[{sgr}mX");

        var snap = t.Terminal.CreateSnapshot();
        var fg = snap.GetCell(0, 0).Foreground;
        Assert.NotNull(fg);
        Assert.Equal(r, fg.Value.R);
        Assert.Equal(g, fg.Value.G);
        Assert.Equal(b, fg.Value.B);
        Assert.Equal(Hex1bColorKind.Bright, fg.Value.Kind);
        Assert.Equal(sgr - 90, fg.Value.AnsiIndex);
    }

    [Fact]
    public void Color256_Index196_MapsCorrectly()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[38;5;196mX"); // Index 196 = bright red in 6x6x6 cube

        var snap = t.Terminal.CreateSnapshot();
        var fg = snap.GetCell(0, 0).Foreground;
        Assert.NotNull(fg);

        // Index 196 = 6x6x6 cube: (196-16)=180, r=180/36=5→255, g=0, b=0
        Assert.Equal(255, fg.Value.R);
        Assert.Equal(0, fg.Value.G);
        Assert.Equal(0, fg.Value.B);
        Assert.Equal(Hex1bColorKind.Indexed, fg.Value.Kind);
        Assert.Equal(196, fg.Value.AnsiIndex);
    }

    [Fact]
    public void RgbColor_PassthroughExact()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[38;2;171;205;239mX");

        var snap = t.Terminal.CreateSnapshot();
        var fg = snap.GetCell(0, 0).Foreground;
        Assert.NotNull(fg);
        Assert.Equal(Hex1bColor.FromRgb(171, 205, 239), fg.Value);
    }

    [Fact]
    public void Sgr39_ResetsToDefaultForeground()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[31mR\x1b[39mD");

        var snap = t.Terminal.CreateSnapshot();
        var fgR = snap.GetCell(0, 0).Foreground;
        var fgD = snap.GetCell(1, 0).Foreground;

        Assert.NotNull(fgR); // Red should be set
        Assert.Null(fgD);    // Default foreground = null
    }
}
