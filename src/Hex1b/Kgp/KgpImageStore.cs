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

    internal enum ChunkStatus
    {
        Incomplete,
        Complete,
        TooLarge,
    }

    internal readonly record struct PendingTransmission(
        KgpParsedCommand Command,
        KgpParsedCommand.TransmissionData Transmission,
        KgpParsedCommand.QuietMode Quiet);

    internal readonly record struct ChunkResult(
        ChunkStatus Status,
        KgpParsedCommand? InitialCommand,
        KgpParsedCommand.TransmissionData Transmission,
        KgpParsedCommand.QuietMode Quiet,
        byte[]? Data,
        long AttemptedLength,
        long MaximumLength);

    internal enum AnimationFrameStatus
    {
        Success,
        InvalidIdentity,
        ImageNotFound,
        UnsupportedMedium,
        UnsupportedCompression,
        UnsupportedFormat,
        UnsupportedBaseFormat,
        InvalidBaseData,
        InvalidDimensions,
        BaseFrameNotFound,
        InsufficientData,
        TooMuchData,
        FrameLimitReached,
        NoSpace,
        OutOfMemory,
    }

    internal readonly record struct AnimationFrameInfo(
        AnimationFrameStatus Status,
        uint ImageId,
        uint ImageNumber,
        uint FrameNumber,
        uint SourceWidth,
        uint SourceHeight,
        int ExpectedDataLength,
        long RequiredStorageBytes);

    internal readonly record struct AnimationFrameResult(
        AnimationFrameInfo Info,
        KgpImageData? Image);

    internal enum AnimationFrameDeleteStatus
    {
        NotFound,
        NoOp,
        Deleted,
        ImageRemoved,
        OutOfMemory,
    }

    internal readonly record struct AnimationFrameDeleteResult(
        AnimationFrameDeleteStatus Status,
        uint ImageId,
        uint ImageNumber,
        uint FrameNumber,
        KgpImageData? Image,
        bool CurrentFrameChanged);

    private readonly object _lock = new();
    private readonly Dictionary<uint, KgpImageData> _imagesById = new();
    private readonly Dictionary<uint, List<uint>> _imagesByNumber = new();
    private readonly HashSet<uint> _unaddressableImageIds = new();
    private uint _nextId = 1;
    private long _totalSize;
    private readonly long _quotaBytes;

    private KgpPendingUpload? _pendingUpload;

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
    /// Total size of all stored root and animation frame data in bytes.
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
        get { lock (_lock) return _pendingUpload is not null; }
    }

    internal long MaximumPendingUploadBytes
        => Math.Min(Math.Max(0, _quotaBytes), Array.MaxLength);

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
            IReadOnlyList<KgpPlacement> sourcePlacements,
            IEnumerable<uint>? additionalImageIds = null)
    {
        lock (_lock)
        {
            var placements = new List<KgpPlacement>(sourcePlacements.Count);
            var images = new Dictionary<uint, KgpImageData>();
            foreach (var sourcePlacement in sourcePlacements)
            {
                if (!_imagesById.TryGetValue(sourcePlacement.ImageId, out var image))
                    continue;

                var placement = sourcePlacement.Clone();
                placements.Add(placement);
                if (!images.ContainsKey(placement.ImageId))
                    images.Add(placement.ImageId, image);
            }

            if (additionalImageIds is not null)
            {
                foreach (var imageId in additionalImageIds)
                {
                    if (!images.ContainsKey(imageId) &&
                        _imagesById.TryGetValue(imageId, out var image))
                    {
                        images.Add(imageId, image);
                    }
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

    internal bool SelectAddressableImage(uint imageId, bool removeData)
    {
        lock (_lock)
        {
            if (_unaddressableImageIds.Contains(imageId) ||
                !_imagesById.ContainsKey(imageId))
            {
                return false;
            }

            if (removeData)
                RemoveImageUnsafe(imageId);
            return true;
        }
    }

    internal HashSet<uint> SelectAddressableImagesInRange(
        uint firstImageId,
        uint lastImageId,
        bool removeData)
    {
        lock (_lock)
        {
            var selected = new HashSet<uint>();
            if (firstImageId == 0 ||
                lastImageId == 0 ||
                firstImageId > lastImageId)
            {
                return selected;
            }

            foreach (var imageId in _imagesById.Keys)
            {
                if (imageId >= firstImageId &&
                    imageId <= lastImageId &&
                    !_unaddressableImageIds.Contains(imageId))
                {
                    selected.Add(imageId);
                }
            }

            if (removeData)
            {
                foreach (var imageId in selected)
                    RemoveImageUnsafe(imageId);
            }

            return selected;
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

    internal AnimationFrameInfo GetAnimationFrameInfo(
        KgpParsedCommand.AnimationFrame command)
    {
        ArgumentNullException.ThrowIfNull(command);

        lock (_lock)
        {
            return GetAnimationFrameInfoUnsafe(command);
        }
    }

    internal AnimationFrameResult StoreAnimationFrame(
        KgpParsedCommand.AnimationFrame command,
        byte[] data)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(data);

        lock (_lock)
        {
            var info = GetAnimationFrameInfoUnsafe(command);
            if (info.Status != AnimationFrameStatus.Success)
                return new AnimationFrameResult(info, Image: null);

            if (data.Length < info.ExpectedDataLength)
            {
                return new AnimationFrameResult(
                    info with { Status = AnimationFrameStatus.InsufficientData },
                    Image: null);
            }

            if (data.Length > info.ExpectedDataLength)
            {
                return new AnimationFrameResult(
                    info with { Status = AnimationFrameStatus.TooMuchData },
                    Image: null);
            }

            if (info.RequiredStorageBytes > 0 &&
                WouldExceedQuota(
                    _totalSize,
                    info.RequiredStorageBytes,
                    _quotaBytes))
            {
                return new AnimationFrameResult(
                    info with { Status = AnimationFrameStatus.NoSpace },
                    Image: null);
            }

            var image = _imagesById[info.ImageId];
            try
            {
                var animation = CreateNormalizedAnimation(image);
                var frameIndex = checked((int)info.FrameNumber - 1);
                var isNewFrame = frameIndex == animation.FrameCount;
                byte[] canvas;
                int gapMilliseconds;
                if (isNewFrame)
                {
                    canvas = command.Frame.BaseFrameNumber > 0
                        ? animation.GetFrame(
                                checked((int)command.Frame.BaseFrameNumber - 1))
                            .Data
                            .ToArray()
                        : KgpAnimationFrameComposer.CreateRgbaCanvas(
                            image.Width,
                            image.Height,
                            command.Frame.BackgroundColor);
                    gapMilliseconds = command.Frame.Gap switch
                    {
                        > 0 => command.Frame.Gap,
                        < 0 => 0,
                        _ => 40,
                    };
                }
                else
                {
                    var existing = animation.GetFrame(frameIndex);
                    canvas = existing.Data.ToArray();
                    gapMilliseconds = command.Frame.Gap == 0
                        ? existing.GapMilliseconds
                        : Math.Max(0, command.Frame.Gap);
                }

                KgpAnimationFrameComposer.Compose(
                    canvas,
                    image.Width,
                    image.Height,
                    data,
                    info.SourceWidth,
                    info.SourceHeight,
                    command.Transmission.Format,
                    command.Frame.X,
                    command.Frame.Y,
                    command.Frame.Composition);

                var updatedFrame = new KgpAnimationFrame(
                    canvas,
                    gapMilliseconds);
                var updatedAnimation = isNewFrame
                    ? animation.AddFrame(updatedFrame)
                    : animation.SetFrame(frameIndex, updatedFrame);
                var updatedImage = image.WithAnimation(updatedAnimation);
                var storageDelta = checked(
                    updatedImage.StorageSize - image.StorageSize);
                if (storageDelta != info.RequiredStorageBytes)
                {
                    throw new InvalidOperationException(
                        "KGP animation frame storage accounting changed during a locked transaction.");
                }

                _imagesById[image.ImageId] = updatedImage;
                _totalSize = checked(_totalSize + storageDelta);
                return new AnimationFrameResult(info, updatedImage);
            }
            catch (OutOfMemoryException)
            {
                return new AnimationFrameResult(
                    info with { Status = AnimationFrameStatus.OutOfMemory },
                    Image: null);
            }
        }
    }

    internal AnimationFrameDeleteResult DeleteAnimationFrame(
        uint imageId,
        uint imageNumber,
        uint frameNumber,
        bool freeData)
    {
        lock (_lock)
        {
            var image = ResolveAddressableImageUnsafe(imageId, imageNumber);
            if (image is null)
            {
                return new AnimationFrameDeleteResult(
                    AnimationFrameDeleteStatus.NotFound,
                    imageId,
                    imageNumber,
                    FrameNumber: 0,
                    Image: null,
                    CurrentFrameChanged: false);
            }

            if (image.FrameCount == 1)
            {
                if (!freeData)
                {
                    return new AnimationFrameDeleteResult(
                        AnimationFrameDeleteStatus.NoOp,
                        image.ImageId,
                        image.ImageNumber,
                        FrameNumber: 1,
                        image,
                        CurrentFrameChanged: false);
                }

                RemoveImageUnsafe(image.ImageId);
                return new AnimationFrameDeleteResult(
                    AnimationFrameDeleteStatus.ImageRemoved,
                    image.ImageId,
                    image.ImageNumber,
                    FrameNumber: 1,
                    Image: null,
                    CurrentFrameChanged: true);
            }

            var animation = image.AnimationState
                ?? throw new InvalidOperationException(
                    "An image with multiple frames has no animation state.");
            var resolvedFrameNumber = frameNumber == 0
                ? 1u
                : Math.Min(frameNumber, checked((uint)animation.FrameCount));
            var removedIndex = checked((int)resolvedFrameNumber - 1);
            try
            {
                var currentFrameIndex = image.CurrentFrameIndex;
                var lastFrameIndex = animation.FrameCount - 2;
                var currentFrameChanged = false;
                if (currentFrameIndex > lastFrameIndex)
                {
                    currentFrameIndex = lastFrameIndex;
                    currentFrameChanged = true;
                }
                else if (removedIndex == currentFrameIndex)
                {
                    currentFrameChanged = true;
                }
                else if (removedIndex < currentFrameIndex)
                {
                    currentFrameIndex--;
                }

                var updatedAnimation = animation.RemoveFrame(
                    removedIndex,
                    currentFrameIndex);
                var updatedImage = image.WithAnimation(updatedAnimation);
                _imagesById[image.ImageId] = updatedImage;
                _totalSize = checked(
                    _totalSize - (image.StorageSize - updatedImage.StorageSize));
                return new AnimationFrameDeleteResult(
                    AnimationFrameDeleteStatus.Deleted,
                    image.ImageId,
                    image.ImageNumber,
                    resolvedFrameNumber,
                    updatedImage,
                    currentFrameChanged);
            }
            catch (OutOfMemoryException)
            {
                return new AnimationFrameDeleteResult(
                    AnimationFrameDeleteStatus.OutOfMemory,
                    image.ImageId,
                    image.ImageNumber,
                    resolvedFrameNumber,
                    image,
                    CurrentFrameChanged: false);
            }
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

    internal void RemoveUnreferencedImages(IEnumerable<uint> retainedImageIds)
    {
        ArgumentNullException.ThrowIfNull(retainedImageIds);

        lock (_lock)
        {
            var retained = retainedImageIds as HashSet<uint> ?? [.. retainedImageIds];
            var imageIds = _imagesById.Keys.ToArray();
            foreach (var imageId in imageIds)
            {
                if (!retained.Contains(imageId))
                    RemoveImageUnsafe(imageId);
            }
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
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(decodedData);

        lock (_lock)
        {
            var transmission = command.ToTransmissionData();
            var result = ProcessChunkUnsafe(
                initialCommand: null,
                transmission,
                ToQuietMode(command.Quiet),
                quietWasSpecified: command.Quiet != 0,
                decodedData,
                Array.MaxLength);
            if (result.Status != ChunkStatus.Complete)
                return null;

            var imageId = result.Transmission.ImageId > 0
                ? result.Transmission.ImageId
                : AllocateIdUnsafe();
            return CreateImage(imageId, result.Transmission, result.Data!);
        }
    }

    internal ChunkResult ProcessChunk(
        KgpParsedCommand command,
        byte[] decodedData,
        long maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(decodedData);
        if (!command.TryGetTransmission(out var transmission))
            throw new ArgumentException("The command does not carry transmission data.", nameof(command));

        lock (_lock)
        {
            return ProcessChunkUnsafe(
                command,
                transmission,
                command.Quiet,
                command.ControlKeys.Contains('q'),
                decodedData,
                maximumBytes);
        }
    }

    internal PendingTransmission? GetPendingTransmission()
    {
        lock (_lock)
        {
            return GetPendingTransmissionUnsafe();
        }
    }

    internal PendingTransmission? AbortPendingTransmission()
    {
        lock (_lock)
        {
            var pending = GetPendingTransmissionUnsafe();
            AbortChunkedTransferUnsafe();
            return pending;
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
        var pending = _pendingUpload;
        _pendingUpload = null;
        pending?.Dispose();
    }

    private ChunkResult ProcessChunkUnsafe(
        KgpParsedCommand? initialCommand,
        KgpParsedCommand.TransmissionData transmission,
        KgpParsedCommand.QuietMode quiet,
        bool quietWasSpecified,
        byte[] decodedData,
        long maximumBytes)
    {
        if (_pendingUpload is null)
        {
            _pendingUpload = new KgpPendingUpload(
                initialCommand,
                transmission,
                quiet,
                maximumBytes);
        }
        else if (quietWasSpecified)
        {
            _pendingUpload.ApplyQuiet(quiet);
        }

        var pending = _pendingUpload;
        if (!pending.TryAppend(decodedData, out var attemptedLength))
        {
            var tooLarge = new ChunkResult(
                ChunkStatus.TooLarge,
                pending.InitialCommand,
                pending.Transmission,
                pending.EffectiveQuiet,
                Data: null,
                attemptedLength,
                pending.MaximumBytes);
            AbortChunkedTransferUnsafe();
            return tooLarge;
        }

        if (transmission.MoreData)
        {
            return new ChunkResult(
                ChunkStatus.Incomplete,
                pending.InitialCommand,
                pending.Transmission,
                pending.EffectiveQuiet,
                Data: null,
                attemptedLength,
                pending.MaximumBytes);
        }

        var initial = pending.InitialCommand;
        var initialTransmission = pending.Transmission;
        var effectiveQuiet = pending.EffectiveQuiet;
        var completeData = pending.Complete();
        _pendingUpload = null;
        return new ChunkResult(
            ChunkStatus.Complete,
            initial,
            initialTransmission,
            effectiveQuiet,
            completeData,
            attemptedLength,
            pending.MaximumBytes);
    }

    private PendingTransmission? GetPendingTransmissionUnsafe()
    {
        if (_pendingUpload?.InitialCommand is not { } command)
            return null;

        return new PendingTransmission(
            command,
            _pendingUpload.Transmission,
            _pendingUpload.EffectiveQuiet);
    }

    private static KgpParsedCommand.QuietMode ToQuietMode(int quiet)
        => quiet switch
        {
            <= 0 => KgpParsedCommand.QuietMode.Normal,
            1 => KgpParsedCommand.QuietMode.SuppressSuccess,
            _ => KgpParsedCommand.QuietMode.SuppressAll,
        };

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
            _totalSize -= existing.StorageSize;
            _imagesById.Remove(existing.ImageId);
            _unaddressableImageIds.Remove(existing.ImageId);
            RemoveFromNumberIndex(existing);
        }

        while (WouldExceedQuota(
                   _totalSize,
                   image.StorageSize,
                   _quotaBytes) &&
               _imagesById.Count > 0)
        {
            EvictOldest();
        }

        _totalSize += image.StorageSize;
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

        _totalSize -= image.StorageSize;
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

    private static bool WouldExceedQuota(
        long currentSize,
        long addedSize,
        long quotaBytes)
    {
        if (currentSize < 0 || addedSize < 0)
            throw new ArgumentOutOfRangeException(nameof(currentSize));
        return addedSize > quotaBytes ||
               currentSize > quotaBytes - addedSize;
    }

    private static KgpImageData CreateImage(
        uint imageId,
        KgpParsedCommand.TransmissionData transmission,
        byte[] data)
    {
        var width = transmission.Width;
        var height = transmission.Height;
        if (transmission.Format == KgpFormat.Png)
        {
            if (!KgpPngMetadata.TryReadDimensions(
                    data,
                    out width,
                    out height))
            {
                width = 0;
                height = 0;
            }
        }

        return new KgpImageData(
            imageId,
            transmission.ImageNumber,
            data,
            width,
            height,
            transmission.Format);
    }

    private AnimationFrameInfo GetAnimationFrameInfoUnsafe(
        KgpParsedCommand.AnimationFrame command)
    {
        var transmission = command.Transmission;
        if (transmission.ImageId == 0 && transmission.ImageNumber == 0)
        {
            return CreateAnimationFrameFailure(
                AnimationFrameStatus.InvalidIdentity,
                transmission);
        }

        if (transmission.Medium != KgpTransmissionMedium.Direct)
        {
            return CreateAnimationFrameFailure(
                AnimationFrameStatus.UnsupportedMedium,
                transmission);
        }

        if (transmission.Compression != KgpParsedCommand.CompressionMode.None)
        {
            return CreateAnimationFrameFailure(
                AnimationFrameStatus.UnsupportedCompression,
                transmission);
        }

        if (transmission.Format is not (KgpFormat.Rgb24 or KgpFormat.Rgba32))
        {
            return CreateAnimationFrameFailure(
                AnimationFrameStatus.UnsupportedFormat,
                transmission);
        }

        var image = ResolveAddressableImageUnsafe(
            transmission.ImageId,
            transmission.ImageNumber);
        if (image is null)
        {
            return CreateAnimationFrameFailure(
                AnimationFrameStatus.ImageNotFound,
                transmission);
        }

        if (image.Format == KgpFormat.Png && image.AnimationState is null)
        {
            return CreateAnimationFrameFailure(
                AnimationFrameStatus.UnsupportedBaseFormat,
                transmission,
                image);
        }

        if (image.AnimationState is null && !image.IsDataSizeValid())
        {
            return CreateAnimationFrameFailure(
                AnimationFrameStatus.InvalidBaseData,
                transmission,
                image);
        }

        var sourceWidth = transmission.Width == 0
            ? image.Width
            : transmission.Width;
        var sourceHeight = transmission.Height == 0
            ? image.Height
            : transmission.Height;
        if (sourceWidth == 0 ||
            sourceHeight == 0 ||
            sourceWidth > image.Width ||
            sourceHeight > image.Height)
        {
            return CreateAnimationFrameFailure(
                AnimationFrameStatus.InvalidDimensions,
                transmission,
                image);
        }

        var bytesPerPixel = transmission.Format == KgpFormat.Rgb24 ? 3u : 4u;
        var expectedLength = (ulong)sourceWidth * sourceHeight * bytesPerPixel;
        if (expectedLength > (ulong)Array.MaxLength)
        {
            return CreateAnimationFrameFailure(
                AnimationFrameStatus.InvalidDimensions,
                transmission,
                image);
        }

        if (!KgpAnimationFrameComposer.TryGetRgbaBufferLength(
                image.Width,
                image.Height,
                out var rgbaFrameLength))
        {
            return CreateAnimationFrameFailure(
                AnimationFrameStatus.InvalidDimensions,
                transmission,
                image);
        }

        var frameCount = checked((uint)image.FrameCount);
        var resolvedFrameNumber =
            command.Frame.EditFrameNumber >= 1 &&
            command.Frame.EditFrameNumber <= frameCount
                ? command.Frame.EditFrameNumber
                : checked(frameCount + 1);
        var isNewFrame = resolvedFrameNumber == frameCount + 1;
        if (isNewFrame &&
            command.Frame.BaseFrameNumber > frameCount)
        {
            return new AnimationFrameInfo(
                AnimationFrameStatus.BaseFrameNotFound,
                image.ImageId,
                image.ImageNumber,
                resolvedFrameNumber,
                sourceWidth,
                sourceHeight,
                checked((int)expectedLength),
                RequiredStorageBytes: 0);
        }

        if (isNewFrame &&
            image.FrameCount >= KgpAnimationState.MaximumFrameCount)
        {
            return new AnimationFrameInfo(
                AnimationFrameStatus.FrameLimitReached,
                image.ImageId,
                image.ImageNumber,
                resolvedFrameNumber,
                sourceWidth,
                sourceHeight,
                checked((int)expectedLength),
                RequiredStorageBytes: 0);
        }

        var requiredStorageBytes = 0L;
        if (image.AnimationState is null)
            requiredStorageBytes += rgbaFrameLength - image.StorageSize;
        if (isNewFrame)
            requiredStorageBytes += rgbaFrameLength;

        return new AnimationFrameInfo(
            AnimationFrameStatus.Success,
            image.ImageId,
            image.ImageNumber,
            resolvedFrameNumber,
            sourceWidth,
            sourceHeight,
            checked((int)expectedLength),
            requiredStorageBytes);
    }

    private static AnimationFrameInfo CreateAnimationFrameFailure(
        AnimationFrameStatus status,
        KgpParsedCommand.TransmissionData transmission,
        KgpImageData? image = null)
        => new(
            status,
            image?.ImageId ?? transmission.ImageId,
            image?.ImageNumber ?? transmission.ImageNumber,
            FrameNumber: 0,
            SourceWidth: 0,
            SourceHeight: 0,
            ExpectedDataLength: 0,
            RequiredStorageBytes: 0);

    private static KgpAnimationState CreateNormalizedAnimation(
        KgpImageData image)
    {
        if (image.AnimationState is { } animation)
            return animation;

        var rootData = image.Format == KgpFormat.Rgba32
            ? image.Data
            : KgpAnimationFrameComposer.ConvertToRgba(
                image.Data,
                image.Width,
                image.Height,
                image.Format);
        return KgpAnimationState.CreateRoot(
            new KgpAnimationFrame(rootData, gapMilliseconds: 0));
    }

    private KgpImageData? ResolveAddressableImageUnsafe(
        uint imageId,
        uint imageNumber)
    {
        if (imageId > 0)
        {
            if (_unaddressableImageIds.Contains(imageId))
                return null;
            return _imagesById.TryGetValue(imageId, out var image)
                ? image
                : null;
        }

        return imageNumber > 0
            ? GetImageByNumberUnsafe(imageNumber)
            : null;
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
