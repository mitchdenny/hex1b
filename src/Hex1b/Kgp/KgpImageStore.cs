namespace Hex1b;

/// <summary>
/// Central store for KGP images. Manages image IDs, image numbers,
/// chunked transfers, and storage quotas.
/// </summary>
/// <remarks>
/// Thread-safe. All public methods are synchronized.
/// </remarks>
public sealed class KgpImageStore
{
    private readonly object _lock = new();
    private readonly Dictionary<uint, KgpImageData> _imagesById = new();
    private readonly Dictionary<uint, List<uint>> _imagesByNumber = new();
    private uint _nextId = 1;
    private long _totalSize;
    private readonly long _quotaBytes;

    // Chunked transfer state
    private uint _chunkedImageId;
    private uint _chunkedImageNumber;
    private KgpCommand? _chunkedCommand;
    private readonly List<byte> _chunkedData = new();

    /// <summary>
    /// Creates a new image store with the specified storage quota.
    /// </summary>
    /// <param name="quotaBytes">Maximum total image data size in bytes. Default: 320MB (matching kitty).</param>
    public KgpImageStore(long quotaBytes = 320 * 1024 * 1024)
    {
        _quotaBytes = quotaBytes;
    }

    /// <summary>
    /// Number of images currently stored.
    /// </summary>
    public int ImageCount
    {
        get { lock (_lock) return _imagesById.Count; }
    }

    /// <summary>
    /// Total size of all stored image data in bytes.
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
    /// Allocates a unique image ID.
    /// </summary>
    public uint AllocateId()
    {
        lock (_lock)
        {
            return _nextId++;
        }
    }

    /// <summary>
    /// Stores an image. If an image with the same ID already exists, it is replaced.
    /// </summary>
    /// <returns>The stored image, or null if quota would be exceeded and no eviction possible.</returns>
    public KgpImageData? StoreImage(KgpImageData image)
    {
        lock (_lock)
        {
            // Remove existing image with same ID
            if (_imagesById.TryGetValue(image.ImageId, out var existing))
            {
                _totalSize -= existing.Data.Length;
                RemoveFromNumberIndex(existing);
            }

            // Evict old images if necessary
            while (_totalSize + image.Data.Length > _quotaBytes && _imagesById.Count > 0)
            {
                EvictOldest();
            }

            _totalSize += image.Data.Length;
            _imagesById[image.ImageId] = image;

            if (image.ImageNumber > 0)
            {
                if (!_imagesByNumber.TryGetValue(image.ImageNumber, out var list))
                {
                    list = new List<uint>();
                    _imagesByNumber[image.ImageNumber] = list;
                }
                list.Add(image.ImageId);
            }

            return image;
        }
    }

    /// <summary>
    /// Gets an image by its ID.
    /// </summary>
    public KgpImageData? GetImageById(uint imageId)
    {
        lock (_lock)
        {
            return _imagesById.TryGetValue(imageId, out var image) ? image : null;
        }
    }

    /// <summary>
    /// Gets the newest image with the specified image number.
    /// </summary>
    public KgpImageData? GetImageByNumber(uint imageNumber)
    {
        lock (_lock)
        {
            if (!_imagesByNumber.TryGetValue(imageNumber, out var list) || list.Count == 0)
                return null;

            var newestId = list[^1];
            return _imagesById.TryGetValue(newestId, out var image) ? image : null;
        }
    }

    /// <summary>
    /// Removes an image by its ID.
    /// </summary>
    /// <returns>True if the image was found and removed.</returns>
    public bool RemoveImage(uint imageId)
    {
        lock (_lock)
        {
            if (!_imagesById.TryGetValue(imageId, out var image))
                return false;

            _totalSize -= image.Data.Length;
            _imagesById.Remove(imageId);
            RemoveFromNumberIndex(image);
            return true;
        }
    }

    /// <summary>
    /// Removes the newest image with the specified number.
    /// </summary>
    /// <returns>True if an image was found and removed.</returns>
    public bool RemoveImageByNumber(uint imageNumber)
    {
        lock (_lock)
        {
            var image = GetImageByNumberUnsafe(imageNumber);
            if (image is null)
                return false;

            _totalSize -= image.Data.Length;
            _imagesById.Remove(image.ImageId);
            RemoveFromNumberIndex(image);
            return true;
        }
    }

    /// <summary>
    /// Removes all images.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _imagesById.Clear();
            _imagesByNumber.Clear();
            _totalSize = 0;
            AbortChunkedTransferUnsafe();
        }
    }

    /// <summary>
    /// Begins or continues a chunked transfer. Returns the completed image when the final chunk arrives.
    /// </summary>
    /// <param name="command">The KGP command for this chunk.</param>
    /// <param name="decodedData">The base64-decoded payload data for this chunk.</param>
    /// <returns>
    /// The completed <see cref="KgpImageData"/> when the final chunk (m=0) is received,
    /// or null if more chunks are expected.
    /// </returns>
    public KgpImageData? ProcessChunk(KgpCommand command, byte[] decodedData)
    {
        lock (_lock)
        {
            if (_chunkedCommand is null)
            {
                // First chunk — save the command metadata
                _chunkedCommand = command;
                _chunkedImageId = command.ImageId;
                _chunkedImageNumber = command.ImageNumber;
            }

            _chunkedData.AddRange(decodedData);

            if (command.MoreData == 0)
            {
                // Final chunk — assemble the complete image
                var completeData = _chunkedData.ToArray();
                var imageId = _chunkedImageId > 0 ? _chunkedImageId : AllocateIdUnsafe();
                var image = new KgpImageData(
                    imageId,
                    _chunkedImageNumber,
                    completeData,
                    _chunkedCommand.Width,
                    _chunkedCommand.Height,
                    _chunkedCommand.Format);

                AbortChunkedTransferUnsafe();
                return image;
            }

            return null;
        }
    }

    /// <summary>
    /// Aborts any in-progress chunked transfer.
    /// </summary>
    public void AbortChunkedTransfer()
    {
        lock (_lock)
        {
            AbortChunkedTransferUnsafe();
        }
    }

    private void AbortChunkedTransferUnsafe()
    {
        _chunkedCommand = null;
        _chunkedImageId = 0;
        _chunkedImageNumber = 0;
        _chunkedData.Clear();
    }

    private uint AllocateIdUnsafe()
    {
        return _nextId++;
    }

    private KgpImageData? GetImageByNumberUnsafe(uint imageNumber)
    {
        if (!_imagesByNumber.TryGetValue(imageNumber, out var list) || list.Count == 0)
            return null;

        var newestId = list[^1];
        return _imagesById.TryGetValue(newestId, out var image) ? image : null;
    }

    private void RemoveFromNumberIndex(KgpImageData image)
    {
        if (image.ImageNumber > 0 && _imagesByNumber.TryGetValue(image.ImageNumber, out var list))
        {
            list.Remove(image.ImageId);
            if (list.Count == 0)
                _imagesByNumber.Remove(image.ImageNumber);
        }
    }

    private void EvictOldest()
    {
        // Simple FIFO eviction — remove the image with the lowest ID
        uint? oldestId = null;
        foreach (var id in _imagesById.Keys)
        {
            if (oldestId is null || id < oldestId.Value)
                oldestId = id;
        }

        if (oldestId.HasValue)
        {
            RemoveImage(oldestId.Value);
        }
    }
}
