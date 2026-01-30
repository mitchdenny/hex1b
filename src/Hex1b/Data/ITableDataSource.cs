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
}
