using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Context passed to an <c>ItemTemplate</c> callback on <see cref="ListWidget{T}"/>.
/// Exposes the per-row item value alongside the row's current selection, focus, and hover
/// state so the template can decide how to style itself. Derives from
/// <see cref="WidgetContext{TParentWidget}"/> so the usual fluent extension methods
/// (<c>context.Text(...)</c>, <c>context.VStack(...)</c>, etc.) are available inside the
/// builder.
/// </summary>
/// <typeparam name="T">The item type of the parent list.</typeparam>
public sealed class ListItemContext<T> : WidgetContext<ListWidget<T>>
{
    /// <summary>The item value backing this row.</summary>
    public T Item { get; }

    /// <summary>The zero-based index of this row in the source <c>Items</c> list.</summary>
    public int Index { get; }

    /// <summary>True when this row is the list's currently selected row.</summary>
    public bool IsSelected { get; }

    /// <summary>True when the list itself currently holds focus.</summary>
    public bool IsFocused { get; }

    /// <summary>True when the mouse is currently hovering over this row.</summary>
    public bool IsHovered { get; }

    internal ListItemContext(T item, int index, bool isSelected, bool isFocused, bool isHovered)
    {
        Item = item;
        Index = index;
        IsSelected = isSelected;
        IsFocused = isFocused;
        IsHovered = isHovered;
    }
}
