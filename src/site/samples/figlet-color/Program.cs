using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.EffectPanel(
            v.FigletText("Hex1b").Font(FigletFonts.Slant),
            surface =>
            {
                // Apply a horizontal gradient to non-blank cells.
                var start = (R: (byte)64,  G: (byte)156, B: (byte)255);
                var end   = (R: (byte)255, G: (byte)128, B: (byte)64);
                for (var y = 0; y < surface.Height; y++)
                for (var x = 0; x < surface.Width; x++)
                {
                    var cell = surface[x, y];
                    if (string.IsNullOrEmpty(cell.Character) || cell.Character == " ") continue;
                    var t = (double)x / Math.Max(1, surface.Width - 1);
                    var r = (byte)(start.R + (end.R - start.R) * t);
                    var g = (byte)(start.G + (end.G - start.G) * t);
                    var b = (byte)(start.B + (end.B - start.B) * t);
                    surface[x, y] = cell with { Foreground = Hex1bColor.FromRgb(r, g, b) };
                }
            }),
        v.Text(""),
        v.Text("FigletText is monochrome — apply colors with EffectPanel.")
    ]))
    .Build();

await terminal.RunAsync();
