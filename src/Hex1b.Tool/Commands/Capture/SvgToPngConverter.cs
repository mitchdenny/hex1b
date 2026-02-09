using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Skia;

namespace Hex1b.Tool.Commands.Capture;

/// <summary>
/// Converts terminal SVG screenshots to PNG using a two-pass rendering approach.
/// Pass 1: Svg.Skia renders backgrounds/rects (text stripped).
/// Pass 2: SkiaSharp draws text directly with the embedded Nerd Font.
/// This bypasses Svg.Skia's inability to render Private Use Area glyphs.
/// </summary>
internal static class SvgToPngConverter
{
    internal const string EmbeddedFontFamily = "CaskaydiaCove Nerd Font Mono";
    internal const string EmbeddedFontResource = "Hex1b.Tool.Fonts.CaskaydiaCoveNerdFontMono-Regular.ttf";

    private static readonly XNamespace SvgNamespace = "http://www.w3.org/2000/svg";
    private static readonly Regex s_rgbPattern = new(@"^rgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)$", RegexOptions.Compiled);
    private static readonly Regex s_fontSizePattern = new(@"font-size:\s*(\d+)px", RegexOptions.Compiled);

    internal record TextElement(float X, float Y, string Fill, string? Style, string Content);

    internal static SKColor ParseSvgColor(string color)
    {
        var rgbMatch = s_rgbPattern.Match(color);
        if (rgbMatch.Success)
        {
            return new SKColor(
                byte.Parse(rgbMatch.Groups[1].Value, CultureInfo.InvariantCulture),
                byte.Parse(rgbMatch.Groups[2].Value, CultureInfo.InvariantCulture),
                byte.Parse(rgbMatch.Groups[3].Value, CultureInfo.InvariantCulture));
        }

        return SKColor.Parse(color);
    }

    internal static (List<TextElement> TextElements, string BackgroundSvg, float FontSize, int Width, int Height) ExtractTextElements(string svgContent)
    {
        var doc = XDocument.Parse(svgContent);

        var textElements = doc.Descendants(SvgNamespace + "text").Select(t => new TextElement(
            X: float.Parse(t.Attribute("x")?.Value ?? "0", CultureInfo.InvariantCulture),
            Y: float.Parse(t.Attribute("y")?.Value ?? "0", CultureInfo.InvariantCulture),
            Fill: t.Attribute("fill")?.Value ?? "#ffffff",
            Style: t.Attribute("style")?.Value,
            Content: t.Value
        )).ToList();

        float fontSize = 14f;
        var styleEl = doc.Descendants(SvgNamespace + "style").FirstOrDefault();
        if (styleEl != null)
        {
            var match = s_fontSizePattern.Match(styleEl.Value);
            if (match.Success)
                fontSize = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        doc.Descendants(SvgNamespace + "text").Remove();
        var bgSvg = doc.ToString();

        var root = doc.Root!;
        var width = (int)Math.Ceiling(float.Parse(root.Attribute("width")?.Value ?? "800", CultureInfo.InvariantCulture));
        var height = (int)Math.Ceiling(float.Parse(root.Attribute("height")?.Value ?? "600", CultureInfo.InvariantCulture));

        return (textElements, bgSvg, fontSize, width, height);
    }

    internal static byte[] Convert(string svgContent, SKTypeface? typeface = null)
    {
        var (textElements, bgSvg, fontSize, width, height) = ExtractTextElements(svgContent);

        // Pass 1: Render backgrounds with Svg.Skia
        using var svg = new SKSvg();
        using var bgStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(bgSvg));
        svg.Load(bgStream);

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        if (svg.Picture != null)
            canvas.DrawPicture(svg.Picture);

        // Pass 2: Draw text directly with SkiaSharp
        var ownsTypeface = typeface == null;
        typeface ??= LoadEmbeddedTypeface();

        if (typeface != null)
        {
            try
            {
                RenderText(canvas, textElements, typeface, fontSize);
            }
            finally
            {
                if (ownsTypeface) typeface.Dispose();
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    internal static void RenderText(SKCanvas canvas, List<TextElement> textElements, SKTypeface typeface, float fontSize)
    {
        using var font = new SKFont(typeface, fontSize);

        foreach (var t in textElements)
        {
            if (string.IsNullOrEmpty(t.Content) || t.Content == " ")
                continue;

            var color = ParseSvgColor(t.Fill);
            using var paint = new SKPaint { Color = color };

            var isBold = t.Style?.Contains("font-weight:bold") == true;
            if (isBold)
            {
                paint.Style = SKPaintStyle.StrokeAndFill;
                paint.StrokeWidth = fontSize * 0.04f;
            }

            if (t.Style?.Contains("opacity:0.5") == true)
                paint.Color = color.WithAlpha(128);

            var isItalic = t.Style?.Contains("font-style:italic") == true;
            if (isItalic)
            {
                canvas.Save();
                canvas.Skew(-0.2f, 0);
            }

            canvas.DrawText(t.Content, t.X, t.Y, font, paint);

            if (isItalic)
                canvas.Restore();

            if (t.Style != null && (t.Style.Contains("underline") || t.Style.Contains("line-through") || t.Style.Contains("overline")))
            {
                var metrics = font.Metrics;
                using var linePaint = new SKPaint { Color = paint.Color, StrokeWidth = 1f, Style = SKPaintStyle.Stroke };
                var textWidth = font.MeasureText(t.Content);

                if (t.Style.Contains("underline"))
                    canvas.DrawLine(t.X, t.Y + 2, t.X + textWidth, t.Y + 2, linePaint);

                if (t.Style.Contains("line-through"))
                {
                    var strikeY = t.Y + metrics.StrikeoutPosition.GetValueOrDefault();
                    canvas.DrawLine(t.X, strikeY, t.X + textWidth, strikeY, linePaint);
                }

                if (t.Style.Contains("overline"))
                {
                    var overY = t.Y + metrics.Ascent;
                    canvas.DrawLine(t.X, overY, t.X + textWidth, overY, linePaint);
                }
            }
        }
    }

    internal static string EnsureEmbeddedFontExtracted()
    {
        var fontDir = Path.Combine(Path.GetTempPath(), "hex1b-fonts");
        Directory.CreateDirectory(fontDir);

        var fontPath = Path.Combine(fontDir, "CaskaydiaCoveNerdFontMono-Regular.ttf");
        if (!File.Exists(fontPath))
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedFontResource);
            if (stream == null)
                throw new InvalidOperationException($"Embedded font resource '{EmbeddedFontResource}' not found");

            using var fs = File.Create(fontPath);
            stream.CopyTo(fs);
        }

        return fontPath;
    }

    internal static SKTypeface? LoadEmbeddedTypeface()
    {
        try
        {
            var fontPath = EnsureEmbeddedFontExtracted();
            return SKTypeface.FromFile(fontPath);
        }
        catch
        {
            return null;
        }
    }
}
