using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Button Widget Documentation: Counter Demo
/// Demonstrates multiple buttons with state management.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the counterCode sample in:
/// src/content/guide/widgets/button.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ButtonCounterExample(ILogger<ButtonCounterExample> logger) : Hex1bExample
{
    private readonly ILogger<ButtonCounterExample> _logger = logger;

    public override string Id => "button-counter";
    public override string Title => "Button Widget - Counter Demo";
    public override string Description => "Demonstrates multiple buttons controlling shared state";

    private class CounterState
    {
        public int Count { get; set; }
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating button counter example widget builder");

        var state = new CounterState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Border(b => [
                b.VStack(v => [
                    v.Text($"Count: {state.Count}"),
                    v.Text(""),
                    v.HStack(h => [
                        h.Button("- Decrement").OnClick(_ => state.Count--),
                        h.Text(" "),
                        h.Button("+ Increment").OnClick(_ => state.Count++)
                    ]),
                    v.Text(""),
                    v.Button("Reset").OnClick(_ => state.Count = 0)
                ])
            ], title: "Counter");
        };
    }
}
