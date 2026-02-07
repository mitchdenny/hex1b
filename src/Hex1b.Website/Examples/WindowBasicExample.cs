using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Window Widget Documentation: Basic Usage
/// Demonstrates creating and opening floating windows.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/windows.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class WindowBasicExample(ILogger<WindowBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<WindowBasicExample> _logger = logger;

    public override string Id => "window-basic";
    public override string Title => "Windows - Basic Usage";
    public override string Description => "Demonstrates creating and opening floating windows";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating window basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.ZStack(z => [
                z.WindowPanel()
                    .Background(b => b.VStack(v => [
                        v.Text(""),
                        v.Button("Open Window").OnClick(e =>
                        {
                            e.Windows.Window(w => w.VStack(v => [
                                v.Text(""),
                                v.Text("  Hello from a floating window!"),
                                v.Text(""),
                                v.Button("Close").OnClick(ev => ev.Windows.Close(w.Window))
                            ]))
                            .Title("My Window")
                            .Size(40, 8)
                            .Open(e.Windows);
                        }),
                        v.Text(""),
                        v.Text("  Press the button to open a window...")
                    ]))
                    .Fill()
            ]);
        };
    }
}
