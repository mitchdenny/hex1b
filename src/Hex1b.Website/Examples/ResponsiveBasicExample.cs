using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// A simple responsive layout that demonstrates basic width-based conditions.
/// Shows different text content based on terminal width.
/// </summary>
public class ResponsiveBasicExample(ILogger<ResponsiveBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<ResponsiveBasicExample> _logger = logger;

    public override string Id => "responsive-basic";
    public override string Title => "Responsive - Basic Usage";
    public override string Description => "Demonstrates basic responsive layout with width-based conditions.";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating responsive basic example");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Resize your terminal to see the layout change!"),
                v.Text(""),
                v.Responsive(r => [
                    r.WhenMinWidth(100, r => r.Text("Wide layout: You have plenty of space!")),
                    r.WhenMinWidth(60, r => r.Text("Medium layout: Comfortable width")),
                    r.Otherwise(r => r.Text("Narrow layout: Compact view"))
                ])
            ]);
        };
    }
}
