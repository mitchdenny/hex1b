using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Example demonstrating a vertical DragBarPanel for a resizable output panel.
/// </summary>
public class DragBarVerticalExample(ILogger<DragBarVerticalExample> logger) : Hex1bExample
{
    private readonly ILogger<DragBarVerticalExample> _logger = logger;

    public override string Id => "dragbar-vertical";
    public override string Title => "DragBarPanel - Vertical Resizing";
    public override string Description => "A resizable bottom panel using DragBarPanel inside a VStack.";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating vertical DragBarPanel example");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Border(
                    v.VStack(main => [
                        main.Text(""),
                        main.Text("  Main Content Area"),
                        main.Text(""),
                        main.Text("  The panel below can be resized").Wrap(),
                        main.Text("  by dragging its top handle.").Wrap()
                    ])
                ).Title("Editor").Fill(),

                v.DragBarPanel(
                    v.VStack(panel => [
                        panel.Text(" Output Panel"),
                        panel.Text(" [INFO] Build started..."),
                        panel.Text(" [INFO] Compilation successful"),
                        panel.Text(" [INFO] 0 warnings, 0 errors")
                    ])
                )
                .InitialSize(8)
                .MinSize(4)
                .MaxSize(20)
            ]);
        };
    }
}
