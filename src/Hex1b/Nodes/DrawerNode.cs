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
    /// The content node (either collapsed or expanded content).
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
    /// The clip mode for the drawer's content. Defaults to Clip.
    /// </summary>
    public ClipMode ClipMode { get; set; } = ClipMode.Clip;

    public override bool IsFocusable => false;

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
    }
    
    private void OnClick()
    {
        if (!IsExpanded)
        {
            IsExpanded = true;
            ExpandedAction?.Invoke();
        }
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
