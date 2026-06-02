using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Smoke tests for the bundled fonts on <see cref="FigletFonts"/>. Verifies each can be loaded
/// from the embedded resource without errors and contains the required 102 FIGcharacters.
/// </summary>
[TestClass]
public class FigletFontsCatalogTests
{
    [TestMethod]
    [DataRow("standard")]
    [DataRow("slant")]
    [DataRow("small")]
    [DataRow("big")]
    [DataRow("mini")]
    [DataRow("shadow")]
    [DataRow("block")]
    [DataRow("banner")]
    public void LoadBundled_AllNames_Succeeds(string name)
    {
        var font = FigletFont.LoadBundled(name);
        Assert.IsNotNull(font);
        Assert.IsTrue(font.Height >= 1);
    }

    [TestMethod]
    [DataRow("standard")]
    [DataRow("slant")]
    [DataRow("small")]
    [DataRow("big")]
    [DataRow("mini")]
    [DataRow("shadow")]
    [DataRow("block")]
    [DataRow("banner")]
    public void LoadBundled_HasAllRequiredGlyphs(string name)
    {
        var font = FigletFont.LoadBundled(name);
        for (var c = 32; c <= 126; c++)
        {
            Assert.IsTrue(font.TryGetGlyph(c, out _), $"{name} missing ASCII {c}");
        }
        foreach (var c in new[] { 196, 214, 220, 228, 246, 252, 223 })
        {
            Assert.IsTrue(font.TryGetGlyph(c, out _), $"{name} missing German block char {c}");
        }
    }

    [TestMethod]
    public void FigletFonts_All_LoadsEveryBundledFont()
    {
        var all = FigletFonts.All;
        Assert.AreEqual(8, all.Count);
        foreach (var f in all)
        {
            Assert.IsNotNull(f);
        }
    }

    [TestMethod]
    public void FigletFonts_Standard_ReturnsSameInstanceOnRepeatedAccess()
    {
        var a = FigletFonts.Standard;
        var b = FigletFonts.Standard;
        Assert.AreSame(a, b);
    }

    [TestMethod]
    public void LoadBundled_UnknownName_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => FigletFont.LoadBundled("does-not-exist"));
    }

    [TestMethod]
    public async Task LoadBundledAsync_StandardLoads()
    {
        var font = await FigletFont.LoadBundledAsync("standard", TestContext.Current.CancellationToken);
        Assert.IsNotNull(font);
    }
}
