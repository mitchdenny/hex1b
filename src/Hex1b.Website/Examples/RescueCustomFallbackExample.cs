using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Rescue Widget Documentation: Custom Fallback
/// Demonstrates custom fallback UI using WithFallback.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the customFallbackCode sample in:
/// src/content/guide/widgets/rescue.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class RescueCustomFallbackExample(ILogger<RescueCustomFallbackExample> logger) : Hex1bExample
{
    private readonly ILogger<RescueCustomFallbackExample> _logger = logger;

    public override string Id => "rescue-custom-fallback";
    public override string Title => "Rescue Widget - Custom Fallback";
    public override string Description => "Demonstrates custom fallback UI using WithFallback";

    private class ExampleState
    {
        public bool ShouldThrow { get; set; }
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating rescue custom fallback example widget builder");

        var state = new ExampleState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Rescue(v => [
                // This will throw during reconcile when ShouldThrow is true
                state.ShouldThrow
                    ? throw new InvalidOperationException("Something went wrong!")
                    : v.Text("Custom Fallback Demo"),
                v.Text(""),
                v.Text("This uses WithFallback() to provide"),
                v.Text("a custom error UI instead of the default."),
                v.Text(""),
                v.Button("Trigger Error").OnClick(_ => {
                    state.ShouldThrow = true;
                })
            ])
            .WithFallback(rescue => rescue.Border(b => [
                b.VStack(inner => [
                    inner.Text("🔥 Custom Error Handler 🔥"),
                    inner.Text(""),
                    inner.Text($"Error Type: {rescue.Exception.GetType().Name}"),
                    inner.Text($"Phase: {rescue.ErrorPhase}"),
                    inner.Text(""),
                    inner.Text("Message:"),
                    inner.Text($"  {rescue.Exception.Message}"),
                    inner.Text(""),
                    inner.Button("🔄 Try Again").OnClick(_ => rescue.Reset()),
                ])
            ]).Title("Oops!"))
            .OnReset(_ => state.ShouldThrow = false);
        };
    }
}
