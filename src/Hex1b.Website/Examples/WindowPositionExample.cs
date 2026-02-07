using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Window Widget Documentation: Window Positioning
/// Demonstrates different window positioning strategies.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the positionCode sample in:
/// src/content/guide/widgets/windows.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class WindowPositionExample(ILogger<WindowPositionExample> logger) : Hex1bExample
{
    private readonly ILogger<WindowPositionExample> _logger = logger;

    public override string Id => "window-position";
    public override string Title => "Window Positioning";
    public override string Description => "Demonstrates window positioning strategies";

    private int _windowCounter;

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating window position example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.ZStack(z => [
                z.WindowPanel()
                    .Background(b => b.VStack(v => [
                        v.Text(""),
                        v.Text("  Click buttons to open windows at different positions:"),
                        v.Text(""),
                        v.HStack(h => [
                            h.Text("  "),
                            h.Button("Top-Left").OnClick(e =>
                            {
                                e.Windows.Window(w => w.Text("  Top-Left  "))
                                    .Title($"TL-{++_windowCounter}")
                                    .Size(15, 5)
                                    .Position(WindowPositionSpec.TopLeft)
                                    .Open(e.Windows);
                            }),
                            h.Text(" "),
                            h.Button("Center").OnClick(e =>
                            {
                                e.Windows.Window(w => w.Text("  Center  "))
                                    .Title($"C-{++_windowCounter}")
                                    .Size(15, 5)
                                    .Position(WindowPositionSpec.Center)
                                    .Open(e.Windows);
                            }),
                            h.Text(" "),
                            h.Button("Bottom-Right").OnClick(e =>
                            {
                                e.Windows.Window(w => w.Text("  Bottom-Right  "))
                                    .Title($"BR-{++_windowCounter}")
                                    .Size(18, 5)
                                    .Position(WindowPositionSpec.BottomRight)
                                    .Open(e.Windows);
                            })
                        ]),
                        v.Text(""),
                        v.Button("Close All").OnClick(e => e.Windows.CloseAll())
                    ]))
                    .Fill()
            ]);
        };
    }
}
