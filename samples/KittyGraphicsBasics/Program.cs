using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;
using Hex1b.Automation;
using SkiaSharp;
using Svg.Skia;

const uint imageWidth = 32;
const uint imageHeight = 32;
var pixelData = GenerateTestPattern(imageWidth, imageHeight);
var statusText = "Press S to save SVG, Ctrl+C to exit";

Hex1bTerminal? terminalRef = null;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Kitty Graphics Protocol (KGP) Demo"),
        v.Separator(),
        v.Text(""),
        v.Text("32×32 test pattern (gradient + color blocks):"),
        v.KittyGraphics(pixelData, imageWidth, imageHeight)
            .WithDisplaySize(16, 8),
        v.Text(""),
        v.Text("Same image at different sizes:"),
        v.HStack(h => [
            h.KittyGraphics(pixelData, imageWidth, imageHeight)
                .WithDisplaySize(8, 4),
            h.Text("  "),
            h.KittyGraphics(pixelData, imageWidth, imageHeight)
                .WithDisplaySize(4, 2),
            h.Text("  "),
            h.KittyGraphics(pixelData, imageWidth, imageHeight)
                .WithDisplaySize(2, 1)
        ]),
        v.Text(""),
        v.Separator(),
        v.Text(statusText)
    ]).WithInputBindings(bindings =>
    {
        bindings.Key(Hex1bKey.S).Action(() =>
        {
            if (terminalRef is null) return;
            var snapshot = terminalRef.CreateSnapshot();
            var svg = snapshot.ToSvg();
            
            // Save SVG
            var svgPath = Path.GetFullPath("kgp-demo.svg");
            File.WriteAllText(svgPath, svg);
            
            // Save PNG via Svg.Skia
            var pngPath = Path.GetFullPath("kgp-demo.png");
            using var skSvg = new SKSvg();
            skSvg.FromSvg(svg);
            if (skSvg.Picture is not null)
            {
                var bounds = skSvg.Picture.CullRect;
                var width = (int)bounds.Width;
                var height = (int)bounds.Height;
                using var surface = SKSurface.Create(new SKImageInfo(width, height));
                surface.Canvas.Clear(SKColors.Black);
                surface.Canvas.DrawPicture(skSvg.Picture);
                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.OpenWrite(pngPath);
                data.SaveTo(stream);
            }
            
            statusText = $"Saved: {svgPath} + {pngPath}";
        });
    }))
    .Build();

terminalRef = terminal;

await terminal.RunAsync();

/// <summary>
/// Generates a 32×32 RGBA test pattern with color gradients and blocks.
/// </summary>
static byte[] GenerateTestPattern(uint width, uint height)
{
    var data = new byte[width * height * 4];
    for (uint y = 0; y < height; y++)
    {
        for (uint x = 0; x < width; x++)
        {
            var offset = (int)((y * width + x) * 4);
            var quadrantX = x < width / 2 ? 0 : 1;
            var quadrantY = y < height / 2 ? 0 : 1;
            var quadrant = quadrantY * 2 + quadrantX;

            switch (quadrant)
            {
                case 0: // Top-left: red gradient
                    data[offset] = (byte)(x * 255 / width);
                    data[offset + 1] = 0;
                    data[offset + 2] = 0;
                    break;
                case 1: // Top-right: green gradient
                    data[offset] = 0;
                    data[offset + 1] = (byte)(y * 255 / height);
                    data[offset + 2] = 0;
                    break;
                case 2: // Bottom-left: blue gradient
                    data[offset] = 0;
                    data[offset + 1] = 0;
                    data[offset + 2] = (byte)(x * 255 / width);
                    break;
                case 3: // Bottom-right: yellow gradient
                    data[offset] = (byte)(x * 255 / width);
                    data[offset + 1] = (byte)(y * 255 / height);
                    data[offset + 2] = 0;
                    break;
            }

            data[offset + 3] = 255; // Full alpha
        }
    }

    return data;
}
