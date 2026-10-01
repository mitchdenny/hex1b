using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;

var app = new Hex1bApp(ctx =>
    ctx.EffectPanel(
        ctx.Text("This text is dimmed"),
        surface =>
        {
            for (int y = 0; y < surface.Height; y++)
            for (int x = 0; x < surface.Width; x++)
            {
                var cell = surface[x, y];
                if (cell.Foreground is { } fg)
                    surface[x, y] = cell with
                    {
                        Foreground = Hex1bColor.FromRgb(
                            (byte)(fg.R / 2), (byte)(fg.G / 2), (byte)(fg.B / 2))
                    };
            }
        }
    )
);

await app.RunAsync();
