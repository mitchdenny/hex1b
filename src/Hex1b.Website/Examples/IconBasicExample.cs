using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Icon Widget Documentation: Basic Usage
/// Demonstrates basic icon display inline with text.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/icon.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class IconBasicExample(ILogger<IconBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<IconBasicExample> _logger = logger;

    public override string Id => "icon-basic";
    public override string Title => "Icon Widget - Basic Usage";
    public override string Description => "Demonstrates basic icon display inline with text";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating icon basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.HStack(h => [
                h.Icon("🏠"),
                h.Text(" Home"),
                h.Text("  |  "),
                h.Icon("⚙️"),
                h.Text(" Settings"),
                h.Text("  |  "),
                h.Icon("❓"),
                h.Text(" Help")
            ]);
        };
    }
}
