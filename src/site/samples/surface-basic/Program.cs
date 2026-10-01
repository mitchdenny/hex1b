using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Surface(s => [
        s.Layer(surface => {
            // Draw a simple pattern
            for (int y = 0; y < surface.Height; y++)
            {
                for (int x = 0; x < surface.Width; x++)
                {
                    var isCheckerboard = (x + y) % 2 == 0;
                    surface[x, y] = SurfaceCells.Char(
                        isCheckerboard ? '░' : '▓',
                        isCheckerboard ? Hex1bColor.DarkGray : Hex1bColor.Gray
                    );
                }
            }
        })
    ]).Size(20, 10))
    .Build();

await terminal.RunAsync();
