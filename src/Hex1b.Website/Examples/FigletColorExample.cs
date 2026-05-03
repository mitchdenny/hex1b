using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// FigletText Widget Documentation: Coloring with EffectPanel
/// Wraps a FigletText in an EffectPanel to apply a horizontal gradient.
/// FIGlet text itself is monochrome — colors are applied as a post-processing
/// effect on the rendered surface.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the colorCode sample in:
/// src/content/guide/widgets/figlet.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class FigletColorExample(ILogger<FigletColorExample> logger) : Hex1bExample
{
    private readonly ILogger<FigletColorExample> _logger = logger;

    public override string Id => "figlet-color";
    public override string Title => "FigletText - Color via EffectPanel";
    public override string Description => "Applies a gradient to FIGlet text via EffectPanel";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating figlet color example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.EffectPanel(
                    v.FigletText("Hex1b").Font(FigletFonts.Slant),
                    surface => HorizontalGradient(surface)),
                v.Text(""),
                v.Text("FigletText is monochrome — apply colors with EffectPanel."),
            ]);
        };
    }

    private static void HorizontalGradient(Surface surface)
    {
        var start = (R: (byte)64, G: (byte)156, B: (byte)255);
        var end = (R: (byte)255, G: (byte)128, B: (byte)64);
        for (var y = 0; y < surface.Height; y++)
        {
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
        }
    }
}
