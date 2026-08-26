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
    internal readonly record struct ImageRelocation(
        uint PreviousId,
        uint CurrentId);

    internal readonly record struct StoreResult(
        KgpImageData Image,
        bool Replaced,
        ImageRelocation? Relocation = null);

    internal readonly record struct TransmissionStartResult(
        bool RemovedAddressableImage,
        ImageRelocation? Relocation);

    internal readonly record struct CompletedTransmission(
        KgpParsedCommand.TransmissionData Transmission,
        StoreResult Store);

    private readonly record struct AssembledTransmission(
        KgpParsedCommand.TransmissionData Transmission,
        byte[] Data);

    private readonly object _lock = new();
    private readonly Dictionary<uint, KgpImageData> _imagesById = new();
    private readonly Dictionary<uint, List<uint>> _imagesByNumber = new();
    private readonly HashSet<uint> _unaddressableImageIds = new();
    private uint _nextId = 1;
    private long _totalSize;
    private readonly long _quotaBytes;

    // Chunked transfer state
    private KgpParsedCommand.TransmissionData? _chunkedCommand;
    private readonly List<byte> _chunkedData = new();

    /// <summary>
    /// Creates a new image store with the specified storage quota.
    /// </summary>
    /// <param name="quotaBytes">Maximum total image data size in bytes. Default: 320MB (matching kitty).</param>
    public KgpImageStore(long quotaBytes = 320 * 1024 * 1024)
        : this(quotaBytes, 1)
    {
    }

    internal KgpImageStore(long quotaBytes, uint nextId)
    {
        _quotaBytes = quotaBytes;
        _nextId = nextId == 0 ? 1 : nextId;
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
    /// Allocates a currently unused, non-zero image ID.
    /// </summary>
    /// <remarks>
    /// Allocation skips live image IDs and wraps from <see cref="uint.MaxValue"/> to 1.
    /// Terminal transmission paths allocate and store under one lock so the selected ID
    /// cannot be claimed between those operations.
    /// </remarks>
    public uint AllocateId()
    {
        lock (_lock)
        {
            return AllocateIdUnsafe();
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
            return StoreImageUnsafe(image).Image;
        }
    }

    internal StoreResult StoreImage(
        KgpParsedCommand.TransmissionData transmission,
        byte[] data)
    {
        lock (_lock)
        {
            return StoreTransmissionUnsafe(transmission, data);
        }
    }

    internal TransmissionStartResult BeginExplicitTransmission(uint imageId)
    {
        if (imageId == 0)
            throw new ArgumentOutOfRangeException(nameof(imageId));

        lock (_lock)
        {
            ImageRelocation? relocation = null;
            if (_unaddressableImageIds.Contains(imageId))
                relocation = RelocateUnaddressableImageUnsafe(imageId);

            return new TransmissionStartResult(
                RemoveImageUnsafe(imageId),
                relocation);
        }
    }

    internal (
        IReadOnlyList<KgpPlacement> Placements,
        IReadOnlyDictionary<uint, KgpImageData> Images) CaptureSnapshot(
            IReadOnlyList<KgpPlacement> sourcePlacements)
    {
        lock (_lock)
        {
            var placements = new KgpPlacement[sourcePlacements.Count];
            var images = new Dictionary<uint, KgpImageData>();
            for (var i = 0; i < sourcePlacements.Count; i++)
            {
                var placement = sourcePlacements[i].Clone();
                placements[i] = placement;
                if (!images.ContainsKey(placement.ImageId) &&
                    _imagesById.TryGetValue(placement.ImageId, out var image))
                {
                    images.Add(placement.ImageId, image);
                }
            }

            return (placements, images);
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

    internal KgpImageData? GetImageByClientId(uint imageId)
    {
        lock (_lock)
        {
            if (_unaddressableImageIds.Contains(imageId))
                return null;

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
            return RemoveImageUnsafe(imageId);
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

            return RemoveImageUnsafe(image.ImageId);
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
            _unaddressableImageIds.Clear();
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
            var assembled = ProcessChunkUnsafe(command.ToTransmissionData(), decodedData);
            if (assembled is null)
                return null;

            var transmission = assembled.Value.Transmission;
            var imageId = transmission.ImageId > 0
                ? transmission.ImageId
                : AllocateIdUnsafe();
            return CreateImage(imageId, transmission, assembled.Value.Data);
        }
    }

    internal CompletedTransmission? ProcessChunkAndStore(
        KgpParsedCommand.TransmissionData transmission,
        byte[] decodedData)
    {
        lock (_lock)
        {
            var assembled = ProcessChunkUnsafe(transmission, decodedData);
            if (assembled is null)
                return null;

            var store = StoreTransmissionUnsafe(
                assembled.Value.Transmission,
                assembled.Value.Data);
            return new CompletedTransmission(
                assembled.Value.Transmission,
                store);
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
        _chunkedData.Clear();
    }

    private AssembledTransmission? ProcessChunkUnsafe(
        KgpParsedCommand.TransmissionData transmission,
        byte[] decodedData)
    {
        if (_chunkedCommand is null)
        {
            _chunkedCommand = transmission;
        }

        _chunkedData.AddRange(decodedData);

        if (transmission.MoreData)
            return null;

        var completeData = _chunkedData.ToArray();
        var firstTransmission = _chunkedCommand.Value;
        AbortChunkedTransferUnsafe();
        return new AssembledTransmission(firstTransmission, completeData);
    }

    private StoreResult StoreTransmissionUnsafe(
        KgpParsedCommand.TransmissionData transmission,
        byte[] data)
    {
        if (transmission.ImageId > 0 && transmission.ImageNumber > 0)
        {
            throw new InvalidOperationException(
                "A KGP transmission cannot specify both an image ID and image number.");
        }

        var imageId = transmission.ImageId > 0
            ? transmission.ImageId
            : AllocateIdUnsafe();
        ImageRelocation? relocation = null;
        if (transmission.IdentityKind == KgpParsedCommand.ImageIdentityKind.ExplicitId &&
            _unaddressableImageIds.Contains(imageId))
        {
            // Anonymous images have private storage IDs, not client IDs. Move a
            // private image aside when a client explicitly claims that number.
            relocation = RelocateUnaddressableImageUnsafe(imageId);
        }

        var image = CreateImage(imageId, transmission, data);
        var stored = StoreImageUnsafe(
            image,
            transmission.IdentityKind != KgpParsedCommand.ImageIdentityKind.Anonymous);
        return stored with { Relocation = relocation };
    }

    private StoreResult StoreImageUnsafe(
        KgpImageData image,
        bool addressable = true)
    {
        var replaced = _imagesById.TryGetValue(image.ImageId, out var existing);
        if (existing is not null)
        {
            _totalSize -= existing.Data.Length;
            RemoveFromNumberIndex(existing);
        }

        while (_totalSize + image.Data.Length > _quotaBytes && _imagesById.Count > 0)
        {
            EvictOldest();
        }

        _totalSize += image.Data.Length;
        _imagesById[image.ImageId] = image;
        if (addressable)
            _unaddressableImageIds.Remove(image.ImageId);
        else
            _unaddressableImageIds.Add(image.ImageId);

        if (image.ImageNumber > 0)
        {
            if (!_imagesByNumber.TryGetValue(image.ImageNumber, out var list))
            {
                list = new List<uint>();
                _imagesByNumber[image.ImageNumber] = list;
            }
            list.Add(image.ImageId);
        }

        return new StoreResult(image, replaced);
    }

    private bool RemoveImageUnsafe(uint imageId)
    {
        if (!_imagesById.TryGetValue(imageId, out var image))
            return false;

        _totalSize -= image.Data.Length;
        _imagesById.Remove(imageId);
        _unaddressableImageIds.Remove(imageId);
        RemoveFromNumberIndex(image);
        return true;
    }

    private ImageRelocation RelocateUnaddressableImageUnsafe(uint previousId)
    {
        var image = _imagesById[previousId];
        var currentId = AllocateIdUnsafe();
        var relocatedImage = image.WithImageId(currentId);

        _imagesById.Remove(previousId);
        _unaddressableImageIds.Remove(previousId);
        _imagesById[currentId] = relocatedImage;
        _unaddressableImageIds.Add(currentId);

        return new ImageRelocation(previousId, currentId);
    }

    private uint AllocateIdUnsafe()
    {
        var attemptsRemaining = (long)_imagesById.Count + 1;
        while (attemptsRemaining-- > 0)
        {
            var candidate = _nextId;
            _nextId = candidate == uint.MaxValue ? 1 : candidate + 1;
            if (!_imagesById.ContainsKey(candidate))
                return candidate;
        }

        throw new InvalidOperationException("No KGP image IDs are available.");
    }

    private static KgpImageData CreateImage(
        uint imageId,
        KgpParsedCommand.TransmissionData transmission,
        byte[] data)
    {
        return new KgpImageData(
            imageId,
            transmission.ImageNumber,
            data,
            transmission.Width,
            transmission.Height,
            transmission.Format);
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
