using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Icon Widget Documentation: Clickable Icons
/// Demonstrates clickable icons with OnClick handlers.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the clickableCode sample in:
/// src/content/guide/widgets/icon.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class IconClickExample(ILogger<IconClickExample> logger) : Hex1bExample
{
    private readonly ILogger<IconClickExample> _logger = logger;

    public override string Id => "icon-click";
    public override string Title => "Icon Widget - Clickable Icons";
    public override string Description => "Demonstrates clickable icons with OnClick handlers";

    private class IconState
    {
        public string LastAction { get; set; } = "(none)";
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating icon click example widget builder");

        var state = new IconState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Click an icon:"),
                v.Text(""),
                v.HStack(h => [
                    h.Icon("▶️").OnClick(_ => state.LastAction = "Play!"),
                    h.Text(" "),
                    h.Icon("⏸️").OnClick(_ => state.LastAction = "Pause!"),
                    h.Text(" "),
                    h.Icon("⏹️").OnClick(_ => state.LastAction = "Stop!")
                ]),
                v.Text(""),
                v.Text($"Last action: {state.LastAction}")
            ]);
        };
    }
}
