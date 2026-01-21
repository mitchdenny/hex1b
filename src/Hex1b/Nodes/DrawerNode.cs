using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="DrawerWidget"/>.
/// Manages expansion state, measures/arranges content, and handles input.
/// </summary>
public sealed class DrawerNode : Hex1bNode, ILayoutProvider
{
    /// <summary>
    /// The content node (either collapsed or expanded content for inline mode).
    /// </summary>
    public Hex1bNode? Content { get; set; }
    
    private bool _isExpanded;
    
    /// <summary>
    /// Whether the drawer is currently expanded.
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                MarkDirty();
            }
        }
    }
    
    /// <summary>
    /// The direction the drawer expands toward.
    /// </summary>
    public DrawerDirection Direction { get; set; } = DrawerDirection.Right;
    
    /// <summary>
    /// The current rendering mode (Inline or Overlay).
    /// </summary>
    public DrawerMode Mode { get; set; } = DrawerMode.Inline;
    
    /// <summary>
    /// Action to invoke when the drawer expands.
    /// </summary>
    internal Action? ExpandedAction { get; set; }
    
    /// <summary>
    /// Action to invoke when the drawer collapses.
    /// </summary>
    internal Action? CollapsedAction { get; set; }
    
    /// <summary>
    /// The expanded content builder (stored for overlay mode lazy building).
    /// </summary>
    internal Func<WidgetContext<DrawerWidget>, IEnumerable<Hex1bWidget>>? ExpandedContentBuilder { get; set; }
    
    /// <summary>
    /// The clip mode for the drawer's content. Defaults to Clip.
    /// </summary>
    public ClipMode ClipMode { get; set; } = ClipMode.Clip;
    
    /// <summary>
    /// Tracks if we have an active popup entry so we don't push duplicates.
    /// </summary>
    internal PopupEntry? ActivePopupEntry { get; set; }

    public override bool IsFocusable => Mode == DrawerMode.Overlay && !IsExpanded;

    #region ILayoutProvider Implementation
    
    /// <summary>
    /// The clip rectangle for child content.
    /// </summary>
    public Rect ClipRect => Bounds;
    
    /// <inheritdoc />
    public ILayoutProvider? ParentLayoutProvider { get; set; }

    public bool ShouldRenderAt(int x, int y) => LayoutProviderHelper.ShouldRenderAt(this, x, y);

    public (int adjustedX, string clippedText) ClipString(int x, int y, string text)
        => LayoutProviderHelper.ClipString(this, x, y, text);
    
    #endregion

    public override Size Measure(Constraints constraints)
    {
        if (Content == null)
        {
            // Invisible drawer (no collapsed content)
            return Size.Zero;
        }
        
        // Measure the content
        var contentSize = Content.Measure(constraints);
        return constraints.Constrain(contentSize);
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);
        
        if (Content != null)
        {
            Content.Arrange(bounds);
        }
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        // In overlay mode when collapsed, the drawer itself is focusable
        if (IsFocusable)
        {
            yield return this;
        }
        
        if (Content != null)
        {
            foreach (var focusable in Content.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (Content == null) return;
        
        var previousLayout = context.CurrentLayoutProvider;
        ParentLayoutProvider = previousLayout;
        context.CurrentLayoutProvider = this;
        
        context.SetCursorPosition(Content.Bounds.X, Content.Bounds.Y);
        Content.Render(context);
        
        context.CurrentLayoutProvider = previousLayout;
        ParentLayoutProvider = null;
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Click on collapsed drawer expands it
        bindings.Mouse(MouseButton.Left).Action(OnClick, "Toggle drawer");
        // Enter/Space on focused drawer also expands it
        bindings.Key(Hex1bKey.Enter).Action(OnClick, "Open drawer");
        bindings.Key(Hex1bKey.Spacebar).Action(OnClick, "Open drawer");
    }
    
    private Task OnClick(InputBindingActionContext ctx)
    {
        if (!IsExpanded && Mode == DrawerMode.Overlay && ExpandedContentBuilder != null)
        {
            // Overlay mode: push popup
            IsExpanded = true;
            ExpandedAction?.Invoke();
            
            // Find the popup host by walking up from this node (not from FocusedNode)
            var popupHost = FindPopupHost();
            if (popupHost == null)
            {
                // No popup host found - can't show overlay
                return Task.CompletedTask;
            }
            
            // Determine anchor position based on direction
            var anchorPosition = Direction switch
            {
                DrawerDirection.Right => AnchorPosition.Right,
                DrawerDirection.Left => AnchorPosition.Left,
                DrawerDirection.Down => AnchorPosition.Below,
                DrawerDirection.Up => AnchorPosition.Above,
                _ => AnchorPosition.Below
            };
            
            var builder = ExpandedContentBuilder;
            
            ActivePopupEntry = popupHost.Popups.PushAnchored(
                this, 
                anchorPosition, 
                () => 
                {
                    var widgetContext = new WidgetContext<DrawerWidget>();
                    var expandedWidgets = builder(widgetContext).ToList();
                    return new VStackWidget(expandedWidgets);
                },
                focusRestoreNode: this,
                onDismiss: () =>
                {
                    ActivePopupEntry = null;
                    _isExpanded = false;
                    CollapsedAction?.Invoke();
                    MarkDirty();
                }
            );
            
            // Focus will be set to first focusable in popup by ZStackWidget reconciler
            // (it handles focus management for new popups automatically)
        }
        else if (!IsExpanded)
        {
            // Inline mode: just expand
            IsExpanded = true;
            ExpandedAction?.Invoke();
        }
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Finds the nearest IPopupHost ancestor.
    /// </summary>
    private IPopupHost? FindPopupHost()
    {
        var current = Parent;
        while (current != null)
        {
            if (current is IPopupHost host)
            {
                return host;
            }
            current = current.Parent;
        }
        return null;
    }
    
    /// <summary>
    /// Collapses the drawer (called by collapse button).
    /// </summary>
    public void Collapse()
    {
        if (IsExpanded)
        {
            IsExpanded = false;
            CollapsedAction?.Invoke();
        }
    }
    
    /// <summary>
    /// Expands the drawer.
    /// </summary>
    public void Expand()
    {
        if (!IsExpanded)
        {
            IsExpanded = true;
            ExpandedAction?.Invoke();
        }
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Content != null) yield return Content;
    }
}
