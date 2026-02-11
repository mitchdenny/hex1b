using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Getting Started Step 2: Interactive Counter
/// Demonstrates state management with a simple counter.
/// </summary>
public class GettingStartedStep2Example(ILogger<GettingStartedStep2Example> logger) : Hex1bExample
{
    private readonly ILogger<GettingStartedStep2Example> _logger = logger;

    public override string Id => "getting-started-step2";
    public override string Title => "Getting Started - Step 2: Counter";
    public override string Description => "Interactive counter with state management";

    private class CounterState
    {
        public int Count { get; set; }
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating getting started step 2 widget builder");

        var state = new CounterState();

        return () =>
        {
            var ctx = new RootContext();

            return ctx.Border(b => [
                b.Text($"Button pressed {state.Count} times"),
                b.Text(""),
                b.Button("Click me!").OnClick(_ => state.Count++)
            ]).Title("Counter Demo");
        };
    }
}
