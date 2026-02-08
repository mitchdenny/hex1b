using System.Collections.Specialized;
using Hex1b.Data;

namespace Hex1b.Logging;

/// <summary>
/// Virtualized data source backed by the circular buffer in <see cref="Hex1bLogStore"/>.
/// </summary>
internal sealed class Hex1bLogTableDataSource : ITableDataSource<Hex1bLogEntry>, IDisposable
{
    private readonly CircularBuffer<Hex1bLogEntry> _buffer;

    public Hex1bLogTableDataSource(CircularBuffer<Hex1bLogEntry> buffer)
    {
        _buffer = buffer;
        _buffer.CollectionChanged += OnBufferChanged;
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public ValueTask<int> GetItemCountAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(_buffer.Count);
    }

    public ValueTask<IReadOnlyList<Hex1bLogEntry>> GetItemsAsync(
        int startIndex,
        int count,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(_buffer.GetItems(startIndex, count));
    }

    private void OnBufferChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        CollectionChanged?.Invoke(this, e);
    }

    public void Dispose()
    {
        _buffer.CollectionChanged -= OnBufferChanged;
    }
}
