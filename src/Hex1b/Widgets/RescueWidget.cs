using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that catches exceptions and displays a fallback when errors occur.
/// Similar to React's ErrorBoundary pattern.
/// </summary>
public sealed record RescueWidget : Hex1bWidget
{
    /// <summary>
    /// The child widget to render (may throw during any lifecycle phase).
    /// </summary>
    public Hex1bWidget Child { get; }
    
    /// <summary>
    /// The state for tracking error status.
    /// </summary>
    public RescueState State { get; }
    
    /// <summary>
    /// Optional custom fallback widget builder. Receives the exception for display.
    /// If null, a default fallback is used.
    /// </summary>
    public Func<RescueState, Hex1bWidget>? FallbackBuilder { get; init; }
    
    /// <summary>
    /// Whether to show detailed exception information (stack trace, etc.).
    /// Defaults to true in DEBUG builds, false in RELEASE builds.
    /// </summary>
    public bool ShowDetails { get; init; }
    
    /// <summary>
    /// Actions available to the user in the fallback view.
    /// These appear as buttons at the bottom of the error display.
    /// </summary>
    public IReadOnlyList<RescueAction> Actions { get; init; } = [];

    /// <summary>
    /// Creates a new RescueWidget.
    /// </summary>
    /// <param name="child">The child widget to render.</param>
    /// <param name="state">Optional state for tracking errors. If null, a new state is created.</param>
    /// <param name="fallbackBuilder">Optional custom fallback builder.</param>
    /// <param name="showDetails">Whether to show detailed exception info. Defaults based on build configuration.</param>
    /// <param name="actions">Optional actions to display in the fallback.</param>
    public RescueWidget(
        Hex1bWidget child,
        RescueState? state = null,
        Func<RescueState, Hex1bWidget>? fallbackBuilder = null,
        bool? showDetails = null,
        IReadOnlyList<RescueAction>? actions = null)
    {
        Child = child;
        State = state ?? new RescueState();
        FallbackBuilder = fallbackBuilder;
        Actions = actions ?? [];
#if DEBUG
        ShowDetails = showDetails ?? true;
#else
        ShowDetails = showDetails ?? false;
#endif
    }

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as RescueNode ?? new RescueNode();
        node.State = State;
        node.FallbackBuilder = FallbackBuilder;
        node.ShowDetails = ShowDetails;
        node.Actions = Actions;
        
        // If we're already in error state, don't try to reconcile the child
        if (State.HasError)
        {
            // Build and reconcile the fallback instead
            var fallbackWidget = BuildFallback();
            node.FallbackChild = await context.ReconcileChildAsync(node.FallbackChild, fallbackWidget, node);
            return node;
        }
        
        try
        {
            node.Child = await context.ReconcileChildAsync(node.Child, Child, node);
            node.FallbackChild = null; // Clear any previous fallback
        }
        catch (Exception ex)
        {
            State.HasError = true;
            State.Exception = ex;
            State.ErrorPhase = RescueErrorPhase.Reconcile;
            
            // Build fallback
            var fallbackWidget = BuildFallback();
            node.FallbackChild = await context.ReconcileChildAsync(node.FallbackChild, fallbackWidget, node);
        }
        
        return node;
    }

    private Hex1bWidget BuildFallback()
    {
        if (FallbackBuilder != null)
        {
            return FallbackBuilder(State);
        }
        
        return new RescueFallbackWidget(State, ShowDetails, Actions);
    }

    internal override Type GetExpectedNodeType() => typeof(RescueNode);
}
