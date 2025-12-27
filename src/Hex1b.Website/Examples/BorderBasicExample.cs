using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Border Widget Documentation: Basic Usage
/// Demonstrates border with content and interactive elements.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/border.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class BorderBasicExample(ILogger<BorderBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<BorderBasicExample> _logger = logger;

    public override string Id => "border-basic";
    public override string Title => "Border Widget - Basic Usage";
    public override string Description => "Demonstrates border with content";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating border basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Border(b => [
                b.Text("Welcome to Hex1b!"),
                b.Text(""),
                b.Text("This content is wrapped"),
                b.Text("in a border widget.")
            ]);
        };
    }
}
