using Hex1b;

// Generate a simple gradient image (RGBA32 format: 4 bytes per pixel)
var width = 64;
var height = 32;
var pixels = new byte[width * height * 4];
for (int y = 0; y < height; y++)
{
    for (int x = 0; x < width; x++)
    {
        var i = (y * width + x) * 4;
        pixels[i]     = (byte)(x * 255 / width);   // R
        pixels[i + 1] = (byte)(y * 255 / height);  // G
        pixels[i + 2] = 128;                        // B
        pixels[i + 3] = 255;                        // A
    }
}

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("KGP Image Demo"),
        v.KgpImage(pixels, width, height,
            img => img.Text("Terminal does not support graphics"))
    ]))
    .Build();

await terminal.RunAsync();
