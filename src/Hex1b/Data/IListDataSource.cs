using System.Collections.Specialized;
using Hex1b.Widgets;

namespace Hex1b.Data;

/// <summary>
/// Provides virtualized async data access for very large datasets bound to a
/// <see cref="ListWidget{T}"/>. Mirrors <see cref="ITableDataSource{T}"/>
/// but is intentionally kept separate so the two surfaces can evolve
/// independently.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <remarks>
/// <para>
/// Use this interface when the full list cannot reasonably live in memory
/// (search results paged from a remote API, an indexed file, a database
/// query, etc.). The list materialises only a windowed range of items around
/// the visible viewport on each frame and only refetches when the window
/// moves or the source raises <see cref="INotifyCollectionChanged.CollectionChanged"/>.
/// </para>
/// <para>
/// For an in-memory <see cref="IReadOnlyList{T}"/>, prefer
/// <see cref="ListDataSource{T}"/> which wraps the list and serves
/// requests synchronously.
/// </para>
/// </remarks>
public interface IListDataSource<T> : INotifyCollectionChanged
{
    /// <summary>
    /// Returns the total number of items in the data source. Used for scroll
    /// math, scrollbar sizing, and selection bounds.
    /// </summary>
    ValueTask<int> GetItemCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a contiguous range of items starting at
    /// <paramref name="startIndex"/>. Implementations may return fewer items
    /// when the requested range extends past the end of the source; an empty
    /// list signals that no items are available at or beyond
    /// <paramref name="startIndex"/>.
    /// </summary>
    /// <param name="startIndex">Zero-based index of the first item to fetch.</param>
    /// <param name="count">Maximum number of items to fetch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<IReadOnlyList<T>> GetItemsAsync(
        int startIndex,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a user-supplied key to an absolute item index, or <c>null</c>
    /// when the key is unknown to this data source.
    /// </summary>
    /// <remarks>
    /// Used to support "scroll to key" navigation across windows that aren't
    /// currently cached. In-memory implementations may perform a linear scan;
    /// remote sources should use an indexed lookup.
    /// </remarks>
    ValueTask<int?> GetIndexForKeyAsync(object? key, CancellationToken cancellationToken = default);
}
