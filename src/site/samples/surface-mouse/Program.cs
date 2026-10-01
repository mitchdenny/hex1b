using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Surface(s => [
        // Background
        s.Layer(surface => {
            for (int y = 0; y < surface.Height; y++)
                for (int x = 0; x < surface.Width; x++)
                    surface[x, y] = SurfaceCells.Char('·', Hex1bColor.DarkGray);
        }),
        // Mouse highlight using computed layer
        s.Layer(computeCtx => {
            // Only draw if mouse is over the surface
            if (s.MouseX < 0 || s.MouseY < 0)
                return computeCtx.GetBelow();  // Pass through
            
            // Highlight area around mouse
            var dx = Math.Abs(computeCtx.X - s.MouseX);
            var dy = Math.Abs(computeCtx.Y - s.MouseY);
            if (dx <= 2 && dy <= 1)
            {
                var below = computeCtx.GetBelow();
                return below
                    .WithForeground(Hex1bColor.Yellow)
                    .WithBackground(Hex1bColor.Blue);
            }
            return computeCtx.GetBelow();
        })
    ]).Size(30, 10))
    .Build();

await terminal.RunAsync();
