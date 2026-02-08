using System.Collections.Specialized;

namespace Hex1b.Logging;

/// <summary>
/// Thread-safe circular buffer with a fixed capacity.
/// When the buffer is full, the oldest items are overwritten.
/// </summary>
internal sealed class CircularBuffer<T> : INotifyCollectionChanged
{
    private readonly T[] _buffer;
    private readonly object _lock = new();
    private int _head;
    private int _count;

    public CircularBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

        _buffer = new T[capacity];
    }

    public int Capacity => _buffer.Length;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }

    public void Add(T item)
    {
        lock (_lock)
        {
            var index = (_head + _count) % _buffer.Length;

            if (_count == _buffer.Length)
            {
                // Buffer full — overwrite oldest
                _buffer[_head] = item;
                _head = (_head + 1) % _buffer.Length;
            }
            else
            {
                _buffer[index] = item;
                _count++;
            }
        }

        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>
    /// Returns items from the buffer as an ordered list (oldest first).
    /// </summary>
    public IReadOnlyList<T> GetItems(int startIndex, int count)
    {
        lock (_lock)
        {
            if (startIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(startIndex));

            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (startIndex >= _count)
                return Array.Empty<T>();

            var actualCount = Math.Min(count, _count - startIndex);
            var result = new T[actualCount];

            for (var i = 0; i < actualCount; i++)
            {
                var bufferIndex = (_head + startIndex + i) % _buffer.Length;
                result[i] = _buffer[bufferIndex];
            }

            return result;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _head = 0;
            _count = 0;
        }

        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
