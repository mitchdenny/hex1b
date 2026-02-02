using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Surface Documentation: Basic Usage
/// Demonstrates basic surface rendering with a checkerboard pattern.
/// </summary>
public class SurfaceBasicExample(ILogger<SurfaceBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<SurfaceBasicExample> _logger = logger;

    public override string Id => "surface-basic";
    public override string Title => "Surface - Basic Usage";
    public override string Description => "Demonstrates basic surface rendering with a pattern";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating surface basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Surface Widget Demo"),
                v.Text(""),
                v.Surface(s => [
                    s.Layer(surface => {
                        // Draw a simple checkerboard pattern
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
                ]).Size(20, 8)
            ]);
        };
    }
}
