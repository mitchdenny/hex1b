using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Surface Documentation: Multiple Layers
/// Demonstrates compositing multiple surface layers.
/// </summary>
public class SurfaceLayersExample(ILogger<SurfaceLayersExample> logger) : Hex1bExample
{
    private readonly ILogger<SurfaceLayersExample> _logger = logger;

    public override string Id => "surface-layers";
    public override string Title => "Surface - Multiple Layers";
    public override string Description => "Demonstrates compositing multiple surface layers";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating surface layers example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Surface Layers Demo"),
                v.Text(""),
                v.Surface(s => [
                    // Layer 1: Background gradient
                    s.Layer(surface => {
                        for (int y = 0; y < surface.Height; y++)
                        {
                            var shade = (byte)(50 + y * 15);
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
                ]).Size(30, 8)
            ]);
        };
    }
}
