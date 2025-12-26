using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Button Widget Documentation: Basic Usage
/// Demonstrates simple button creation and click handling.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/button.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ButtonBasicExample(ILogger<ButtonBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<ButtonBasicExample> _logger = logger;

    public override string Id => "button-basic";
    public override string Title => "Button Widget - Basic Usage";
    public override string Description => "Demonstrates basic button creation and click handling";

    private class ButtonState
    {
        public int ClickCount { get; set; }
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating button basic example widget builder");

        var state = new ButtonState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Button Examples"),
                v.Text(""),
                v.Text($"Button clicked {state.ClickCount} times"),
                v.Text(""),
                v.Button("Click me!").OnClick(_ => state.ClickCount++),
                v.Text(""),
                v.Text("Press Tab to focus, Enter or Space to activate")
            ]);
        };
    }
}
