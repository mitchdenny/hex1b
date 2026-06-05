using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Context passed to an <c>ItemTemplate</c> callback on <see cref="ListWidget{T}"/>.
/// Exposes the per-row item value alongside the row's current focus, selection,
/// and hover state so the template can decide how to style itself. Derives from
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

    /// <summary>True when this row is the list's currently focused (cursor) row.</summary>
    public bool IsFocused { get; }

    /// <summary>
    /// True when the list's multi-select feature is enabled and this row is part
    /// of the checked set. Always <c>false</c> when multi-select is off — the
    /// cursor row is reported through <see cref="IsFocused"/>, not here.
    /// </summary>
    public bool IsSelected { get; }

    /// <summary>True when the list itself currently holds keyboard focus.</summary>
    public bool OwnerHasFocus { get; }

    /// <summary>True when the mouse is currently hovering over this row.</summary>
    public bool IsHovered { get; }

    /// <summary>
    /// True when <see cref="Item"/> was materialised from the data source for
    /// this row. Always <c>true</c> in non-virtualized mode. In virtualized
    /// mode this is <c>false</c> only for rows whose data is still in-flight —
    /// templates can branch on this to render a placeholder (e.g. a loading
    /// spinner) instead of dereferencing a possibly <c>default(T)</c> item.
    /// </summary>
    public bool IsLoaded { get; }

    internal ListItemContext(
        T item,
        int index,
        bool isFocused,
        bool ownerHasFocus,
        bool isHovered,
        bool isLoaded = true,
        bool isSelected = false)
    {
        Item = item;
        Index = index;
        IsFocused = isFocused;
        OwnerHasFocus = ownerHasFocus;
        IsHovered = isHovered;
        IsLoaded = isLoaded;
        IsSelected = isSelected;
    }
}
