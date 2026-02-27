using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

// Minimal KGP test — no windows, no menus, just a KGP image in a VStack.
// If this renders the image, the pipeline works. If not, it's a base KGP issue.

// Generate a small 8x8 red square (RGBA format, 8*8*4 = 256 bytes)
var pixelData = new byte[8 * 8 * 4];
for (var i = 0; i < 8 * 8; i++)
{
    pixelData[i * 4] = 255;     // R
    pixelData[i * 4 + 1] = 0;   // G
    pixelData[i * 4 + 2] = 0;   // B
    pixelData[i * 4 + 3] = 255; // A
}

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithKittyGraphicsSupport()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.VStack(v => [
            v.Text("KGP Minimal Test"),
            v.Text("You should see a red square below:"),
            v.Text(""),
            v.KittyGraphics(pixelData, 8, 8)
                .WithDisplaySize(16, 8),
            v.Text(""),
            v.Text("Press Escape to quit"),
        ]).WithInputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.Escape).Action(ctx =>
            {
                ctx.RequestStop();
                return Task.CompletedTask;
            });
        });
    })
    .Build();

await terminal.RunAsync();
