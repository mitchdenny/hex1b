using System.Buffers;

namespace Hex1b.Tokens;

/// <summary>
/// An <see cref="IBufferWriter{T}"/> backed by an array rented from
/// <see cref="ArrayPool{T}.Shared"/>. The caller takes ownership of the array via
/// <see cref="DetachBuffer"/> and is responsible for returning it to the pool when done.
/// </summary>
/// <remarks>
/// This exists so the ANSI serializer can write directly into pooled storage. The default
/// <see cref="ArrayBufferWriter{T}"/> allocates with <c>new T[]</c>, which for a busy
/// fullscreen frame routinely lands in the Large Object Heap (≥ 85 KB) — that allocation
/// is one of the largest per-frame Gen2 contributors in the render loop.
/// </remarks>
internal sealed class PooledArrayBufferWriter : IBufferWriter<byte>
{
    private byte[] _buffer;
    private int _written;

    public PooledArrayBufferWriter(int initialCapacity)
    {
        if (initialCapacity <= 0) initialCapacity = 256;
        _buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
    }

    public int WrittenCount => _written;

    public void Advance(int count)
    {
        if (count < 0 || _written + count > _buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));
        _written += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsMemory(_written);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsSpan(_written);
    }

    /// <summary>
    /// Transfers ownership of the underlying pooled buffer to the caller. The caller must
    /// return it to <see cref="ArrayPool{T}.Shared"/> when the bytes have been fully consumed.
    /// After this call the writer must not be used.
    /// </summary>
    public byte[] DetachBuffer(out int length)
    {
        var buf = _buffer;
        length = _written;
        _buffer = Array.Empty<byte>();
        _written = 0;
        return buf;
    }

    /// <summary>
    /// Returns the underlying buffer to the pool and resets the writer. Use this when
    /// disposing of a writer whose contents will not be consumed (e.g. an exception path).
    /// </summary>
    public void ReturnToPool()
    {
        if (_buffer.Length > 0)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = Array.Empty<byte>();
            _written = 0;
        }
    }

    private void EnsureCapacity(int sizeHint)
    {
        if (sizeHint < 1) sizeHint = 1;
        var required = _written + sizeHint;
        if (required <= _buffer.Length) return;

        // Grow by doubling, but at least to required.
        var newSize = Math.Max(required, _buffer.Length * 2);
        var next = ArrayPool<byte>.Shared.Rent(newSize);
        Buffer.BlockCopy(_buffer, 0, next, 0, _written);
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = next;
    }
}
