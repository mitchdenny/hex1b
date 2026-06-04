namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building <see cref="ListWidget{T}"/>.
/// </summary>
public static class ListExtensions
{
    /// <summary>
    /// Creates a typed list bound to <paramref name="items"/>. Use
    /// <see cref="TypedListExtensions.ItemTemplate{T}"/> to render each row as
    /// a custom widget tree.
    /// </summary>
    /// <remarks>
    /// The non-generic <c>ListWidget</c> previously returned here has been
    /// replaced with <see cref="ListWidget{T}"/> of <typeparamref name="T"/>.
    /// Callers using only string items keep working unchanged because typed
    /// event args still expose <c>SelectedText</c> / <c>ActivatedText</c>
    /// convenience accessors. The legacy non-generic <c>ListWidget</c> remains
    /// available for direct construction (<c>new ListWidget(items)</c>) but
    /// is marked obsolete.
    /// </remarks>
    public static ListWidget<T> List<TParent, T>(
        this WidgetContext<TParent> context,
        IReadOnlyList<T> items)
        where TParent : Hex1bWidget
        => new(items);
}
