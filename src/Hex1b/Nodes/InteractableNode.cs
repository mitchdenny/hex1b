using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A node that makes its child tree focusable, clickable, and hoverable as a single unit.
/// The node itself is the focus target — children are excluded from the focus ring.
/// Renders no chrome of its own; layout and rendering pass through to the child.
/// </summary>
public sealed class InteractableNode : Hex1bNode
{
    /// <summary>
    /// The single child node.
    /// </summary>
    public Hex1bNode? Child { get; set; }

    /// <summary>
    /// The source widget that was reconciled into this node.
    /// </summary>
    public InteractableWidget? SourceWidget { get; set; }

    /// <summary>
    /// The async action to execute when the interactable is activated (Enter, Space, or mouse click).
    /// </summary>
    public Func<InputBindingActionContext, Task>? ClickAction { get; set; }

    /// <summary>
    /// Callback invoked when focus state changes. Synchronous because it fires from a property setter.
    /// </summary>
    public Action<bool>? FocusChangedAction { get; set; }

    /// <summary>
    /// Callback invoked when hover state changes. Synchronous because it fires from a property setter.
    /// </summary>
    public Action<bool>? HoverChangedAction { get; set; }

    private bool _isFocused;
    public override bool IsFocused
    {
        get => _isFocused;
        set
        {
            if (_isFocused != value)
            {
                _isFocused = value;
                FocusChangedAction?.Invoke(value);
                MarkDirty();
            }
        }
    }

    private bool _isHovered;
    public override bool IsHovered
    {
        get => _isHovered;
        set
        {
            if (_isHovered != value)
            {
                _isHovered = value;
                HoverChangedAction?.Invoke(value);
                MarkDirty();
            }
        }
    }

    public override bool IsFocusable => true;

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        if (ClickAction != null)
        {
            bindings.Key(Hex1bKey.Enter).Action(ClickAction, "Activate");
            bindings.Key(Hex1bKey.Spacebar).Action(ClickAction, "Activate");
            bindings.Mouse(MouseButton.Left).Action(ClickAction, "Click");
        }
    }

    public override Size Measure(Constraints constraints)
    {
        if (Child == null)
        {
            return constraints.Constrain(Size.Zero);
        }

        return Child.Measure(constraints);
    }

    public override void Arrange(Rect rect)
    {
        base.Arrange(rect);
        Child?.Arrange(rect);
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (Child != null)
        {
            context.RenderChild(Child);
        }
    }

    /// <summary>
    /// Hit test bounds cover the entire arranged area.
    /// </summary>
    public override Rect HitTestBounds => Bounds;

    /// <summary>
    /// Returns the child for tree traversal and input routing.
    /// </summary>
    public override IReadOnlyList<Hex1bNode> GetChildren()
    {
        return Child != null ? [Child] : [];
    }

    // GetFocusableNodes is NOT overridden — base yields [this] since IsFocusable is true.
    // Children are excluded from the focus ring.
}
