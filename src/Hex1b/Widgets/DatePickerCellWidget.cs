using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Internal widget for a date picker grid cell (year or month).
/// Renders themed text with hover/selected/current day styling via
/// <see cref="Hex1b.Theming.DatePickerTheme"/>.
/// </summary>
internal sealed record DatePickerCellWidget(string Label, bool IsSelected, bool IsCurrent, bool IsCellFocused, bool IsCellHovered) : Hex1bWidget
{
    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as DatePickerCellNode ?? new DatePickerCellNode();
        node.Label = Label;
        node.IsSelected = IsSelected;
        node.IsCurrent = IsCurrent;
        node.IsCellFocused = IsCellFocused;
        node.IsCellHovered = IsCellHovered;
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(DatePickerCellNode);
}
