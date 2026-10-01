using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Surface(s => [
        // Layer 1: Background gradient
        s.Layer(surface => {
            for (int y = 0; y < surface.Height; y++)
            {
                var shade = (byte)(50 + y * 10);
                for (int x = 0; x < surface.Width; x++)
                {
                    surface[x, y] = SurfaceCells.Space(Hex1bColor.FromRgb(0, 0, shade));
                }
            }
        }),
        // Layer 2: Text overlay
        s.Layer(surface => {
            var text = "SURFACE";
            var startX = (surface.Width - text.Length) / 2;
            var y = surface.Height / 2;
            for (int i = 0; i < text.Length; i++)
            {
                surface[startX + i, y] = SurfaceCells.Char(
                    text[i], Hex1bColor.White
                );
            }
        })
    ]).Size(30, 10))
    .Build();

await terminal.RunAsync();
