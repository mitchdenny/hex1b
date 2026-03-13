using Hex1b.Input;
using Hex1b.Layout;

namespace Hex1b.Nodes;

/// <summary>
/// A lightweight focusable node that overlays a link region within a
/// <see cref="MarkdownTextBlockNode"/>. It renders nothing visible — the parent
/// text block handles link rendering. Its purpose is to participate in focus
/// navigation (Tab/Shift+Tab) and provide correct <see cref="Hex1bNode.Bounds"/>
/// so the ancestor <see cref="ScrollPanelNode"/> can auto-scroll to the
/// focused link.
/// </summary>
internal sealed class MarkdownLinkRegionNode : Hex1bNode
{
    /// <summary>
    /// Action ID for the link activation action (Enter key).
    /// </summary>
    public static readonly ActionId Activate
        = new($"{nameof(MarkdownLinkRegionNode)}.{nameof(Activate)}");

    /// <summary>
    /// The URL this link region points to.
    /// </summary>
    public string Url { get; set; } = "";

    /// <summary>
    /// The visible text of the link.
    /// </summary>
    public string LinkText { get; set; } = "";

    /// <summary>
    /// The link's unique ID within the parent text block (sequential index).
    /// </summary>
    public int LinkId { get; set; } = -1;

    /// <summary>
    /// Line index within the parent text block where this link region starts.
    /// Set by the parent during MeasureCore.
    /// </summary>
    public int LineIndex { get; set; }

    /// <summary>
    /// Column offset within the parent text block where this link region starts.
    /// Set by the parent during MeasureCore.
    /// </summary>
    public int ColumnOffset { get; set; }

    /// <summary>
    /// Display width of this link region.
    /// Set by the parent during MeasureCore.
    /// </summary>
    public int LinkDisplayWidth { get; set; }

    /// <summary>
    /// Callback invoked when the link is activated (Enter key or mouse click).
    /// </summary>
    public Func<InputBindingActionContext, Task>? ActivateCallback { get; set; }

    public override bool IsFocusable => true;

    private bool _isFocused;

    public override bool IsFocused
    {
        get => _isFocused;
        set
        {
            if (_isFocused != value)
            {
                _isFocused = value;
                MarkDirty();
                // Also mark the parent dirty so it re-renders the focus highlight
                Parent?.MarkDirty();
            }
        }
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        if (ActivateCallback != null)
        {
            bindings.Key(Hex1bKey.Enter)
                .Triggers(Activate, ActivateCallback, "Activate link");
            bindings.Mouse(MouseButton.Left)
                .Triggers(Activate, ActivateCallback, "Click link");
        }

        MarkdownScrollHelper.AddScrollBindings(this, bindings);
    }

    /// <summary>
    /// This node is sized and positioned by its parent text block.
    /// </summary>
    protected override Size MeasureCore(Constraints constraints)
        => new(0, 0);

    /// <summary>
    /// This node renders nothing — the parent text block handles link rendering.
    /// </summary>
    public override void Render(Hex1bRenderContext context)
    {
    }
}
