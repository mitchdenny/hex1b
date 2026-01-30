using System.Collections.Specialized;

namespace Hex1b.Data;

/// <summary>
/// A table data source that wraps an in-memory list.
/// Provides synchronous access via <see cref="ITableDataSource{T}"/>.
/// </summary>
/// <typeparam name="T">The type of items in the list.</typeparam>
/// <remarks>
/// <para>
/// Use this class when you have all data in memory and want to use the
/// <see cref="ITableDataSource{T}"/> interface for consistency with async sources.
/// </para>
/// <para>
/// If the underlying list implements <see cref="INotifyCollectionChanged"/>
/// (e.g., <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/>),
/// changes will be forwarded to subscribers.
/// </para>
/// </remarks>
public class ListTableDataSource<T> : ITableDataSource<T>, IDisposable
{
    private readonly IReadOnlyList<T> _list;
    private readonly INotifyCollectionChanged? _notifyCollection;
    
    /// <summary>
    /// Creates a new list data source wrapping the specified list.
    /// </summary>
    /// <param name="list">The list to wrap.</param>
    public ListTableDataSource(IReadOnlyList<T> list)
    {
        _list = list ?? throw new ArgumentNullException(nameof(list));
        
        // Subscribe to collection changes if supported
        if (list is INotifyCollectionChanged notifyCollection)
        {
            _notifyCollection = notifyCollection;
            _notifyCollection.CollectionChanged += OnSourceCollectionChanged;
        }
    }
    
    /// <inheritdoc />
    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    
    /// <inheritdoc />
    public ValueTask<int> GetItemCountAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(_list.Count);
    }
    
    /// <inheritdoc />
    public ValueTask<IReadOnlyList<T>> GetItemsAsync(
        int startIndex, 
        int count, 
        CancellationToken cancellationToken = default)
    {
        if (startIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(startIndex), "Start index cannot be negative.");
        
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative.");
        
        // Return empty if start is beyond the list
        if (startIndex >= _list.Count)
            return ValueTask.FromResult<IReadOnlyList<T>>(Array.Empty<T>());
        
        // Clamp count to available items
        var actualCount = Math.Min(count, _list.Count - startIndex);
        
        // For lists that support efficient range access, use Take/Skip
        // Otherwise, copy to array
        var items = _list.Skip(startIndex).Take(actualCount).ToList();
        
        return ValueTask.FromResult<IReadOnlyList<T>>(items);
    }
    
    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        CollectionChanged?.Invoke(this, e);
    }
    
    /// <summary>
    /// Disposes the data source and unsubscribes from collection changes.
    /// </summary>
    public void Dispose()
    {
        if (_notifyCollection is not null)
        {
            _notifyCollection.CollectionChanged -= OnSourceCollectionChanged;
        }
        GC.SuppressFinalize(this);
    }
}
