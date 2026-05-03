using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Smoke tests for the bundled fonts on <see cref="FigletFonts"/>. Verifies each can be loaded
/// from the embedded resource without errors and contains the required 102 FIGcharacters.
/// </summary>
public class FigletFontsCatalogTests
{
    [Theory]
    [InlineData("standard")]
    [InlineData("slant")]
    [InlineData("small")]
    [InlineData("big")]
    [InlineData("mini")]
    [InlineData("shadow")]
    [InlineData("block")]
    [InlineData("banner")]
    public void LoadBundled_AllNames_Succeeds(string name)
    {
        var font = FigletFont.LoadBundled(name);
        Assert.NotNull(font);
        Assert.True(font.Height >= 1);
    }

    [Theory]
    [InlineData("standard")]
    [InlineData("slant")]
    [InlineData("small")]
    [InlineData("big")]
    [InlineData("mini")]
    [InlineData("shadow")]
    [InlineData("block")]
    [InlineData("banner")]
    public void LoadBundled_HasAllRequiredGlyphs(string name)
    {
        var font = FigletFont.LoadBundled(name);
        for (var c = 32; c <= 126; c++)
        {
            Assert.True(font.TryGetGlyph(c, out _), $"{name} missing ASCII {c}");
        }
        foreach (var c in new[] { 196, 214, 220, 228, 246, 252, 223 })
        {
            Assert.True(font.TryGetGlyph(c, out _), $"{name} missing German block char {c}");
        }
    }

    [Fact]
    public void FigletFonts_All_LoadsEveryBundledFont()
    {
        var all = FigletFonts.All;
        Assert.Equal(8, all.Count);
        foreach (var f in all)
        {
            Assert.NotNull(f);
        }
    }

    [Fact]
    public void FigletFonts_Standard_ReturnsSameInstanceOnRepeatedAccess()
    {
        var a = FigletFonts.Standard;
        var b = FigletFonts.Standard;
        Assert.Same(a, b);
    }

    [Fact]
    public void LoadBundled_UnknownName_Throws()
    {
        Assert.Throws<ArgumentException>(() => FigletFont.LoadBundled("does-not-exist"));
    }

    [Fact]
    public async Task LoadBundledAsync_StandardLoads()
    {
        var font = await FigletFont.LoadBundledAsync("standard", TestContext.Current.CancellationToken);
        Assert.NotNull(font);
    }
}
