namespace Hex1b.Tests.Scene.Textures;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hex1b;
using Hex1b.Scene.Textures;
using Hex1b.Theming;

[TestClass]
public class TerminalTextureTests
{
    private static uint Rgba(byte r, byte g, byte b, byte a = 255)
        => ((uint)r << 24) | ((uint)g << 16) | ((uint)b << 8) | a;

    // ---- Glyph coverage ----

    [TestMethod]
    public void CoverageAt_Space_IsZero()
    {
        Assert.AreEqual(0f, TerminalGlyphRasterizer.CoverageAt(" ", 0.25f, 0.25f));
        Assert.AreEqual(0f, TerminalGlyphRasterizer.CoverageAt("", 0.5f, 0.5f));
    }

    [TestMethod]
    public void CoverageAt_FullBlock_IsOneEverywhere()
    {
        Assert.AreEqual(1f, TerminalGlyphRasterizer.CoverageAt("\u2588", 0.1f, 0.1f));
        Assert.AreEqual(1f, TerminalGlyphRasterizer.CoverageAt("\u2588", 0.9f, 0.9f));
    }

    [TestMethod]
    public void CoverageAt_UpperHalfBlock_TopFilledBottomEmpty()
    {
        // ▀ U+2580
        Assert.AreEqual(1f, TerminalGlyphRasterizer.CoverageAt("\u2580", 0.5f, 0.25f), "top half should be ink");
        Assert.AreEqual(0f, TerminalGlyphRasterizer.CoverageAt("\u2580", 0.5f, 0.75f), "bottom half should be background");
    }

    [TestMethod]
    public void CoverageAt_LowerHalfBlock_BottomFilledTopEmpty()
    {
        // ▄ U+2584
        Assert.AreEqual(0f, TerminalGlyphRasterizer.CoverageAt("\u2584", 0.5f, 0.25f));
        Assert.AreEqual(1f, TerminalGlyphRasterizer.CoverageAt("\u2584", 0.5f, 0.75f));
    }

    [TestMethod]
    public void CoverageAt_LeftAndRightHalfBlocks_SplitHorizontally()
    {
        // ▌ U+258C left half
        Assert.AreEqual(1f, TerminalGlyphRasterizer.CoverageAt("\u258C", 0.25f, 0.5f));
        Assert.AreEqual(0f, TerminalGlyphRasterizer.CoverageAt("\u258C", 0.75f, 0.5f));
        // ▐ U+2590 right half
        Assert.AreEqual(0f, TerminalGlyphRasterizer.CoverageAt("\u2590", 0.25f, 0.5f));
        Assert.AreEqual(1f, TerminalGlyphRasterizer.CoverageAt("\u2590", 0.75f, 0.5f));
    }

    [TestMethod]
    public void CoverageAt_ShadeBlocks_ReturnPartialCoverage()
    {
        Assert.AreEqual(0.25f, TerminalGlyphRasterizer.CoverageAt("\u2591", 0.5f, 0.5f)); // ░
        Assert.AreEqual(0.50f, TerminalGlyphRasterizer.CoverageAt("\u2592", 0.5f, 0.5f)); // ▒
        Assert.AreEqual(0.75f, TerminalGlyphRasterizer.CoverageAt("\u2593", 0.5f, 0.5f)); // ▓
    }

    [TestMethod]
    public void CoverageAt_QuadrantLowerLeft_OnlyLowerLeftFilled()
    {
        // ▖ U+2596 lower-left quadrant
        Assert.AreEqual(1f, TerminalGlyphRasterizer.CoverageAt("\u2596", 0.25f, 0.75f));
        Assert.AreEqual(0f, TerminalGlyphRasterizer.CoverageAt("\u2596", 0.75f, 0.75f));
        Assert.AreEqual(0f, TerminalGlyphRasterizer.CoverageAt("\u2596", 0.25f, 0.25f));
    }

    [TestMethod]
    public void CoverageAt_BrailleSingleDot_OnlyTopLeftFilled()
    {
        // U+2801 = dot 1 (top-left) only
        Assert.AreEqual(1f, TerminalGlyphRasterizer.CoverageAt("\u2801", 0.25f, 0.10f));
        Assert.AreEqual(0f, TerminalGlyphRasterizer.CoverageAt("\u2801", 0.75f, 0.10f));
        Assert.AreEqual(0f, TerminalGlyphRasterizer.CoverageAt("\u2801", 0.25f, 0.90f));
    }

    [TestMethod]
    public void CoverageAt_BrailleAllDots_FullyFilled()
    {
        // U+28FF = all 8 dots
        Assert.AreEqual(1f, TerminalGlyphRasterizer.CoverageAt("\u28FF", 0.25f, 0.10f));
        Assert.AreEqual(1f, TerminalGlyphRasterizer.CoverageAt("\u28FF", 0.75f, 0.90f));
    }

    [TestMethod]
    public void CoverageAt_OrdinaryText_ReturnsApproximateCoverage()
    {
        Assert.AreEqual(TerminalGlyphRasterizer.DefaultTextCoverage,
            TerminalGlyphRasterizer.CoverageAt("A", 0.5f, 0.5f));
    }

    // ---- Cell sampling ----

    [TestMethod]
    public void SampleInto_FullBlock_PaintsForegroundColor()
    {
        var buffer = new TerminalCell[1, 1];
        buffer[0, 0] = new TerminalCell("\u2588", Hex1bColor.Red, Hex1bColor.Blue);

        var texture = TerminalCellTextureSampler.CreateTexture(
            buffer, 1, 1, cellPixelWidth: 2, cellPixelHeight: 2);

        Assert.AreEqual(2, texture.Width);
        Assert.AreEqual(2, texture.Height);
        // Every pixel should be the foreground (red), since full block = full coverage.
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 2; x++)
                Assert.AreEqual(Rgba(255, 0, 0), texture.GetPixel(x, y), $"pixel ({x},{y})");
    }

    [TestMethod]
    public void SampleInto_Space_PaintsBackgroundColor()
    {
        var buffer = new TerminalCell[1, 1];
        buffer[0, 0] = new TerminalCell(" ", Hex1bColor.Red, Hex1bColor.Blue);

        var texture = TerminalCellTextureSampler.CreateTexture(
            buffer, 1, 1, cellPixelWidth: 2, cellPixelHeight: 2);

        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 2; x++)
                Assert.AreEqual(Rgba(0, 0, 255), texture.GetPixel(x, y));
    }

    [TestMethod]
    public void SampleInto_UpperHalfBlock_ReconstructsTopForegroundBottomBackground()
    {
        // ▀ red on blue, expanded to 1x2 reconstructs the two vertical pixels exactly.
        var buffer = new TerminalCell[1, 1];
        buffer[0, 0] = new TerminalCell("\u2580", Hex1bColor.Red, Hex1bColor.Blue);

        var texture = TerminalCellTextureSampler.CreateTexture(
            buffer, 1, 1, cellPixelWidth: 1, cellPixelHeight: 2);

        Assert.AreEqual(Rgba(255, 0, 0), texture.GetPixel(0, 0), "top pixel = foreground");
        Assert.AreEqual(Rgba(0, 0, 255), texture.GetPixel(0, 1), "bottom pixel = background");
    }

    [TestMethod]
    public void SampleInto_ReverseAttribute_SwapsForegroundAndBackground()
    {
        var buffer = new TerminalCell[1, 1];
        buffer[0, 0] = new TerminalCell("\u2588", Hex1bColor.Red, Hex1bColor.Blue, CellAttributes.Reverse);

        var texture = TerminalCellTextureSampler.CreateTexture(
            buffer, 1, 1, cellPixelWidth: 1, cellPixelHeight: 1);

        // Full block + reversed => paints the (swapped) foreground = blue.
        Assert.AreEqual(Rgba(0, 0, 255), texture.GetPixel(0, 0));
    }

    [TestMethod]
    public void SampleInto_DefaultColors_UseProvidedDefaults()
    {
        var buffer = new TerminalCell[1, 1];
        buffer[0, 0] = new TerminalCell("\u2588", null, null);

        var texture = TerminalCellTextureSampler.CreateTexture(
            buffer, 1, 1, cellPixelWidth: 1, cellPixelHeight: 1,
            defaultForeground: Hex1bColor.Green, defaultBackground: Hex1bColor.Black);

        Assert.AreEqual(Rgba(0, 255, 0), texture.GetPixel(0, 0));
    }

    // ---- TerminalTexture wrapper ----

    private sealed class FakeSource : ITerminalTextureSource
    {
        public TerminalCell[,] Buffer = new TerminalCell[1, 1];
        public int Width = 1;
        public int Height = 1;

        public (TerminalCell[,] Buffer, int Width, int Height) GetScreenBufferSnapshot()
            => (Buffer, Width, Height);
    }

    [TestMethod]
    public void TerminalTexture_Update_ProducesTextureFromSource()
    {
        var source = new FakeSource
        {
            Width = 2,
            Height = 1,
            Buffer = new TerminalCell[1, 2]
        };
        source.Buffer[0, 0] = new TerminalCell("\u2588", Hex1bColor.Red, Hex1bColor.Black);
        source.Buffer[0, 1] = new TerminalCell(" ", Hex1bColor.Red, Hex1bColor.Black);

        var termTex = new TerminalTexture(source, cellPixelWidth: 1, cellPixelHeight: 1);
        var texture = termTex.Update();

        Assert.AreEqual(2, texture.Width);
        Assert.AreEqual(1, texture.Height);
        Assert.AreEqual(Rgba(255, 0, 0), texture.GetPixel(0, 0));
        Assert.AreEqual(Rgba(0, 0, 0), texture.GetPixel(1, 0));
    }

    [TestMethod]
    public void TerminalTexture_Update_ReusesTextureWhenSizeUnchanged()
    {
        var source = new FakeSource { Width = 2, Height = 2, Buffer = new TerminalCell[2, 2] };
        var termTex = new TerminalTexture(source);

        var first = termTex.Update();
        var second = termTex.Update();

        Assert.AreSame(first, second, "texture instance should be reused when size is stable");
    }

    [TestMethod]
    public void TerminalTexture_Update_ReallocatesOnResize()
    {
        var source = new FakeSource { Width = 2, Height = 2, Buffer = new TerminalCell[2, 2] };
        var termTex = new TerminalTexture(source, cellPixelWidth: 1, cellPixelHeight: 1);

        var first = termTex.Update();
        Assert.AreEqual(2, first.Width);

        source.Width = 4;
        source.Height = 3;
        source.Buffer = new TerminalCell[3, 4];
        var second = termTex.Update();

        Assert.AreNotSame(first, second);
        Assert.AreEqual(4, second.Width);
        Assert.AreEqual(3, second.Height);
    }

    [TestMethod]
    public void TerminalTexture_TextureIsNullBeforeFirstUpdate()
    {
        var source = new FakeSource();
        var termTex = new TerminalTexture(source);
        Assert.IsNull(termTex.Texture);
        termTex.Update();
        Assert.IsNotNull(termTex.Texture);
    }
}
