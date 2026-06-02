using Hex1b.Tool.Commands.Capture;
using SkiaSharp;

namespace Hex1b.Tool.Tests;

[TestClass]
public class SvgToPngConverterTests
{
    private const string MinimalSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="200" height="40">
          <defs><style>.terminal-text { font-family: 'CaskaydiaCove Nerd Font Mono'; font-size: 14px; }</style></defs>
          <rect width="200" height="40" fill="#1e1e1e"/>
          <text x="5.0" y="20.0" fill="#d4d4d4" text-anchor="start">Hello</text>
        </svg>
        """;

    // --- Color Parsing ---

    [TestMethod]
    public void ParseSvgColor_HexColor_ReturnsCorrectColor()
    {
        var color = SvgToPngConverter.ParseSvgColor("#ff6b6b");
        Assert.AreEqual(new SKColor(255, 107, 107), color);
    }

    [TestMethod]
    public void ParseSvgColor_ShortHexColor_ReturnsCorrectColor()
    {
        var color = SvgToPngConverter.ParseSvgColor("#fff");
        Assert.AreEqual(new SKColor(255, 255, 255), color);
    }

    [TestMethod]
    public void ParseSvgColor_RgbColor_ReturnsCorrectColor()
    {
        var color = SvgToPngConverter.ParseSvgColor("rgb(78,201,176)");
        Assert.AreEqual(new SKColor(78, 201, 176), color);
    }

    [TestMethod]
    public void ParseSvgColor_RgbColorWithSpaces_ReturnsCorrectColor()
    {
        var color = SvgToPngConverter.ParseSvgColor("rgb( 78 , 201 , 176 )");
        Assert.AreEqual(new SKColor(78, 201, 176), color);
    }

    [TestMethod]
    public void ParseSvgColor_InvalidColor_Throws()
    {
        Assert.Throws<Exception>(() => SvgToPngConverter.ParseSvgColor("not-a-color"));
    }

    // --- SVG Text Extraction ---

    [TestMethod]
    public void ExtractTextElements_ParsesTextPosition()
    {
        var (textElements, _, _, _, _) = SvgToPngConverter.ExtractTextElements(MinimalSvg);

        Assert.HasCount(1, textElements);
        Assert.AreEqual(5.0f, textElements[0].X);
        Assert.AreEqual(20.0f, textElements[0].Y);
    }

    [TestMethod]
    public void ExtractTextElements_ParsesFillColor()
    {
        var (textElements, _, _, _, _) = SvgToPngConverter.ExtractTextElements(MinimalSvg);

        Assert.AreEqual("#d4d4d4", textElements[0].Fill);
    }

    [TestMethod]
    public void ExtractTextElements_ParsesTextContent()
    {
        var (textElements, _, _, _, _) = SvgToPngConverter.ExtractTextElements(MinimalSvg);

        Assert.AreEqual("Hello", textElements[0].Content);
    }

    [TestMethod]
    public void ExtractTextElements_ParsesFontSize()
    {
        var (_, _, fontSize, _, _) = SvgToPngConverter.ExtractTextElements(MinimalSvg);

        Assert.AreEqual(14f, fontSize);
    }

    [TestMethod]
    public void ExtractTextElements_DefaultsFontSizeTo14()
    {
        var svgNoStyle = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="40">
              <text x="0" y="20" fill="#fff">A</text>
            </svg>
            """;

        var (_, _, fontSize, _, _) = SvgToPngConverter.ExtractTextElements(svgNoStyle);
        Assert.AreEqual(14f, fontSize);
    }

    [TestMethod]
    public void ExtractTextElements_ParsesDimensions()
    {
        var (_, _, _, width, height) = SvgToPngConverter.ExtractTextElements(MinimalSvg);

        Assert.AreEqual(200, width);
        Assert.AreEqual(40, height);
    }

    [TestMethod]
    public void ExtractTextElements_StripsTextFromBackgroundSvg()
    {
        var (_, bgSvg, _, _, _) = SvgToPngConverter.ExtractTextElements(MinimalSvg);

        Assert.DoesNotContain("<text", bgSvg);
        Assert.Contains("<rect", bgSvg);
    }

    [TestMethod]
    public void ExtractTextElements_ParsesStyleAttribute()
    {
        var svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="40">
              <text x="0" y="20" fill="#fff" style="font-weight:bold;font-style:italic;">B</text>
            </svg>
            """;

        var (textElements, _, _, _, _) = SvgToPngConverter.ExtractTextElements(svg);

        Assert.Contains("font-weight:bold", textElements[0].Style);
        Assert.Contains("font-style:italic", textElements[0].Style);
    }

    [TestMethod]
    public void ExtractTextElements_HandlesRgbFillColor()
    {
        var svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="40">
              <text x="0" y="20" fill="rgb(78,201,176)">A</text>
            </svg>
            """;

        var (textElements, _, _, _, _) = SvgToPngConverter.ExtractTextElements(svg);
        Assert.AreEqual("rgb(78,201,176)", textElements[0].Fill);
    }

    [TestMethod]
    public void ExtractTextElements_HandlesMultipleTextElements()
    {
        var svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="40">
              <text x="0" y="20" fill="#fff">A</text>
              <text x="9" y="20" fill="#f00">B</text>
              <text x="18" y="20" fill="#0f0">C</text>
            </svg>
            """;

        var (textElements, _, _, _, _) = SvgToPngConverter.ExtractTextElements(svg);

        Assert.AreEqual(3, textElements.Count);
        Assert.AreEqual("A", textElements[0].Content);
        Assert.AreEqual("B", textElements[1].Content);
        Assert.AreEqual("C", textElements[2].Content);
    }

    // --- Embedded Font ---

    [TestMethod]
    public void EnsureEmbeddedFontExtracted_ReturnsFontPath()
    {
        var path = SvgToPngConverter.EnsureEmbeddedFontExtracted();

        Assert.IsTrue(File.Exists(path));
        Assert.EndsWith("CaskaydiaCoveNerdFontMono-Regular.ttf", path);
    }

    [TestMethod]
    public void LoadEmbeddedTypeface_ReturnsNonNull()
    {
        using var typeface = SvgToPngConverter.LoadEmbeddedTypeface();

        Assert.IsNotNull(typeface);
        Assert.IsTrue(typeface.GlyphCount > 10000, $"Expected Nerd Font with many glyphs, got {typeface.GlyphCount}");
    }

    [TestMethod]
    public void LoadEmbeddedTypeface_ContainsNerdFontGlyphs()
    {
        using var typeface = SvgToPngConverter.LoadEmbeddedTypeface()!;

        // Nerd Font PUA codepoints
        Assert.AreNotEqual(0, typeface.GetGlyph(0xE0A0)); // git branch
        Assert.AreNotEqual(0, typeface.GetGlyph(0xE0B0)); // powerline arrow
        Assert.AreNotEqual(0, typeface.GetGlyph(0xF489)); // terminal icon
    }

    // --- Full Conversion ---

    [TestMethod]
    public void Convert_ProducesValidPng()
    {
        var pngBytes = SvgToPngConverter.Convert(MinimalSvg);

        Assert.IsNotEmpty(pngBytes);
        // PNG magic bytes
        Assert.AreEqual((byte)0x89, pngBytes[0]);
        Assert.AreEqual((byte)0x50, pngBytes[1]); // P
        Assert.AreEqual((byte)0x4E, pngBytes[2]); // N
        Assert.AreEqual((byte)0x47, pngBytes[3]); // G
    }

    [TestMethod]
    public void Convert_ProducesCorrectDimensions()
    {
        var pngBytes = SvgToPngConverter.Convert(MinimalSvg);

        using var bmp = SKBitmap.Decode(pngBytes);
        Assert.AreEqual(200, bmp.Width);
        Assert.AreEqual(40, bmp.Height);
    }

    [TestMethod]
    public void Convert_RendersBackgroundColor()
    {
        var pngBytes = SvgToPngConverter.Convert(MinimalSvg);

        using var bmp = SKBitmap.Decode(pngBytes);
        // Center pixel should be the background color #1e1e1e
        var pixel = bmp.GetPixel(100, 35);
        Assert.AreEqual(new SKColor(30, 30, 30), pixel);
    }

    [TestMethod]
    public void Convert_WithRgbColors_DoesNotThrow()
    {
        var svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="40">
              <defs><style>.terminal-text { font-family: 'CaskaydiaCove Nerd Font Mono'; font-size: 14px; }</style></defs>
              <rect width="100" height="40" fill="#1e1e1e"/>
              <text x="5" y="20" fill="rgb(78,201,176)">A</text>
            </svg>
            """;

        var pngBytes = SvgToPngConverter.Convert(svg);
        Assert.IsNotEmpty(pngBytes);
    }

    [TestMethod]
    public void Convert_WithNerdFontGlyphs_ProducesOutput()
    {
        var glyph = "\ue0a0";
        var svg = string.Format("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="40">
              <defs><style>.terminal-text {{ font-family: 'CaskaydiaCove Nerd Font Mono'; font-size: 14px; }}</style></defs>
              <rect width="100" height="40" fill="#1e1e1e"/>
              <text x="5" y="20" fill="#d4d4d4">{0}</text>
            </svg>
            """, glyph);

        var pngBytes = SvgToPngConverter.Convert(svg);
        Assert.IsNotEmpty(pngBytes);
    }

    [TestMethod]
    public void Convert_WithBoldStyle_ProducesOutput()
    {
        var svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="40">
              <rect width="100" height="40" fill="#1e1e1e"/>
              <text x="5" y="20" fill="#fff" style="font-weight:bold;">B</text>
            </svg>
            """;

        var pngBytes = SvgToPngConverter.Convert(svg);
        Assert.IsNotEmpty(pngBytes);
    }

    [TestMethod]
    public void Convert_WithItalicStyle_ProducesOutput()
    {
        var svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="40">
              <rect width="100" height="40" fill="#1e1e1e"/>
              <text x="5" y="20" fill="#fff" style="font-style:italic;">I</text>
            </svg>
            """;

        var pngBytes = SvgToPngConverter.Convert(svg);
        Assert.IsNotEmpty(pngBytes);
    }

    [TestMethod]
    public void Convert_WithDimStyle_ProducesOutput()
    {
        var svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="40">
              <rect width="100" height="40" fill="#1e1e1e"/>
              <text x="5" y="20" fill="#fff" style="opacity:0.5;">D</text>
            </svg>
            """;

        var pngBytes = SvgToPngConverter.Convert(svg);
        Assert.IsNotEmpty(pngBytes);
    }
}
