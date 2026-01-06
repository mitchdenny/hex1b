using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Drawer Widget Documentation: Position Demo
/// Demonstrates drawers in different positions (left, right).
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the positionCode sample in:
/// src/content/guide/widgets/drawer.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class DrawerPositionExample(ILogger<DrawerPositionExample> logger) : Hex1bExample
{
    private readonly ILogger<DrawerPositionExample> _logger = logger;

    public override string Id => "drawer-position";
    public override string Title => "Drawer Widget - Positions";
    public override string Description => "Demonstrates drawers in different positions";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating drawer position example widget builder");

        var leftExpanded = true;
        var rightExpanded = false;

        return () =>
        {
            var ctx = new RootContext();
            return ctx.HStack(h => [
                // Left drawer (docked)
                h.Drawer(
                    isExpanded: leftExpanded,
                    onToggle: expanded => leftExpanded = expanded,
                    header: ctx.Text("◀ Explorer"),
                    content: ctx.VStack(v => [
                        v.Text("src/"),
                        v.Text("  Program.cs"),
                        v.Text("  App.cs"),
                        v.Text("tests/")
                    ]),
                    position: DrawerPosition.Left
                ),
                // Main content
                h.VStack(v => [
                    v.Text("Editor Pane").Fill()
                ]).Fill(),
                // Right drawer
                h.Drawer(
                    isExpanded: rightExpanded,
                    onToggle: expanded => rightExpanded = expanded,
                    header: ctx.Text("▶ Properties"),
                    content: ctx.VStack(v => [
                        v.Text("Name: Program.cs"),
                        v.Text("Size: 2.4 KB"),
                        v.Text("Modified: Today")
                    ]),
                    position: DrawerPosition.Right
                )
            ]);
        };
    }
}
