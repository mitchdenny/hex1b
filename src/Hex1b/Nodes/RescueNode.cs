using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A node that catches exceptions and displays a fallback when errors occur.
/// </summary>
public sealed class RescueNode : Hex1bNode
{
    /// <summary>
    /// The main child node (may throw during lifecycle methods).
    /// </summary>
    public Hex1bNode? Child { get; set; }
    
    /// <summary>
    /// The fallback child node (shown when an error occurs).
    /// </summary>
    public Hex1bNode? FallbackChild { get; set; }
    
    /// <summary>
    /// The state for tracking error status.
    /// </summary>
    public RescueState State { get; set; } = new();
    
    /// <summary>
    /// Optional custom fallback widget builder.
    /// </summary>
    public Func<RescueState, Hex1bWidget>? FallbackBuilder { get; set; }
    
    /// <summary>
    /// Whether to show detailed exception information.
    /// </summary>
    public bool ShowDetails { get; set; }
    
    /// <summary>
    /// Actions available to the user in the fallback view.
    /// </summary>
    public IReadOnlyList<RescueAction> Actions { get; set; } = [];

    /// <summary>
    /// Gets the active child (either main child or fallback, depending on error state).
    /// </summary>
    private Hex1bNode? ActiveChild => State.HasError ? FallbackChild : Child;

    public override Size Measure(Constraints constraints)
    {
        if (State.HasError)
        {
            return FallbackChild?.Measure(constraints) ?? constraints.Constrain(Size.Zero);
        }
        
        try
        {
            return Child?.Measure(constraints) ?? constraints.Constrain(Size.Zero);
        }
        catch (Exception ex)
        {
            CaptureError(ex, RescueErrorPhase.Measure);
            EnsureFallbackNode();
            return FallbackChild?.Measure(constraints) ?? constraints.Constrain(Size.Zero);
        }
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);
        
        if (State.HasError)
        {
            FallbackChild?.Arrange(bounds);
            return;
        }
        
        try
        {
            Child?.Arrange(bounds);
        }
        catch (Exception ex)
        {
            CaptureError(ex, RescueErrorPhase.Arrange);
            EnsureFallbackNode();
            FallbackChild?.Arrange(bounds);
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (State.HasError)
        {
            FallbackChild?.Render(context);
            return;
        }
        
        try
        {
            Child?.Render(context);
        }
        catch (Exception ex)
        {
            CaptureError(ex, RescueErrorPhase.Render);
            EnsureFallbackNode();
            
            // Re-measure and arrange the fallback, then render it
            if (FallbackChild != null)
            {
                FallbackChild.Measure(new Constraints(0, Bounds.Width, 0, Bounds.Height));
                FallbackChild.Arrange(Bounds);
                FallbackChild.Render(context);
            }
        }
    }

    private void CaptureError(Exception ex, RescueErrorPhase phase)
    {
        State.HasError = true;
        State.Exception = ex;
        State.ErrorPhase = phase;
    }

    private void EnsureFallbackNode()
    {
        if (FallbackChild != null) return;
        
        // Build the fallback widget and create a node manually
        var fallbackWidget = BuildFallback();
        
        // Simple reconciliation for the fallback (sync wait since this is error path)
        var context = ReconcileContext.CreateRoot();
        FallbackChild = context.ReconcileChildAsync(null, fallbackWidget, this).GetAwaiter().GetResult();
    }

    private Hex1bWidget BuildFallback()
    {
        if (FallbackBuilder != null)
        {
            return FallbackBuilder(State);
        }
        
        return BuildDefaultFallback(State, ShowDetails, Actions);
    }

    /// <summary>
    /// Builds the default fallback widget for displaying error information.
    /// Uses RescueFallbackWidget with hardcoded colors to avoid theme-related failures.
    /// </summary>
    /// <param name="state">The rescue state containing error information.</param>
    /// <param name="showDetails">Whether to show detailed exception information.</param>
    /// <param name="actions">Optional actions available to the user.</param>
    internal static Hex1bWidget BuildDefaultFallback(
        RescueState state, 
        bool showDetails = 
#if DEBUG
        true
#else
        false
#endif
        ,
        IReadOnlyList<RescueAction>? actions = null
    ) => new RescueFallbackWidget(state, showDetails, actions);

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        var active = ActiveChild;
        if (active != null)
        {
            foreach (var focusable in active.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        var active = ActiveChild;
        if (active != null) yield return active;
    }
}
