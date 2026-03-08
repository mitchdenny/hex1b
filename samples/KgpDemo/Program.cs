using Hex1b;

// Generate a 64x32 RGBA gradient image
const int imgW = 64, imgH = 32;
var imageData = new byte[imgW * imgH * 4];
for (var y = 0; y < imgH; y++)
{
    for (var x = 0; x < imgW; x++)
    {
        var idx = (y * imgW + x) * 4;
        imageData[idx] = (byte)(x * 255 / imgW);     // R: left-to-right gradient
        imageData[idx + 1] = (byte)(y * 255 / imgH);  // G: top-to-bottom gradient
        imageData[idx + 2] = 128;                      // B: constant
        imageData[idx + 3] = 255;                      // A: opaque
    }
}

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) =>
    {
        return ctx => ctx.VStack(v => [
            v.Text("KGP Gradient Demo"),
            v.KgpImage(imageData, imgW, imgH, "No KGP support - text fallback", width: 20, height: 8),
            v.Text("Press Ctrl+C to exit")
        ]);
    })
    .Build();

await terminal.RunAsync();
