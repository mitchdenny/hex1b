using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// A live demo showing a holy-grail layout built with GridWidget.
/// </summary>
public class GridBasicExample(ILogger<GridBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<GridBasicExample> _logger = logger;

    public override string Id => "grid-basic";
    public override string Title => "Grid - Basic Layout";
    public override string Description => "Demonstrates a holy-grail layout with sidebar, header, and content area using GridWidget.";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating grid basic example");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Grid(g =>
            {
                g.Columns.Add(SizeHint.Fixed(22));
                g.Columns.Add(SizeHint.Fill);

                g.Rows.Add(SizeHint.Fixed(3));
                g.Rows.Add(SizeHint.Fill);
                g.Rows.Add(SizeHint.Fixed(1));

                return [
                    g.Cell(c => c.Border(b => [
                        b.VStack(v => [
                            v.Text("📂 Files"),
                            v.Text("📊 Dashboard"),
                            v.Text("⚙️ Settings"),
                        ])
                    ]).Title("Navigation")).RowSpan(0, 3).Column(0),

                    g.Cell(c => c.Border(b => [
                        b.Text("Grid Layout Demo"),
                    ]).Title("Header")).Row(0).Column(1),

                    g.Cell(c => c.Border(b => [
                        b.VStack(v => [
                            v.Text("This is the main content area."),
                            v.Text("The grid uses:"),
                            v.Text("  • Fixed(22) sidebar column"),
                            v.Text("  • Fill content column"),
                            v.Text("  • Navigation spans 3 rows"),
                        ])
                    ]).Title("Content")).Row(1).Column(1),

                    g.Cell(c => c.Text(" Status: Ready")).Row(2).Column(1),
                ];
            });
        };
    }
}
