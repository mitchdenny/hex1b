using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A fallback widget that renders error information with hardcoded styling,
/// bypassing the theme system to avoid cascading failures when theming itself has errors.
/// This widget composes real widgets (Button, Scroll, VStack, Border) so that
/// focus navigation works normally through the focus ring.
/// </summary>
public sealed record RescueFallbackWidget(
    RescueState State, 
    bool ShowDetails,
    IReadOnlyList<RescueAction>? Actions = null) : Hex1bWidget
{
    // Hardcoded colors that don't rely on theming
    internal static readonly Hex1bColor BackgroundColor = Hex1bColor.FromRgb(40, 0, 0);       // Dark red background
    internal static readonly Hex1bColor BorderColor = Hex1bColor.FromRgb(255, 80, 80);        // Bright red border
    internal static readonly Hex1bColor TitleColor = Hex1bColor.FromRgb(255, 255, 255);       // White title
    internal static readonly Hex1bColor TextColor = Hex1bColor.FromRgb(255, 200, 200);        // Light red text
    internal static readonly Hex1bColor ErrorTypeColor = Hex1bColor.FromRgb(255, 255, 100);   // Yellow for exception type
    internal static readonly Hex1bColor StackTraceColor = Hex1bColor.FromRgb(180, 180, 180);  // Gray for stack trace
    internal static readonly Hex1bColor PhaseColor = Hex1bColor.FromRgb(100, 200, 255);       // Light blue for phase
    internal static readonly Hex1bColor ButtonFocusedBg = Hex1bColor.FromRgb(255, 80, 80);    // Bright red for focused button
    internal static readonly Hex1bColor ButtonFocusedFg = Hex1bColor.FromRgb(255, 255, 255);  // White text on focused button
    internal static readonly Hex1bColor ButtonNormalBg = Hex1bColor.FromRgb(80, 30, 30);      // Darker red for normal button
    internal static readonly Hex1bColor ButtonNormalFg = Hex1bColor.FromRgb(255, 200, 200);   // Light text on normal button

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        // Build widget tree using normal components
        var widgetTree = BuildWidgetTree();
        
        // Reconcile into a RescueFallbackContainerNode that sets hardcoded colors
        var node = existingNode as RescueFallbackContainerNode ?? new RescueFallbackContainerNode();
        node.Child = await context.ReconcileChildAsync(node.Child, widgetTree, node);
        
        // Invalidate focus cache since children may have changed
        node.InvalidateFocusCache();
        
        // Set initial focus only if this is a new node AND we're at the root or parent doesn't manage focus
        if (context.IsNew && !context.ParentManagesFocus())
        {
            node.SetInitialFocus();
        }
        
        return node;
    }
    
    private Hex1bWidget BuildWidgetTree()
    {
        var actions = Actions ?? [];
        
        // Build content lines as a list of widgets
        var contentWidgets = new List<Hex1bWidget>();
        
        if (ShowDetails && State.Exception != null)
        {
            // Message section
            contentWidgets.Add(new TextBlockWidget("Message:"));
            contentWidgets.Add(new TextBlockWidget(""));
            contentWidgets.Add(new TextBlockWidget("  " + (State.Exception.Message ?? "(no message)"), TextOverflow.Wrap));
            contentWidgets.Add(new TextBlockWidget(""));
            
            // Stack trace section
            if (State.Exception.StackTrace != null)
            {
                contentWidgets.Add(new TextBlockWidget("Stack Trace:"));
                contentWidgets.Add(new TextBlockWidget(""));
                
                var stackLines = State.Exception.StackTrace.Split('\n');
                foreach (var stackLine in stackLines)
                {
                    var trimmed = stackLine.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        contentWidgets.Add(new TextBlockWidget("  " + trimmed, TextOverflow.Wrap));
                    }
                }
            }
            
            // Inner exception
            if (State.Exception.InnerException != null)
            {
                contentWidgets.Add(new TextBlockWidget(""));
                contentWidgets.Add(new TextBlockWidget($"Inner Exception: {State.Exception.InnerException.GetType().Name}"));
                contentWidgets.Add(new TextBlockWidget(""));
                contentWidgets.Add(new TextBlockWidget("  " + (State.Exception.InnerException.Message ?? "(no message)"), TextOverflow.Wrap));
                
                if (State.Exception.InnerException.StackTrace != null)
                {
                    contentWidgets.Add(new TextBlockWidget(""));
                    contentWidgets.Add(new TextBlockWidget("Inner Stack Trace:"));
                    contentWidgets.Add(new TextBlockWidget(""));
                    
                    var innerStackLines = State.Exception.InnerException.StackTrace.Split('\n');
                    foreach (var stackLine in innerStackLines)
                    {
                        var trimmed = stackLine.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            contentWidgets.Add(new TextBlockWidget("  " + trimmed, TextOverflow.Wrap));
                        }
                    }
                }
            }
        }
        else
        {
            // Friendly message for production
            contentWidgets.Add(new TextBlockWidget(""));
            contentWidgets.Add(new TextBlockWidget("Something went wrong."));
            contentWidgets.Add(new TextBlockWidget(""));
            contentWidgets.Add(new TextBlockWidget("The application encountered an unexpected error."));
            contentWidgets.Add(new TextBlockWidget(""));
            contentWidgets.Add(new TextBlockWidget("Please try again or contact support if the"));
            contentWidgets.Add(new TextBlockWidget("problem persists."));
            contentWidgets.Add(new TextBlockWidget(""));
            contentWidgets.Add(new TextBlockWidget($"Error ID: {Guid.NewGuid():N}"[..36]));
        }
        
        // Build header section
        var headerWidgets = new List<Hex1bWidget>
        {
            new TextBlockWidget(ShowDetails ? "⚠ UNHANDLED EXCEPTION ⚠" : "⚠ APPLICATION ERROR ⚠")
        };
        
        if (ShowDetails && State.Exception != null)
        {
            headerWidgets.Add(new TextBlockWidget($"Phase: {State.ErrorPhase}"));
            headerWidgets.Add(new TextBlockWidget($"Type:  {State.Exception.GetType().FullName}", TextOverflow.Ellipsis));
        }
        
        // Build button section
        var buttonWidgets = new List<Hex1bWidget>();
        foreach (var action in actions)
        {
            buttonWidgets.Add(new RescueButtonWidget(action.Label, action.Action));
        }
        
        // Build main VStack children
        var mainChildren = new List<Hex1bWidget>();
        
        // Header
        mainChildren.AddRange(headerWidgets);
        
        // Separator (SeparatorWidget auto-detects orientation from VStack parent)
        mainChildren.Add(new SeparatorWidget());
        
        // Buttons row (HStack) if present
        if (buttonWidgets.Count > 0)
        {
            mainChildren.Add(new HStackWidget([..buttonWidgets]));
            mainChildren.Add(new SeparatorWidget());
        }
        
        // Scrollable content - use Fill() to take remaining space
        mainChildren.Add(new ScrollWidget(
            new VStackWidget([..contentWidgets])
        ).Fill());
        
        var mainContent = new VStackWidget([..mainChildren]);
        
        // Wrap in border
        return new RescueBorderWidget(mainContent);
    }

    internal override Type GetExpectedNodeType() => typeof(RescueFallbackContainerNode);
}
