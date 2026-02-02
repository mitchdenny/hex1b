using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Surface Documentation: Mouse Interaction
/// Demonstrates mouse-reactive surface rendering.
/// </summary>
public class SurfaceMouseExample(ILogger<SurfaceMouseExample> logger) : Hex1bExample
{
    private readonly ILogger<SurfaceMouseExample> _logger = logger;

    public override string Id => "surface-mouse";
    public override string Title => "Surface - Mouse Interaction";
    public override string Description => "Demonstrates mouse-reactive surface rendering";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating surface mouse example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Surface Mouse Demo - Move mouse over the surface"),
                v.Text(""),
                v.Surface(s => [
                    // Background
                    s.Layer(surface => {
                        for (int y = 0; y < surface.Height; y++)
                            for (int x = 0; x < surface.Width; x++)
                                surface[x, y] = SurfaceCells.Char('·', Hex1bColor.DarkGray);
                    }),
                    // Mouse highlight using computed layer
                    s.Layer(computeCtx => {
                        // Only highlight if mouse is over the surface
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
                                .WithBackground(Hex1bColor.FromRgb(0, 0, 139));  // Dark blue
                        }
                        return computeCtx.GetBelow();
                    })
                ]).Size(30, 8)
            ]);
        };
    }
}
