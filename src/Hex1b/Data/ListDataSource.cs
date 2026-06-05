using System.Collections.Specialized;

namespace Hex1b.Data;

/// <summary>
/// An <see cref="IListDataSource{T}"/> backed by an in-memory
/// <see cref="IReadOnlyList{T}"/>. Useful when you want to use the
/// virtualized data-source API with data that already lives in memory
/// (e.g. an <c>ObservableCollection&lt;T&gt;</c> whose mutations should
/// re-render the list) or when adapting test data.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
public sealed class ListDataSource<T> : IListDataSource<T>, IDisposable
{
    private readonly IReadOnlyList<T> _list;
    private readonly INotifyCollectionChanged? _notifyCollection;

    /// <summary>Wraps <paramref name="list"/> as a virtualized data source.</summary>
    public ListDataSource(IReadOnlyList<T> list)
    {
        _list = list ?? throw new ArgumentNullException(nameof(list));
        if (list is INotifyCollectionChanged notify)
        {
            _notifyCollection = notify;
            _notifyCollection.CollectionChanged += OnSourceCollectionChanged;
        }
    }

    /// <inheritdoc />
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <inheritdoc />
    public ValueTask<int> GetItemCountAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_list.Count);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<T>> GetItemsAsync(
        int startIndex,
        int count,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (startIndex >= _list.Count)
        {
            return ValueTask.FromResult<IReadOnlyList<T>>(Array.Empty<T>());
        }

        var actual = Math.Min(count, _list.Count - startIndex);
        var window = new T[actual];
        for (int i = 0; i < actual; i++)
        {
            window[i] = _list[startIndex + i];
        }
        return ValueTask.FromResult<IReadOnlyList<T>>(window);
    }

    /// <inheritdoc />
    public ValueTask<int?> GetIndexForKeyAsync(object? key, CancellationToken cancellationToken = default)
    {
        switch (key)
        {
            case int intKey when intKey >= 0 && intKey < _list.Count:
                return ValueTask.FromResult<int?>(intKey);
            case T typedKey:
            {
                var cmp = EqualityComparer<T>.Default;
                for (int i = 0; i < _list.Count; i++)
                {
                    if (cmp.Equals(_list[i], typedKey))
                    {
                        return ValueTask.FromResult<int?>(i);
                    }
                }
                break;
            }
        }
        return ValueTask.FromResult<int?>(null);
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => CollectionChanged?.Invoke(this, e);

    /// <summary>Unsubscribes from the underlying collection's change notifications.</summary>
    public void Dispose()
    {
        if (_notifyCollection is not null)
        {
            _notifyCollection.CollectionChanged -= OnSourceCollectionChanged;
        }
    }
}
