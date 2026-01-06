using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Drawer Widget Documentation: Basic Usage
/// Demonstrates a simple expandable/collapsible drawer with header and content.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/drawer.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class DrawerBasicExample(ILogger<DrawerBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<DrawerBasicExample> _logger = logger;

    public override string Id => "drawer-basic";
    public override string Title => "Drawer Widget - Basic Usage";
    public override string Description => "Demonstrates a basic expandable/collapsible drawer";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating drawer basic example widget builder");

        var isExpanded = false;

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Drawer(
                isExpanded: isExpanded,
                onToggle: expanded => isExpanded = expanded,
                header: ctx.Text("📁 Files"),
                content: ctx.VStack(v => [
                    v.Text("Documents"),
                    v.Text("Downloads"),
                    v.Text("Pictures"),
                    v.Text("Videos")
                ])
            );
        };
    }
}
