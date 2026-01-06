using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Drawer Widget Documentation: Overlay Mode
/// Demonstrates a drawer that overlays content rather than pushing it aside.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the overlayCode sample in:
/// src/content/guide/widgets/drawer.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class DrawerOverlayExample(ILogger<DrawerOverlayExample> logger) : Hex1bExample
{
    private readonly ILogger<DrawerOverlayExample> _logger = logger;

    public override string Id => "drawer-overlay";
    public override string Title => "Drawer Widget - Overlay Mode";
    public override string Description => "Demonstrates a drawer that overlays content";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating drawer overlay example widget builder");

        var isExpanded = false;

        return () =>
        {
            var ctx = new RootContext();
            return ctx.ZStack(z => [
                // Main content behind the drawer
                z.VStack(v => [
                    v.Text("Main Application Content"),
                    v.Text(""),
                    v.Text("This content is always visible."),
                    v.Text("The drawer overlays on top when expanded.")
                ]),
                // Overlay drawer
                z.Drawer(
                    isExpanded: isExpanded,
                    onToggle: expanded => isExpanded = expanded,
                    header: ctx.Text("⚙️ Settings"),
                    content: ctx.VStack(v => [
                        v.Text("Theme: Dark"),
                        v.Text("Font Size: 14"),
                        v.Text("Auto-save: On")
                    ]),
                    mode: DrawerMode.Overlay
                )
            ]);
        };
    }
}
