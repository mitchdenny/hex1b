namespace Hex1b;

/// <summary>
/// A reference-counted wrapper for objects tracked by terminal cells.
/// </summary>
/// <typeparam name="T">The type of data being tracked.</typeparam>
/// <remarks>
/// <para>
/// When a tracked object's reference count reaches zero, it automatically
/// removes itself from its parent store via the removal callback.
/// </para>
/// </remarks>
public sealed class TrackedObject<T> where T : class
{
    private readonly Action<TrackedObject<T>> _onZeroRefs;
    private int _refCount;
    private readonly object _lock = new();

    /// <summary>
    /// Gets the tracked data.
    /// </summary>
    public T Data { get; }

    /// <summary>
    /// Creates a new tracked object with initial refcount of 1.
    /// </summary>
    /// <param name="data">The data to track.</param>
    /// <param name="onZeroRefs">Callback invoked when refcount reaches zero.</param>
    internal TrackedObject(T data, Action<TrackedObject<T>> onZeroRefs)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        _onZeroRefs = onZeroRefs ?? throw new ArgumentNullException(nameof(onZeroRefs));
        _refCount = 1;
    }

    /// <summary>
    /// Gets the current reference count.
    /// </summary>
    public int RefCount
    {
        get
        {
            lock (_lock)
            {
                return _refCount;
            }
        }
    }

    /// <summary>
    /// Adds a reference to this object.
    /// </summary>
    public void AddRef()
    {
        lock (_lock)
        {
            _refCount++;
        }
    }

    /// <summary>
    /// Releases a reference. If refcount reaches zero, invokes the removal callback.
    /// </summary>
    /// <returns>True if this was the last reference and the object was removed.</returns>
    public bool Release()
    {
        Action<TrackedObject<T>>? callbackToInvoke = null;
        
        lock (_lock)
        {
            if (_refCount <= 0)
            {
                // Already released - shouldn't happen in normal use
                return false;
            }

            _refCount--;
            
            if (_refCount == 0)
            {
                // Capture callback to invoke outside lock
                callbackToInvoke = _onZeroRefs;
            }
        }

        // Invoke callback outside lock to avoid deadlocks
        if (callbackToInvoke is not null)
        {
            callbackToInvoke(this);
            return true;
        }

        return false;
    }
}
