using Hex1b.Theming;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for color index boundary correctness — verifying that SGR 30-37 (standard)
/// and SGR 90-97 (bright) map to distinct colors, and that 256-color and RGB
/// pass through correctly. Guards against the index 7/15 swap bug found in psmux.
/// Inspired by psmux's test_issue155_rendering.rs color mapping tests.
/// </summary>
[TestClass]
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

    [TestMethod]
    public void Sgr37_Index7_IsLightGrayNotWhite()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[37mA");

        var snap = t.Terminal.CreateSnapshot();
        var fg = snap.GetCell(0, 0).Foreground;
        Assert.IsNotNull(fg);

        // Index 7 (SGR 37) should be light gray (192,192,192), NOT white (255,255,255)
        Assert.AreEqual(192, fg.Value.R);
        Assert.AreEqual(192, fg.Value.G);
        Assert.AreEqual(192, fg.Value.B);
        Assert.AreEqual(Hex1bColorKind.Standard, fg.Value.Kind);
        Assert.AreEqual(7, fg.Value.AnsiIndex);
    }

    [TestMethod]
    public void Sgr97_Index15_IsWhiteNotGray()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[97mA");

        var snap = t.Terminal.CreateSnapshot();
        var fg = snap.GetCell(0, 0).Foreground;
        Assert.IsNotNull(fg);

        // Index 15 (SGR 97) should be white (255,255,255), NOT gray
        Assert.AreEqual(255, fg.Value.R);
        Assert.AreEqual(255, fg.Value.G);
        Assert.AreEqual(255, fg.Value.B);
        Assert.AreEqual(Hex1bColorKind.Bright, fg.Value.Kind);
        Assert.AreEqual(7, fg.Value.AnsiIndex);
    }

    [TestMethod]
    public void Sgr37_And_Sgr97_ProduceDistinctColors()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[37mA\x1b[97mB");

        var snap = t.Terminal.CreateSnapshot();
        var fg37 = snap.GetCell(0, 0).Foreground;
        var fg97 = snap.GetCell(1, 0).Foreground;

        Assert.IsNotNull(fg37);
        Assert.IsNotNull(fg97);
        Assert.AreNotEqual(fg37, fg97);
    }

    [TestMethod]
    [DataRow(30, 0, 0, 0)]       // Black
    [DataRow(31, 128, 0, 0)]     // Red
    [DataRow(32, 0, 128, 0)]     // Green
    [DataRow(33, 128, 128, 0)]   // Yellow
    [DataRow(34, 0, 0, 128)]     // Blue
    [DataRow(35, 128, 0, 128)]   // Magenta
    [DataRow(36, 0, 128, 128)]   // Cyan
    [DataRow(37, 192, 192, 192)] // White/LightGray
    public void StandardForegroundColors_MapCorrectly(int sgr, int r, int g, int b)
    {
        using var t = new TestTerminal();
        t.Write($"\x1b[{sgr}mX");

        var snap = t.Terminal.CreateSnapshot();
        var fg = snap.GetCell(0, 0).Foreground;
        Assert.IsNotNull(fg);
        Assert.AreEqual(r, fg.Value.R);
        Assert.AreEqual(g, fg.Value.G);
        Assert.AreEqual(b, fg.Value.B);
        Assert.AreEqual(Hex1bColorKind.Standard, fg.Value.Kind);
        Assert.AreEqual(sgr - 30, fg.Value.AnsiIndex);
    }

    [TestMethod]
    [DataRow(90, 128, 128, 128)]   // Bright Black (Dark Gray)
    [DataRow(91, 255, 0, 0)]       // Bright Red
    [DataRow(92, 0, 255, 0)]       // Bright Green
    [DataRow(93, 255, 255, 0)]     // Bright Yellow
    [DataRow(94, 0, 0, 255)]       // Bright Blue
    [DataRow(95, 255, 0, 255)]     // Bright Magenta
    [DataRow(96, 0, 255, 255)]     // Bright Cyan
    [DataRow(97, 255, 255, 255)]   // Bright White
    public void BrightForegroundColors_MapCorrectly(int sgr, int r, int g, int b)
    {
        using var t = new TestTerminal();
        t.Write($"\x1b[{sgr}mX");

        var snap = t.Terminal.CreateSnapshot();
        var fg = snap.GetCell(0, 0).Foreground;
        Assert.IsNotNull(fg);
        Assert.AreEqual(r, fg.Value.R);
        Assert.AreEqual(g, fg.Value.G);
        Assert.AreEqual(b, fg.Value.B);
        Assert.AreEqual(Hex1bColorKind.Bright, fg.Value.Kind);
        Assert.AreEqual(sgr - 90, fg.Value.AnsiIndex);
    }

    [TestMethod]
    public void Color256_Index196_MapsCorrectly()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[38;5;196mX"); // Index 196 = bright red in 6x6x6 cube

        var snap = t.Terminal.CreateSnapshot();
        var fg = snap.GetCell(0, 0).Foreground;
        Assert.IsNotNull(fg);

        // Index 196 = 6x6x6 cube: (196-16)=180, r=180/36=5→255, g=0, b=0
        Assert.AreEqual(255, fg.Value.R);
        Assert.AreEqual(0, fg.Value.G);
        Assert.AreEqual(0, fg.Value.B);
        Assert.AreEqual(Hex1bColorKind.Indexed, fg.Value.Kind);
        Assert.AreEqual(196, fg.Value.AnsiIndex);
    }

    [TestMethod]
    public void RgbColor_PassthroughExact()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[38;2;171;205;239mX");

        var snap = t.Terminal.CreateSnapshot();
        var fg = snap.GetCell(0, 0).Foreground;
        Assert.IsNotNull(fg);
        Assert.AreEqual(Hex1bColor.FromRgb(171, 205, 239), fg.Value);
    }

    [TestMethod]
    public void Sgr39_ResetsToDefaultForeground()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[31mR\x1b[39mD");

        var snap = t.Terminal.CreateSnapshot();
        var fgR = snap.GetCell(0, 0).Foreground;
        var fgD = snap.GetCell(1, 0).Foreground;

        Assert.IsNotNull(fgR); // Red should be set
        Assert.IsNull(fgD);    // Default foreground = null
    }
}
