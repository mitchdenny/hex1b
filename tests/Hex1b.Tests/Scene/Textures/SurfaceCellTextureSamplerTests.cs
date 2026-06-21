#pragma warning disable HEX1B_SCENE // Tests exercise the experimental Scene API
namespace Hex1b.Tests.Scene.Textures;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hex1b;
using Hex1b.Scene.Textures;
using Hex1b.Surfaces;
using Hex1b.Theming;

[TestClass]
public class SurfaceCellTextureSamplerTests
{
    private static (byte r, byte g, byte b, byte a) Unpack(uint rgba)
        => ((byte)((rgba >> 24) & 0xFF), (byte)((rgba >> 16) & 0xFF), (byte)((rgba >> 8) & 0xFF), (byte)(rgba & 0xFF));

    [TestMethod]
    public void CreateTexture_SizesToSurfaceTimesCellPixels()
    {
        var surface = new Surface(4, 3);

        var texture = SurfaceCellTextureSampler.CreateTexture(surface, cellPixelWidth: 2, cellPixelHeight: 2);

        Assert.AreEqual(8, texture.Width);
        Assert.AreEqual(6, texture.Height);
    }

    [TestMethod]
    public void CreateTexture_FullBlockCell_FillsWithForeground()
    {
        var surface = new Surface(1, 1);
        surface.TrySetCell(0, 0, new SurfaceCell("\u2588", Hex1bColor.FromRgb(255, 0, 0), Hex1bColor.FromRgb(0, 0, 0)));

        var texture = SurfaceCellTextureSampler.CreateTexture(surface);

        var (r, g, b, a) = Unpack(texture.GetPixel(0, 0));
        Assert.AreEqual(255, r);
        Assert.AreEqual(0, g);
        Assert.AreEqual(0, b);
        Assert.AreEqual(255, a);
    }

    [TestMethod]
    public void CreateTexture_SpaceCell_FillsWithBackground()
    {
        var surface = new Surface(1, 1);
        surface.TrySetCell(0, 0, new SurfaceCell(" ", Hex1bColor.FromRgb(255, 255, 255), Hex1bColor.FromRgb(10, 20, 30)));

        var texture = SurfaceCellTextureSampler.CreateTexture(surface);

        var (r, g, b, _) = Unpack(texture.GetPixel(0, 0));
        Assert.AreEqual(10, r);
        Assert.AreEqual(20, g);
        Assert.AreEqual(30, b);
    }

    [TestMethod]
    public void CreateTexture_UpperHalfBlock_TopIsForegroundBottomIsBackground()
    {
        var surface = new Surface(1, 1);
        // U+2580 ▀ upper half block: top row foreground, bottom row background.
        surface.TrySetCell(0, 0, new SurfaceCell("\u2580", Hex1bColor.FromRgb(0, 255, 0), Hex1bColor.FromRgb(0, 0, 0)));

        var texture = SurfaceCellTextureSampler.CreateTexture(surface, cellPixelWidth: 2, cellPixelHeight: 2);

        var top = Unpack(texture.GetPixel(0, 0));
        var bottom = Unpack(texture.GetPixel(0, 1));
        Assert.AreEqual(255, top.g, "top half should be foreground");
        Assert.AreEqual(0, bottom.g, "bottom half should be background");
    }

    [TestMethod]
    public void CreateTexture_NullColors_FallBackToDefaults()
    {
        var surface = new Surface(1, 1);
        surface.TrySetCell(0, 0, new SurfaceCell("\u2588", null, null));

        var texture = SurfaceCellTextureSampler.CreateTexture(
            surface,
            defaultForeground: Hex1bColor.FromRgb(1, 2, 3),
            defaultBackground: Hex1bColor.FromRgb(4, 5, 6));

        var (r, g, b, _) = Unpack(texture.GetPixel(0, 0));
        Assert.AreEqual(1, r);
        Assert.AreEqual(2, g);
        Assert.AreEqual(3, b);
    }

    [TestMethod]
    public void CreateTexture_ReverseAttribute_SwapsForegroundAndBackground()
    {
        var surface = new Surface(1, 1);
        surface.TrySetCell(0, 0, new SurfaceCell(
            "\u2588",
            Hex1bColor.FromRgb(200, 0, 0),
            Hex1bColor.FromRgb(0, 0, 200),
            CellAttributes.Reverse));

        var texture = SurfaceCellTextureSampler.CreateTexture(surface);

        // Full block draws the (swapped) foreground, which is the original background.
        var (r, _, b, _) = Unpack(texture.GetPixel(0, 0));
        Assert.AreEqual(0, r);
        Assert.AreEqual(200, b);
    }

    [TestMethod]
    public void CreateTexture_ThrowsForNonPositiveCellPixels()
    {
        var surface = new Surface(2, 2);
        Assert.ThrowsExactly<ArgumentException>(() => SurfaceCellTextureSampler.CreateTexture(surface, cellPixelWidth: 0));
    }
}