using System.Collections.Specialized;
using Hex1b.Composition;
using Hex1b.Data;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Shared default (no-template) renderer for both <see cref="ListNode"/> and
/// templateless <see cref="ListNode{T}"/>. Keeps the visual contract of the
/// original list — themed selection indicator, selected background, hover
/// background — in one place.
/// </summary>
internal static class ListRenderCore
{
    private const string LoadingPlaceholder = "…";

    public static void RenderDefault<T>(ListNode<T> node, Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var selectedIndicator = theme.Get(ListTheme.SelectedIndicator);
        var unselectedIndicator = theme.Get(ListTheme.UnselectedIndicator);
        var selectedFg = theme.Get(ListTheme.SelectedForegroundColor);
        var selectedBg = theme.Get(ListTheme.SelectedBackgroundColor);
        var hoveredFg = theme.Get(ListTheme.HoveredForegroundColor);
        var hoveredBg = theme.Get(ListTheme.HoveredBackgroundColor);

        var globalColors = theme.GetGlobalColorCodes();
        var resetToGlobal = theme.GetResetToGlobalCodes();

        var hoveredItemIndex = node.IsHovered ? node.HoveredItemIndex : -1;

        var visibleStart = node.ScrollOffset;
        var visibleEnd = Math.Min(node.ScrollOffset + node.VisibleItemCount, node.EffectiveItemCount);

        // Multi-select glyph prefix (rendered between the cursor arrow and the
        // item text). Empty string when multi-select is off so the layout
        // remains unchanged for the common single-select case.
        var multiSelect = node.IsMultiSelectEnabled;
        var checkedGlyph = multiSelect ? theme.Get(ListTheme.CheckboxChecked) : string.Empty;
        var uncheckedGlyph = multiSelect ? theme.Get(ListTheme.CheckboxUnchecked) : string.Empty;

        for (int i = visibleStart; i < visibleEnd; i++)
        {
            var item = node.TryGetEffectiveItem(i, out var value)
                ? value?.ToString() ?? string.Empty
                : LoadingPlaceholder;
            var isSelected = i == node.FocusedIndex;
            var isHoveredItem = i == hoveredItemIndex;
            var checkbox = multiSelect
                ? (node.IsIndexSelected(i) ? checkedGlyph : uncheckedGlyph)
                : string.Empty;

            var x = node.Bounds.X;
            var y = node.Bounds.Y + (i - node.ScrollOffset);

            string text;
            if (isSelected && node.IsFocused)
            {
                text = $"{selectedFg.ToForegroundAnsi()}{selectedBg.ToBackgroundAnsi()}{selectedIndicator}{checkbox}{item}{resetToGlobal}";
            }
            else if (isHoveredItem && !isSelected)
            {
                text = $"{hoveredFg.ToForegroundAnsi()}{hoveredBg.ToBackgroundAnsi()}{unselectedIndicator}{checkbox}{item}{resetToGlobal}";
            }
            else if (isSelected)
            {
                text = $"{globalColors}{selectedIndicator}{checkbox}{item}{resetToGlobal}";
            }
            else
            {
                text = $"{globalColors}{unselectedIndicator}{checkbox}{item}{resetToGlobal}";
            }

            if (context.CurrentLayoutProvider != null)
            {
                context.WriteClipped(x, y, text);
            }
            else
            {
                context.SetCursorPosition(x, y);
                context.Write(text);
            }
        }
    }
}
