using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// FloatPanel Widget Documentation: Basic Usage
/// Demonstrates placing widgets at absolute (x, y) coordinates.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/float-panel.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class FloatPanelBasicExample(ILogger<FloatPanelBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<FloatPanelBasicExample> _logger = logger;

    public override string Id => "float-panel-basic";
    public override string Title => "FloatPanel - Basic Usage";
    public override string Description => "Demonstrates placing widgets at absolute coordinates";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating float panel basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.FloatPanel(f => [
                f.Place(2, 1, f.Text("📍 Marker at (2, 1)")),
                f.Place(30, 5, f.Text("📍 Marker at (30, 5)")),
                f.Place(10, 9, f.Text("📍 Marker at (10, 9)")),
                f.Place(45, 3, f.Border(b => [
                    b.Text("Boxed content")
                ]).Title("Info")),
            ]);
        };
    }
}
