namespace Hex1b;

/// <summary>
/// Central store for audio clips. Manages clip IDs, chunked transfers, and storage quotas.
/// </summary>
/// <remarks>
/// Thread-safe. All public methods are synchronized.
/// Modeled after <see cref="KgpImageStore"/>.
/// </remarks>
public sealed class AudioClipStore
{
    private readonly object _lock = new();
    private readonly Dictionary<uint, AudioClipData> _clipsById = new();
    private uint _nextId = 1;
    private long _totalSize;
    private readonly long _quotaBytes;

    // Chunked transfer state
    private uint _chunkedClipId;
    private AudioCommand? _chunkedCommand;
    private readonly List<byte> _chunkedData = new();

    /// <summary>
    /// Creates a new audio clip store with the specified storage quota.
    /// </summary>
    /// <param name="quotaBytes">Maximum total audio data size in bytes. Default: 64MB.</param>
    public AudioClipStore(long quotaBytes = 64 * 1024 * 1024)
    {
        _quotaBytes = quotaBytes;
    }

    /// <summary>
    /// Number of clips currently stored.
    /// </summary>
    public int ClipCount
    {
        get { lock (_lock) return _clipsById.Count; }
    }

    /// <summary>
    /// Total size of all stored audio data in bytes.
    /// </summary>
    public long TotalSize
    {
        get { lock (_lock) return _totalSize; }
    }

    /// <summary>
    /// Whether a chunked transfer is in progress.
    /// </summary>
    public bool IsChunkedTransferInProgress
    {
        get { lock (_lock) return _chunkedCommand is not null; }
    }

    /// <summary>
    /// Allocates a unique clip ID.
    /// </summary>
    public uint AllocateId()
    {
        lock (_lock) return _nextId++;
    }

    /// <summary>
    /// Stores an audio clip. If a clip with the same ID already exists, it is replaced.
    /// </summary>
    public AudioClipData? StoreClip(AudioClipData clip)
    {
        lock (_lock)
        {
            if (_clipsById.TryGetValue(clip.ClipId, out var existing))
            {
                _totalSize -= existing.Data.Length;
            }

            while (_totalSize + clip.Data.Length > _quotaBytes && _clipsById.Count > 0)
            {
                EvictOldest();
            }

            _totalSize += clip.Data.Length;
            _clipsById[clip.ClipId] = clip;
            return clip;
        }
    }

    /// <summary>
    /// Gets a clip by its ID.
    /// </summary>
    public AudioClipData? GetClipById(uint clipId)
    {
        lock (_lock)
        {
            return _clipsById.TryGetValue(clipId, out var clip) ? clip : null;
        }
    }

    /// <summary>
    /// Removes a clip by its ID.
    /// </summary>
    public bool RemoveClip(uint clipId)
    {
        lock (_lock)
        {
            if (!_clipsById.TryGetValue(clipId, out var clip))
                return false;

            _totalSize -= clip.Data.Length;
            _clipsById.Remove(clipId);
            return true;
        }
    }

    /// <summary>
    /// Removes all clips.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _clipsById.Clear();
            _totalSize = 0;
            AbortChunkedTransferUnsafe();
        }
    }

    /// <summary>
    /// Begins or continues a chunked transfer. Returns the completed clip when the final chunk arrives.
    /// </summary>
    public AudioClipData? ProcessChunk(AudioCommand command, byte[] decodedData)
    {
        lock (_lock)
        {
            if (_chunkedCommand is null)
            {
                _chunkedCommand = command;
                _chunkedClipId = command.ClipId;
            }

            _chunkedData.AddRange(decodedData);

            if (command.MoreData == 0)
            {
                var completeData = _chunkedData.ToArray();
                var clipId = _chunkedClipId > 0 ? _chunkedClipId : AllocateIdUnsafe();
                var clip = new AudioClipData(
                    clipId,
                    completeData,
                    _chunkedCommand.Format,
                    _chunkedCommand.SampleRate);

                AbortChunkedTransferUnsafe();
                return clip;
            }

            return null;
        }
    }

    /// <summary>
    /// Aborts any in-progress chunked transfer.
    /// </summary>
    public void AbortChunkedTransfer()
    {
        lock (_lock) AbortChunkedTransferUnsafe();
    }

    private void AbortChunkedTransferUnsafe()
    {
        _chunkedCommand = null;
        _chunkedClipId = 0;
        _chunkedData.Clear();
    }

    private uint AllocateIdUnsafe() => _nextId++;

    private void EvictOldest()
    {
        uint? oldestId = null;
        foreach (var id in _clipsById.Keys)
        {
            if (oldestId is null || id < oldestId.Value)
                oldestId = id;
        }

        if (oldestId.HasValue)
        {
            var clip = _clipsById[oldestId.Value];
            _totalSize -= clip.Data.Length;
            _clipsById.Remove(oldestId.Value);
        }
    }
}
