using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Node representing a single tab item in a TabBar.
/// Used internally by TabBarNode to render individual tabs.
/// </summary>
public sealed class TabItemNode : Hex1bNode
{
    /// <summary>
    /// The tab title text.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Optional icon displayed before the title.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Whether this tab is currently selected.
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Whether this tab is disabled.
    /// </summary>
    public bool IsDisabled { get; set; }

    /// <summary>
    /// The index of this tab in the tab bar.
    /// </summary>
    public int TabIndex { get; set; }

    protected override Size MeasureCore(Constraints constraints)
    {
        // Tab renders as " [Icon] Title " with padding
        var textWidth = Title.Length;
        if (!string.IsNullOrEmpty(Icon))
        {
            textWidth += Icon.Length + 1; // icon + space
        }
        var width = textWidth + 2; // 1 char padding on each side
        var height = 1;
        return constraints.Constrain(new Size(width, height));
    }

    public override void Render(Hex1bRenderContext context)
    {
        // Rendering is handled by TabBarNode for proper styling
    }
}
