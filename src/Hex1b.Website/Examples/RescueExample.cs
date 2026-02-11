using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// An example demonstrating the RescueWidget error boundary.
/// </summary>
public class RescueExample(ILogger<RescueExample> logger) : Hex1bExample
{
    private readonly ILogger<RescueExample> _logger = logger;

    public override string Id => "rescue";
    public override string Title => "Rescue";
    public override string Description => "Error boundary widget that catches exceptions and shows fallback content.";

    /// <summary>
    /// State for the rescue example.
    /// </summary>
    private class RescueExampleState
    {
        private static readonly string[] ExampleIds = ["basic", "custom-fallback", "event-handlers"];
        
        public int SelectedExampleIndex { get; set; } = 0;
        public string SelectedExampleId => ExampleIds[SelectedExampleIndex];
        public int ErrorCount { get; set; }
        public int ResetCount { get; set; }
        
        public IReadOnlyList<string> ExampleItems { get; } =
        [
            "Basic Rescue",
            "Custom Fallback",
            "Event Handlers",
        ];
    }

    /// <summary>
    /// Creates a deeply nested exception with a long message and deep stack trace.
    /// </summary>
    private static Exception CreateStressTestException()
    {
        // Create a long, detailed error message
        var message = """
            Critical failure in DatabaseConnectionPoolManager.AcquireConnectionAsync(): 
            The connection pool has been exhausted after waiting 30000ms for an available connection. 
            Current pool statistics: Active=100, Idle=0, Pending=47, MaxPoolSize=100. 
            This error typically occurs when database operations are not properly disposed, 
            when there's a connection leak in long-running transactions, or when the application 
            is experiencing unusually high load. Consider increasing MaxPoolSize, implementing 
            connection timeout policies, or reviewing code for undisposed SqlConnection instances.
            """;

        try
        {
            Level1();
        }
        catch (Exception ex)
        {
            return new InvalidOperationException(message, ex);
        }
        
        return new InvalidOperationException(message);
    }
    
    // Methods to create a deep stack trace
    private static void Level1() => Level2();
    private static void Level2() => Level3();
    private static void Level3() => Level4();
    private static void Level4() => Level5();
    private static void Level5() => Level6();
    private static void Level6() => Level7();
    private static void Level7() => Level8();
    private static void Level8() => Level9();
    private static void Level9() => Level10();
    private static void Level10() => Level11();
    private static void Level11() => Level12();
    private static void Level12() => Level13();
    private static void Level13() => Level14();
    private static void Level14() => Level15();
    private static void Level15() => ThrowInnerException();
    
    private static void ThrowInnerException()
    {
        throw new TimeoutException(
            "Timeout expired. The timeout period elapsed prior to obtaining a connection from the pool. " +
            "This may have occurred because all pooled connections were in use and max pool size was reached."
        );
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating rescue example widget builder");

        var state = new RescueExampleState();

        return () =>
        {
            var ctx = new RootContext();

            var widget = ctx.HSplitter(
                ctx.VStack(left => [
                    left.Text("Rescue Examples"),
                    left.Text("─────────────────"),
                    left.List(state.ExampleItems).OnSelectionChanged(e => state.SelectedExampleIndex = e.SelectedIndex),
                    left.Text(""),
                    left.Text("RescueWidget is an"),
                    left.Text("error boundary that"),
                    left.Text("catches exceptions"),
                    left.Text("and shows fallback."),
                    left.Text(""),
                    left.Text($"Errors caught: {state.ErrorCount}"),
                    left.Text($"Resets: {state.ResetCount}"),
                ]),
                BuildExampleContent(ctx, state),
                leftWidth: 22
            );

            return widget;
        };
    }

    private static Hex1bWidget BuildExampleContent(RootContext ctx, RescueExampleState state)
    {
        return state.SelectedExampleId switch
        {
            "basic" => BuildBasicRescueExample(ctx, state),
            "custom-fallback" => BuildCustomFallbackExample(ctx, state),
            "event-handlers" => BuildEventHandlersExample(ctx, state),
            _ => BuildBasicRescueExample(ctx, state)
        };
    }

    private static Hex1bWidget BuildBasicRescueExample(RootContext ctx, RescueExampleState state)
    {
        return ctx.Rescue(v => [
            v.Border(b => [
                b.VStack(inner => [
                    inner.Text("Basic Rescue Example"),
                    inner.Text("════════════════════════════════════════"),
                    inner.Text(""),
                    inner.Text("This demonstrates the basic Rescue widget."),
                    inner.Text("When an error occurs, it shows a default"),
                    inner.Text("fallback UI with the exception details."),
                    inner.Text(""),
                    inner.Text("Features:"),
                    inner.Text("  • Catches exceptions from child widgets"),
                    inner.Text("  • Shows error type, message, stack trace"),
                    inner.Text("  • Provides a Retry button to recover"),
                    inner.Text("  • Uses rescue theme (red colors)"),
                    inner.Text(""),
                    inner.Text("Press the button to trigger an error:"),
                    inner.Text(""),
                    inner.Button("Trigger Error").OnClick(_ => throw CreateStressTestException()),
                ])
            ]).Title("Basic Rescue"),
        ])
        .OnRescue(e => state.ErrorCount++)
        .OnReset(_ => state.ResetCount++);
    }

    private static Hex1bWidget BuildCustomFallbackExample(RootContext ctx, RescueExampleState state)
    {
        return ctx.Rescue(v => [
            v.Border(b => [
                b.VStack(inner => [
                    inner.Text("Custom Fallback Example"),
                    inner.Text("════════════════════════════════════════"),
                    inner.Text(""),
                    inner.Text("This demonstrates a custom fallback UI."),
                    inner.Text("Use WithFallback() to provide your own"),
                    inner.Text("error display instead of the default."),
                    inner.Text(""),
                    inner.Text("The RescueContext gives you:"),
                    inner.Text("  • Exception - the error that occurred"),
                    inner.Text("  • ErrorPhase - when it happened"),
                    inner.Text("  • Reset() - to retry the operation"),
                    inner.Text(""),
                    inner.Text("Press the button to trigger an error:"),
                    inner.Text(""),
                    inner.Button("Trigger Error").OnClick(_ => throw new InvalidOperationException("Something went wrong!")),
                ])
            ]).Title("Custom Fallback"),
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
        .OnRescue(e => state.ErrorCount++)
        .OnReset(_ => state.ResetCount++);
    }

    private static Hex1bWidget BuildEventHandlersExample(RootContext ctx, RescueExampleState state)
    {
        return ctx.Rescue(v => [
            v.Border(b => [
                b.VStack(inner => [
                    inner.Text("Event Handlers Example"),
                    inner.Text("════════════════════════════════════════"),
                    inner.Text(""),
                    inner.Text("This demonstrates OnRescue and OnReset."),
                    inner.Text(""),
                    inner.Text("OnRescue is called when an error occurs."),
                    inner.Text("Use it for logging, telemetry, etc."),
                    inner.Text(""),
                    inner.Text("OnReset is called when user clicks Retry."),
                    inner.Text("Use it to reset state, reconnect, etc."),
                    inner.Text(""),
                    inner.Text("Watch the counters in the sidebar!"),
                    inner.Text(""),
                    inner.Text("Press the button to trigger an error:"),
                    inner.Text(""),
                    inner.Button("Trigger Error").OnClick(_ => throw new Exception("Test error for event handlers")),
                ])
            ]).Title("Event Handlers"),
        ])
        .OnRescue(e => {
            state.ErrorCount++;
            // In a real app: logger.LogError(e.Exception, "Error in {Phase}", e.Phase);
        })
        .OnReset(e => {
            state.ResetCount++;
            // In a real app: ResetApplicationState();
        });
    }
}
