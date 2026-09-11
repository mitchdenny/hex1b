#pragma warning disable HEX1B_SIXEL // The gallery demonstrates the experimental Sixel API.

using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Demonstrates structured Sixel pixels, explicit sizing, fallback content,
/// and image replacement through the normal widget pipeline.
/// </summary>
public class SixelExample(ILogger<SixelExample> logger) : Hex1bExample
{
    private readonly ILogger<SixelExample> _logger = logger;

    public override string Id => "sixel";
    public override string Title => "Sixel Graphics";
    public override string Description => "Structured Sixel rendering with terminal-aware sizing and fallback content.";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating Sixel widget example");

        var images = new[]
        {
            CreateGradient(240, 120, Rgba32.FromRgb(20, 90, 220), Rgba32.FromRgb(255, 80, 70)),
            CreateCheckerboard(240, 120, 20),
            CreateRings(240, 120)
        };
        var selectedImage = 0;

        return () =>
        {
            var context = new RootContext();
            return context.VStack(root =>
            [
                root.Text("SixelWidget uses structured RGBA pixels."),
                root.Text("Select an image to exercise replacement without ghost pixels."),
                root.Picker(["Gradient", "Checkerboard", "Rings"], selectedImage)
                    .OnSelectionChanged(e => selectedImage = e.SelectedIndex)
                    .ContentHeight(),
                root.Border(
                        root.Sixel(
                                images[selectedImage],
                                fallback => fallback.VStack(column =>
                                [
                                    column.Text("Sixel graphics are unavailable."),
                                    column.Text("The fallback participates in normal layout and focus.")
                                ]))
                            .Width(36)
                            .Height(12))
                    .Title("Structured pixels - 36x12 cells")
                    .FixedWidth(38)
                    .FixedHeight(14)
            ]);
        };
    }

    private static SixelPixelBuffer CreateGradient(int width, int height, Rgba32 start, Rgba32 end)
    {
        var pixels = new SixelPixelBuffer(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var amount = (double)(x + y) / (width + height - 2);
                pixels[x, y] = new Rgba32(
                    Lerp(start.R, end.R, amount),
                    Lerp(start.G, end.G, amount),
                    Lerp(start.B, end.B, amount),
                    255);
            }
        }

        return pixels;
    }

    private static SixelPixelBuffer CreateCheckerboard(int width, int height, int squareSize)
    {
        var pixels = new SixelPixelBuffer(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[x, y] = ((x / squareSize) + (y / squareSize)) % 2 == 0
                    ? Rgba32.FromRgb(255, 200, 40)
                    : Rgba32.FromRgb(70, 20, 130);
            }
        }

        return pixels;
    }

    private static SixelPixelBuffer CreateRings(int width, int height)
    {
        var pixels = new SixelPixelBuffer(width, height);
        var centerX = (width - 1) / 2d;
        var centerY = (height - 1) / 2d;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var distance = Math.Sqrt(
                    Math.Pow((x - centerX) / width, 2) +
                    Math.Pow((y - centerY) / height, 2));
                var wave = (Math.Sin(distance * 90) + 1) / 2;
                pixels[x, y] = new Rgba32(
                    (byte)(20 + wave * 40),
                    (byte)(90 + wave * 150),
                    (byte)(120 + wave * 135),
                    255);
            }
        }

        return pixels;
    }

    private static byte Lerp(byte start, byte end, double amount)
        => (byte)Math.Round(start + ((end - start) * amount));
}
