using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// VStack Widget Documentation: Fill Sizing
/// Demonstrates vertical layout with fill-sized children.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example demonstrates fill sizing behavior.
/// </remarks>
public class VStackFillExample(ILogger<VStackFillExample> logger) : Hex1bExample
{
    private readonly ILogger<VStackFillExample> _logger = logger;

    public override string Id => "vstack-fill";
    public override string Title => "VStack Widget - Fill Sizing";
    public override string Description => "Demonstrates vertical fill sizing";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating VStack fill example widget builder");

        var topClicks = 0;
        var bottomClicks = 0;

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Header (fixed height)"),
                v.Text(""),
                v.Border(b => [
                    b.VStack(v2 => [
                        v2.Text($"Top panel: {topClicks} clicks"),
                        v2.Button("Click Top").OnClick(_ => topClicks++)
                    ])
                ]).Fill(),
                v.Border(b => [
                    b.VStack(v2 => [
                        v2.Text($"Bottom panel: {bottomClicks} clicks"),
                        v2.Button("Click Bottom").OnClick(_ => bottomClicks++)
                    ])
                ]).Fill(),
                v.Text(""),
                v.Text("Footer (fixed height)")
            ]);
        };
    }
}
