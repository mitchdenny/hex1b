using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// HStack Widget Documentation: Fill Sizing
/// Demonstrates horizontal layout with fill-sized children.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example demonstrates fill sizing behavior.
/// </remarks>
public class HStackFillExample(ILogger<HStackFillExample> logger) : Hex1bExample
{
    private readonly ILogger<HStackFillExample> _logger = logger;

    public override string Id => "hstack-fill";
    public override string Title => "HStack Widget - Fill Sizing";
    public override string Description => "Demonstrates horizontal fill sizing";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating HStack fill example widget builder");

        var leftClicks = 0;
        var rightClicks = 0;

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Two buttons sharing available width equally:"),
                v.Text(""),
                v.HStack(h => [
                    h.Border(b => [
                        b.VStack(v2 => [
                            v2.Text($"Left: {leftClicks}"),
                            v2.Button("Click").OnClick(_ => leftClicks++)
                        ])
                    ]).Fill(),
                    h.Border(b => [
                        b.VStack(v2 => [
                            v2.Text($"Right: {rightClicks}"),
                            v2.Button("Click").OnClick(_ => rightClicks++)
                        ])
                    ]).Fill()
                ]),
                v.Text(""),
                v.Text("Both borders use .Fill() for equal width")
            ]);
        };
    }
}
