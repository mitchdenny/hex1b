using Hex1b;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Exhibits;

/// <summary>
/// An exhibit demonstrating the RescueWidget error boundary.
/// </summary>
public class RescueExhibit(ILogger<RescueExhibit> logger) : Hex1bExhibit
{
    private readonly ILogger<RescueExhibit> _logger = logger;

    public override string Id => "rescue";
    public override string Title => "Rescue";
    public override string Description => "Error boundary widget that catches exceptions and shows fallback content.";

    /// <summary>
    /// State for the rescue exhibit.
    /// </summary>
    private class RescueExhibitState
    {
        private static readonly string[] ExampleIds = ["global", "local"];
        
        public int SelectedExampleIndex { get; set; } = 0;
        public string SelectedExampleId => ExampleIds[SelectedExampleIndex];
        public RescueState LocalRescueState { get; } = new();
        public RescueState GlobalRescueState { get; } = new();
        public bool TriggerLocalError { get; set; }
        public bool TriggerGlobalError { get; set; }
        
        public IReadOnlyList<string> ExampleItems { get; } =
        [
            "Global Rescue",
            "Local Rescue",
        ];
        
        public void ResetLocal()
        {
            LocalRescueState.Reset();
            TriggerLocalError = false;
        }
        
        public void ResetGlobal()
        {
            GlobalRescueState.Reset();
            TriggerGlobalError = false;
        }
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
            Additional diagnostic information: Server=db-prod-cluster-07.internal.example.com:5432, 
            Database=hex1b_production, User=app_service_account, SSL=Required, 
            ApplicationName=Hex1b.Website, ClientEncoding=UTF8, Timezone=UTC, 
            LastSuccessfulConnection=2024-12-16T14:23:45.123Z, 
            ConnectionAttempts=147, FailedAttempts=47, AverageWaitTime=12453ms
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
            "This may have occurred because all pooled connections were in use and max pool size was reached. " +
            "Stack overflow in connection retry logic detected. Recursive retry attempt #47 exceeded maximum depth."
        );
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating rescue exhibit widget builder");

        var state = new RescueExhibitState();

        return () =>
        {
            var ctx = new RootContext();
            
            // Check if global error should be triggered - this throws to the app-level rescue
            if (state.TriggerGlobalError && !state.GlobalRescueState.HasError)
            {
                state.GlobalRescueState.SetError(CreateStressTestException(), RescueErrorPhase.Build);
            }
            
            // If global error is active, show full-screen rescue fallback
            if (state.GlobalRescueState.HasError)
            {
                var globalActions = new List<RescueAction>
                {
                    new("Restart Exhibit", state.ResetGlobal),
                    new("View Logs", () => { }),
                    new("Report Issue", () => { }),
                    new("Ignore", () => { })
                };
                return new RescueFallbackWidget(state.GlobalRescueState, ShowDetails: true, Actions: globalActions);
            }

            var widget = ctx.Splitter(
                ctx.Panel(leftPanel => [
                    leftPanel.VStack(left => [
                        left.Text("Rescue Examples"),
                        left.Text("─────────────────"),
                        left.List(state.ExampleItems, e => state.SelectedExampleIndex = e.SelectedIndex, null),
                        left.Text(""),
                        left.Text("RescueWidget is an"),
                        left.Text("error boundary that"),
                        left.Text("catches exceptions"),
                        left.Text("and shows fallback."),
                    ])
                ]),
                BuildExampleContent(ctx, state),
                leftWidth: 22
            );

            return widget;
        };
    }

    private static Hex1bWidget BuildExampleContent(RootContext ctx, RescueExhibitState state)
    {
        return state.SelectedExampleId switch
        {
            "global" => BuildGlobalRescueExample(ctx, state),
            "local" => BuildLocalRescueExample(ctx, state),
            _ => BuildGlobalRescueExample(ctx, state)
        };
    }

    private static Hex1bWidget BuildGlobalRescueExample(RootContext ctx, RescueExhibitState state)
    {
        return ctx.Border(
            ctx.VStack(v => [
                v.Text("Global Rescue (Full Screen)"),
                v.Text("════════════════════════════════════════"),
                v.Text(""),
                v.Text("This demonstrates the app-level rescue that"),
                v.Text("replaces the ENTIRE screen when triggered."),
                v.Text(""),
                v.Text("The global rescue catches exceptions that"),
                v.Text("escape from the widget builder, protecting"),
                v.Text("the entire application from crashes."),
                v.Text(""),
                v.Text("Features demonstrated:"),
                v.Text("  • Full-screen error display"),
                v.Text("  • Long error message with scrolling"),
                v.Text("  • Deep stack trace (15+ levels)"),
                v.Text("  • Multiple action buttons"),
                v.Text("  • Hardcoded colors (theme-safe)"),
                v.Text(""),
                v.Text("Press the button to trigger a catastrophic"),
                v.Text("error that replaces this entire view:"),
                v.Text(""),
                ctx.Button("Trigger Global Rescue", _ => { state.TriggerGlobalError = true; }),
            ]),
            "Global Rescue"
        );
    }

    private static Hex1bWidget BuildLocalRescueExample(RootContext ctx, RescueExhibitState state)
    {
        // Create actions for the local rescue widget
        var localActions = new List<RescueAction>
        {
            new("Retry", state.ResetLocal),
            new("Ignore", () => { })
        };
        
        // If error was triggered, set it on the rescue state
        if (state.TriggerLocalError && !state.LocalRescueState.HasError)
        {
            state.LocalRescueState.SetError(CreateStressTestException(), RescueErrorPhase.Render);
        }
        
        // Normal content - show button to trigger error
        var normalContent = ctx.Border(
            ctx.VStack(v => [
                v.Text("Local Rescue (Panel Only)"),
                v.Text("════════════════════════════════════════"),
                v.Text(""),
                v.Text("This demonstrates a LOCAL rescue that only"),
                v.Text("affects this panel - the sidebar remains"),
                v.Text("visible and functional."),
                v.Text(""),
                v.Text("Local rescues are useful for:"),
                v.Text("  • Isolating failures to specific areas"),
                v.Text("  • Keeping navigation accessible"),
                v.Text("  • Graceful degradation of features"),
                v.Text(""),
                v.Text("Features demonstrated:"),
                v.Text("  • Contained error display"),
                v.Text("  • Long error message with wrapping"),
                v.Text("  • Deep stack trace (15+ levels)"),
                v.Text("  • Action buttons for recovery"),
                v.Text(""),
                v.Text("Press the button to trigger an error"),
                v.Text("confined to this panel:"),
                v.Text(""),
                ctx.Button("Trigger Local Rescue", _ => { state.TriggerLocalError = true; }),
            ]),
            "Local Rescue"
        );
        
        // Wrap in a RescueWidget - it will show fallback when state has error
        return new RescueWidget(
            child: normalContent,
            state: state.LocalRescueState,
            actions: localActions
        );
    }
}
