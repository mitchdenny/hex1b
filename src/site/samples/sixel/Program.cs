#pragma warning disable HEX1B_SIXEL

using Hex1b;
using Hex1b.Surfaces;

var pixels = new SixelPixelBuffer(160, 80);
for (var y = 0; y < pixels.Height; y++)
{
    for (var x = 0; x < pixels.Width; x++)
    {
        pixels[x, y] = Rgba32.FromRgb(
            (byte)(x * 255 / pixels.Width),
            (byte)(y * 255 / pixels.Height),
            180);
    }
}

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(ctx =>
        ctx.Sixel(
                pixels,
                fallback => fallback.Text("Sixel graphics are unavailable."))
            .Width(24)
            .Height(8))
    .Build();

await terminal.RunAsync();
