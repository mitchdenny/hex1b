using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// InfoBar Widget Documentation: Basic Usage
/// Demonstrates a simple status bar at the bottom of the screen.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/infobar.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class InfoBarBasicExample(ILogger<InfoBarBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<InfoBarBasicExample> _logger = logger;

    public override string Id => "infobar-basic";
    public override string Title => "InfoBar Widget - Basic Usage";
    public override string Description => "Demonstrates a simple status bar";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating infobar basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Border(b => [
                    b.Text("Main application content area")
                ], title: "My App").Fill(),
                v.InfoBar("Ready")
            ]);
        };
    }
}
