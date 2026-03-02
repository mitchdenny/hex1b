using System.Collections.Specialized;

namespace Hex1b.Data;

/// <summary>
/// Provides virtualized async data access for large datasets in tables.
/// Implements <see cref="INotifyCollectionChanged"/> for reactive updates.
/// </summary>
/// <typeparam name="T">The type of items in the data source.</typeparam>
/// <remarks>
/// <para>
/// Use this interface when working with large datasets that should be loaded
/// on-demand, such as data from APIs, databases, or other async sources.
/// </para>
/// <para>
/// For in-memory lists, use <see cref="ListTableDataSource{T}"/> which wraps
/// an <see cref="IReadOnlyList{T}"/> and returns data synchronously.
/// </para>
/// </remarks>
public interface ITableDataSource<T> : INotifyCollectionChanged
{
    /// <summary>
    /// Gets the total item count asynchronously.
    /// Used for scrollbar calculations and virtualization.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total number of items in the data source.</returns>
    ValueTask<int> GetItemCountAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Fetches items for the specified range asynchronously.
    /// </summary>
    /// <param name="startIndex">The zero-based index of the first item to fetch.</param>
    /// <param name="count">The number of items to fetch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of items in the specified range.</returns>
    /// <remarks>
    /// Implementations should return fewer items if the range extends beyond
    /// the end of the data source. An empty list should be returned if
    /// startIndex is beyond the end of the data.
    /// </remarks>
    ValueTask<IReadOnlyList<T>> GetItemsAsync(
        int startIndex, 
        int count, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the index of an item by its key.
    /// Implement this method to enable auto-scrolling to rows outside the cached viewport
    /// when using virtualization.
    /// </summary>
    /// <param name="key">The key of the item to find. This is the same key type used when
    /// setting focus on a table row, or the index if no key selector is provided.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The index of the item if found, or null if not found.</returns>
    /// <remarks>
    /// <para>
    /// Return <c>null</c> if the key is not found or if this data source does not
    /// support index lookup. When <c>null</c> is returned, auto-scrolling to rows
    /// outside the current cache will not work.
    /// </para>
    /// <para>
    /// For in-memory collections like <see cref="ListTableDataSource{T}"/>, this can
    /// perform a linear search. For remote data sources, this can make an efficient
    /// indexed lookup (e.g., database query by primary key).
    /// </para>
    /// </remarks>
    ValueTask<int?> GetIndexForKeyAsync(object? key, CancellationToken cancellationToken = default);
}
