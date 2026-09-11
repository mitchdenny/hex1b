#pragma warning disable HEX1B_SIXEL // This sample demonstrates the experimental Sixel widget API.

using Hex1b;
using Hex1b.Layout;
using Hex1b.Sixel;
using Hex1b.Surfaces;
using Hex1b.Widgets;

var images = new[]
{
    CreateGradient(240, 120, Rgba32.FromRgb(20, 90, 220), Rgba32.FromRgb(255, 80, 70)),
    CreateCheckerboard(240, 120, 20),
    CreateRings(240, 120)
};

var selectedImage = 0;
var horizontalOffset = 0;
var showMovingImage = true;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(ctx => ctx.VStack(root =>
    [
        root.Text("SixelWidget: structured pixels, native output, Surface composition"),
        root.Text("Use Tab/Shift+Tab and Enter to replace, move, or remove the lower image."),
        root.HStack(row =>
        [
            row.Border(
                    row.Sixel(
                        images[0],
                        fallback => fallback.Text("[Sixel unavailable: natural-size image]")))
                .Title("Natural pixel-to-cell size"),
            row.Border(
                    row.Sixel(
                            images[1],
                            fallback => fallback.Text("[Sixel unavailable: explicit-size image]"))
                        .Width(24)
                        .Height(8))
                .Title("Explicit 24x8 cells")
        ]).ContentHeight(),
        root.Border(
                root.HScrollPanel(
                    root.Sixel(
                            images[2],
                            fallback => fallback.Text("[Sixel unavailable: clipped image]"))
                        .Width(42)
                        .Height(9),
                    showScrollbar: false))
            .Title("Clipped to a 28x8 viewport")
            .FixedWidth(30)
            .FixedHeight(10),
        root.Border(
                root.ZStack(stack =>
                [
                    stack.Sixel(
                            images[selectedImage],
                            fallback => fallback.Text("[Sixel unavailable: replacement image]"))
                        .Width(32)
                        .Height(9),
                    stack.Center(center => center.Border(
                        center.Text(" text over Sixel ")))
                ]))
            .Title("Deterministic text overlap")
            .FixedWidth(34)
            .FixedHeight(11),
        root.HStack(row =>
        [
            row.Button("Replace").OnClick(_ => selectedImage = (selectedImage + 1) % images.Length),
            row.Button("Move").OnClick(_ => horizontalOffset = (horizontalOffset + 4) % 17),
            row.Button(showMovingImage ? "Remove" : "Restore").OnClick(_ => showMovingImage = !showMovingImage)
        ]).ContentHeight(),
        root.Border(
                root.HStack(row =>
                [
                    row.Text(new string(' ', horizontalOffset)),
                    showMovingImage
                        ? row.Sixel(
                                images[selectedImage],
                                fallback => fallback.Text("[Sixel unavailable: moving image]"))
                            .Width(24)
                            .Height(7)
                        : row.Text("[image removed; prior pixels should be cleared]")
                ]))
            .Title($"Replacement / movement / removal (offset {horizontalOffset})")
            .FixedHeight(9)
    ]))
    .Build();

await terminal.RunAsync();

static SixelPixelBuffer CreateGradient(int width, int height, Rgba32 start, Rgba32 end)
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

static SixelPixelBuffer CreateCheckerboard(int width, int height, int squareSize)
{
    var pixels = new SixelPixelBuffer(width, height);
    for (var y = 0; y < height; y++)
    {
        for (var x = 0; x < width; x++)
        {
            var bright = ((x / squareSize) + (y / squareSize)) % 2 == 0;
            pixels[x, y] = bright
                ? Rgba32.FromRgb(255, 200, 40)
                : Rgba32.FromRgb(70, 20, 130);
        }
    }

    return pixels;
}

static SixelPixelBuffer CreateRings(int width, int height)
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

static byte Lerp(byte start, byte end, double amount)
    => (byte)Math.Round(start + ((end - start) * amount));
