using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// VStack Widget Documentation: Basic Usage
/// Demonstrates vertical layout with menu buttons.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/vstack.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class VStackBasicExample(ILogger<VStackBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<VStackBasicExample> _logger = logger;

    public override string Id => "vstack-basic";
    public override string Title => "VStack Widget - Basic Usage";
    public override string Description => "Demonstrates vertical layout with menu buttons";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating VStack basic example widget builder");

        var lastAction = "None";

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Welcome to My App"),
                v.Text(""),
                v.Button("Start").OnClick(_ => lastAction = "Started!"),
                v.Button("Settings").OnClick(_ => lastAction = "Settings opened"),
                v.Button("Quit").OnClick(args => args.Context.RequestStop()),
                v.Text(""),
                v.Text($"Last action: {lastAction}")
            ]);
        };
    }
}
