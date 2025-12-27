using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Border Widget Documentation: Border with Title
/// Demonstrates border with a title in the top edge.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the titleCode sample in:
/// src/content/guide/widgets/border.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class BorderTitleExample(ILogger<BorderTitleExample> logger) : Hex1bExample
{
    private readonly ILogger<BorderTitleExample> _logger = logger;

    public override string Id => "border-title";
    public override string Title => "Border Widget - With Title";
    public override string Description => "Demonstrates border with a title";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating border title example widget builder");

        var clickCount = 0;

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Border(b => [
                b.VStack(v => [
                    v.Text($"Button clicked {clickCount} times"),
                    v.Text(""),
                    v.Button("Click me!").OnClick(_ => clickCount++),
                    v.Text(""),
                    v.Text("Use Tab to focus, Enter to activate")
                ])
            ], title: "Interactive Demo");
        };
    }
}
