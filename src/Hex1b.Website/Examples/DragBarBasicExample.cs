using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Example demonstrating a basic DragBarPanel with a resizable sidebar.
/// </summary>
public class DragBarBasicExample(ILogger<DragBarBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<DragBarBasicExample> _logger = logger;

    public override string Id => "dragbar-basic";
    public override string Title => "DragBarPanel - Basic Usage";
    public override string Description => "A resizable sidebar using DragBarPanel inside an HStack.";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating basic DragBarPanel example");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.HStack(h => [
                h.DragBarPanel(
                    h.VStack(panel => [
                        panel.Text(" Sidebar"),
                        panel.Text(" ───────"),
                        panel.Text(" Drag the handle →"),
                        panel.Text(" or Tab to it and"),
                        panel.Text(" use ← → arrow keys")
                    ])
                )
                .InitialSize(30)
                .MinSize(15)
                .MaxSize(50),

                h.Border(
                    h.VStack(main => [
                        main.Text(""),
                        main.Text("  Main Content"),
                        main.Text(""),
                        main.Text("  This area fills the remaining space.").Wrap(),
                        main.Text("  Resize the sidebar by dragging the").Wrap(),
                        main.Text("  handle or using arrow keys.").Wrap()
                    ])
                ).Title("Content").Fill()
            ]);
        };
    }
}
