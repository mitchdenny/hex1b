using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Window Widget Documentation: Resizable Windows
/// Demonstrates resizable windows with min/max constraints.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the resizableCode sample in:
/// src/content/guide/widgets/windows.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class WindowResizableExample(ILogger<WindowResizableExample> logger) : Hex1bExample
{
    private readonly ILogger<WindowResizableExample> _logger = logger;

    public override string Id => "window-resizable";
    public override string Title => "Resizable Window";
    public override string Description => "Demonstrates resizable windows with size constraints";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating window resizable example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.ZStack(z => [
                z.WindowPanel()
                    .Background(b => b.VStack(v => [
                        v.Text(""),
                        v.Button("Open Resizable Window").OnClick(e =>
                        {
                            e.Windows.Window(w => w.VStack(v => [
                                v.Text(""),
                                v.Text("  Drag edges or corners to resize!"),
                                v.Text(""),
                                v.Text("  Constraints:"),
                                v.Text("  • Min: 30×8"),
                                v.Text("  • Max: 80×20"),
                                v.Text("")
                            ]).Fill())
                            .Title("Resizable Window")
                            .Size(50, 12)
                            .Resizable(minWidth: 30, minHeight: 8, maxWidth: 80, maxHeight: 20)
                            .Open(e.Windows);
                        }),
                        v.Text(""),
                        v.Text("  Drag window edges to resize")
                    ]))
                    .Fill()
            ]);
        };
    }
}
