using System.CommandLine;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Hex1b.Diagnostics;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Svg.Skia;

namespace Hex1b.Tool.Commands.Capture;

/// <summary>
/// Captures a terminal screen screenshot in various formats.
/// </summary>
internal sealed class CaptureScreenshotCommand : BaseCommand
{
    private readonly TerminalIdResolver _resolver;
    private readonly TerminalClient _client;

    private static readonly Argument<string> s_idArgument = new("id") { Description = "Terminal ID (or prefix)" };
    private static readonly Option<string> s_formatOption = new("--format") { DefaultValueFactory = _ => "text", Description = "Output format: text, ansi, svg, html, or png" };
    private static readonly Option<string?> s_outputOption = new("--output") { Description = "Save to file instead of stdout (required for png)" };
    private static readonly Option<string?> s_waitOption = new("--wait") { Description = "Wait for text to appear before capturing" };
    private static readonly Option<int> s_timeoutOption = new("--timeout") { DefaultValueFactory = _ => 30, Description = "Timeout in seconds for --wait" };
    private static readonly Option<int> s_scrollbackOption = new("--scrollback") { DefaultValueFactory = _ => 0, Description = "Number of scrollback lines to include" };

    public CaptureScreenshotCommand(
        TerminalIdResolver resolver,
        TerminalClient client,
        OutputFormatter formatter,
        ILogger<CaptureScreenshotCommand> logger)
        : base("screenshot", "Capture a terminal screen screenshot", formatter, logger)
    {
        _resolver = resolver;
        _client = client;

        Arguments.Add(s_idArgument);
        Options.Add(s_formatOption);
        Options.Add(s_outputOption);
        Options.Add(s_waitOption);
        Options.Add(s_timeoutOption);
        Options.Add(s_scrollbackOption);
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var id = parseResult.GetValue(s_idArgument)!;
        var format = parseResult.GetValue(s_formatOption)!;
        var outputPath = parseResult.GetValue(s_outputOption);
        var waitText = parseResult.GetValue(s_waitOption);
        var timeout = parseResult.GetValue(s_timeoutOption);
        var scrollback = parseResult.GetValue(s_scrollbackOption);

        var isPng = string.Equals(format, "png", StringComparison.OrdinalIgnoreCase);

        if (isPng && outputPath == null)
        {
            Formatter.WriteError("--output is required when using --format png");
            return 1;
        }

        var resolved = _resolver.Resolve(id);
        if (!resolved.Success)
        {
            Formatter.WriteError(resolved.Error!);
            return 1;
        }

        // Wait for text if requested
        if (waitText != null)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeout));

            while (!timeoutCts.Token.IsCancellationRequested)
            {
                var textResponse = await _client.SendAsync(resolved.SocketPath!,
                    new DiagnosticsRequest { Method = "capture", Format = "text" }, timeoutCts.Token);

                if (textResponse is { Success: true, Data: not null } && textResponse.Data.Contains(waitText, StringComparison.Ordinal))
                {
                    break;
                }

                await Task.Delay(250, timeoutCts.Token);
            }
        }

        // For PNG, capture as SVG first then convert
        var captureFormat = isPng ? "svg" : format;

        // When rendering to PNG, resolve a monospace font that's actually installed
        // and pass it through the protocol so the SVG is generated correctly.
        string? fontFamily = isPng ? ResolveMonospaceFont() : null;

        var response = await _client.SendAsync(resolved.SocketPath!,
            new DiagnosticsRequest { Method = "capture", Format = captureFormat, ScrollbackLines = scrollback > 0 ? scrollback : null, FontFamily = fontFamily }, cancellationToken);

        if (!response.Success)
        {
            Formatter.WriteError(response.Error ?? "Capture failed");
            return 1;
        }

        if (isPng)
        {
            var pngBytes = ConvertSvgToPng(response.Data!);
            await File.WriteAllBytesAsync(outputPath!, pngBytes, cancellationToken);
            Formatter.WriteLine($"Saved to {outputPath}");
        }
        else if (outputPath != null)
        {
            await File.WriteAllTextAsync(outputPath, response.Data, cancellationToken);
            Formatter.WriteLine($"Saved to {outputPath}");
        }
        else if (parseResult.GetValue(RootCommand.JsonOption))
        {
            Formatter.WriteJson(new
            {
                width = response.Width,
                height = response.Height,
                format,
                data = response.Data
            });
        }
        else
        {
            Console.Write(response.Data);
        }

        return 0;
    }

    private byte[] ConvertSvgToPng(string svgContent)
    {
        // Two-pass rendering: Svg.Skia cannot render Private Use Area glyphs (Nerd Font icons)
        // even with a custom typeface provider. Direct SkiaSharp text rendering works fine.
        // Pass 1: Strip <text> elements from SVG → render backgrounds/rects with Svg.Skia
        // Pass 2: Draw text directly onto the bitmap with SkiaSharp using our embedded font

        var doc = XDocument.Parse(svgContent);
        XNamespace ns = "http://www.w3.org/2000/svg";

        // Extract text elements with their attributes before removing them
        var textElements = doc.Descendants(ns + "text").Select(t => new TextElement(
            X: float.Parse(t.Attribute("x")?.Value ?? "0", CultureInfo.InvariantCulture),
            Y: float.Parse(t.Attribute("y")?.Value ?? "0", CultureInfo.InvariantCulture),
            Fill: t.Attribute("fill")?.Value ?? "#ffffff",
            Style: t.Attribute("style")?.Value,
            Content: t.Value
        )).ToList();

        // Extract font-size from the CSS style block (defaults to 14)
        float fontSize = 14f;
        var styleEl = doc.Descendants(ns + "style").FirstOrDefault();
        if (styleEl != null)
        {
            var match = Regex.Match(styleEl.Value, @"font-size:\s*(\d+)px");
            if (match.Success)
                fontSize = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        // Remove all <text> elements so Svg.Skia only renders backgrounds
        doc.Descendants(ns + "text").Remove();
        var bgSvg = doc.ToString();

        // Get canvas dimensions from SVG root
        var root = doc.Root!;
        var width = (int)Math.Ceiling(float.Parse(root.Attribute("width")?.Value ?? "800", CultureInfo.InvariantCulture));
        var height = (int)Math.Ceiling(float.Parse(root.Attribute("height")?.Value ?? "600", CultureInfo.InvariantCulture));

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
        using var typeface = LoadEmbeddedTypeface();
        if (typeface != null)
        {
            using var regularFont = new SKFont(typeface, fontSize);

            foreach (var t in textElements)
            {
                if (string.IsNullOrEmpty(t.Content) || t.Content == " ")
                    continue;

                var color = ParseSvgColor(t.Fill);
                using var paint = new SKPaint { Color = color };

                // Handle bold (fake bold via stroke)
                var isBold = t.Style?.Contains("font-weight:bold") == true;
                if (isBold)
                {
                    paint.Style = SKPaintStyle.StrokeAndFill;
                    paint.StrokeWidth = fontSize * 0.04f;
                }

                // Handle dim (reduced opacity)
                if (t.Style?.Contains("opacity:0.5") == true)
                    paint.Color = color.WithAlpha(128);

                // Handle italic via skew
                var isItalic = t.Style?.Contains("font-style:italic") == true;
                if (isItalic)
                {
                    canvas.Save();
                    canvas.Skew(-0.2f, 0);
                }

                canvas.DrawText(t.Content, t.X, t.Y, regularFont, paint);

                if (isItalic)
                    canvas.Restore();

                // Handle text decorations
                if (t.Style != null && (t.Style.Contains("underline") || t.Style.Contains("line-through") || t.Style.Contains("overline")))
                {
                    var metrics = regularFont.Metrics;
                    using var linePaint = new SKPaint { Color = paint.Color, StrokeWidth = 1f, Style = SKPaintStyle.Stroke };
                    var textWidth = regularFont.MeasureText(t.Content);

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

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private record TextElement(float X, float Y, string Fill, string? Style, string Content);

    private static readonly Regex s_rgbPattern = new(@"^rgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)$", RegexOptions.Compiled);

    private static SKColor ParseSvgColor(string color)
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

    private const string EmbeddedFontFamily = "CaskaydiaCove Nerd Font Mono";
    private const string EmbeddedFontResource = "Hex1b.Tool.Fonts.CaskaydiaCoveNerdFontMono-Regular.ttf";

    private static string EnsureEmbeddedFontExtracted()
    {
        var fontDir = Path.Combine(Path.GetTempPath(), "hex1b-fonts");
        Directory.CreateDirectory(fontDir);

        var fontPath = Path.Combine(fontDir, "CaskaydiaCoveNerdFontMono-Regular.ttf");
        if (!File.Exists(fontPath))
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedFontResource);
            if (stream == null)
            {
                throw new InvalidOperationException($"Embedded font resource '{EmbeddedFontResource}' not found");
            }

            using var fs = File.Create(fontPath);
            stream.CopyTo(fs);
        }

        return fontPath;
    }

    private static SKTypeface? LoadEmbeddedTypeface()
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

    private static string? ResolveMonospaceFont()
    {
        // Return the embedded font family name so the SVG is generated
        // with a font-family that matches our embedded typeface.
        return EmbeddedFontFamily;
    }
}
