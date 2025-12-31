using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Rescue Widget Documentation: Event Handlers
/// Demonstrates OnRescue and OnReset event handlers.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the eventHandlersCode sample in:
/// src/content/guide/widgets/rescue.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class RescueEventHandlersExample(ILogger<RescueEventHandlersExample> logger) : Hex1bExample
{
    private readonly ILogger<RescueEventHandlersExample> _logger = logger;

    public override string Id => "rescue-event-handlers";
    public override string Title => "Rescue Widget - Event Handlers";
    public override string Description => "Demonstrates OnRescue and OnReset event handlers";

    private class EventState
    {
        public int ErrorCount { get; set; }
        public int ResetCount { get; set; }
        public bool ShouldThrow { get; set; }
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating rescue event handlers example widget builder");

        var state = new EventState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Event Handlers Demo"),
                v.Text(""),
                v.Text($"Errors caught: {state.ErrorCount}"),
                v.Text($"Resets triggered: {state.ResetCount}"),
                v.Text(""),
                v.Rescue(inner => [
                    // This will throw during reconcile when ShouldThrow is true
                    state.ShouldThrow
                        ? throw new Exception("Test error")
                        : inner.Text("Click the button to trigger an error."),
                    inner.Text("Watch the counters above update!"),
                    inner.Text(""),
                    inner.Button("Trigger Error").OnClick(_ => {
                        state.ShouldThrow = true;
                    })
                ])
                .OnRescue(e => {
                    state.ErrorCount++;
                    // In a real app: logger.LogError(e.Exception, "Error in {Phase}", e.Phase);
                })
                .OnReset(_ => {
                    state.ResetCount++;
                    state.ShouldThrow = false;
                    // In a real app: ResetApplicationState();
                })
            ]);
        };
    }
}
