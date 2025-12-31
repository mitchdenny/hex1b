using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Rescue Widget Documentation: Basic Usage
/// Demonstrates basic error boundary with default fallback.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/rescue.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class RescueBasicExample(ILogger<RescueBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<RescueBasicExample> _logger = logger;

    public override string Id => "rescue-basic";
    public override string Title => "Rescue Widget - Basic Usage";
    public override string Description => "Demonstrates basic error boundary with default fallback";

    private class ExampleState
    {
        public bool ShouldThrow { get; set; }
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating rescue basic example widget builder");

        var state = new ExampleState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Rescue(v => [
                // This will throw during reconcile when ShouldThrow is true
                state.ShouldThrow 
                    ? throw new InvalidOperationException("Oops! Something went wrong.")
                    : v.Text("Application content here"),
                v.Text(""),
                v.Text("Click the button to trigger an error."),
                v.Text("The Rescue widget will catch it and show"),
                v.Text("a fallback UI with error details."),
                v.Text(""),
                v.Button("Click me").OnClick(_ => {
                    // Set state to trigger error on next render
                    state.ShouldThrow = true;
                })
            ])
            .OnReset(_ => state.ShouldThrow = false);
        };
    }
}
