using System.Security.Cryptography;

namespace Hex1b;

/// <summary>
/// Stores transmitted image data for the Kitty Graphics Protocol.
/// Images are identified by ID and may be displayed via multiple placements.
/// </summary>
public sealed class KgpImageData
{
    private readonly KgpAnimationState? _animation;

    /// <summary>
    /// The image ID assigned by the client or allocated by the terminal.
    /// </summary>
    public uint ImageId { get; }

    /// <summary>
    /// The image number (I key), if specified. 0 means unspecified.
    /// </summary>
    public uint ImageNumber { get; }

    /// <summary>
    /// The decoded pixel data for the root frame.
    /// </summary>
    /// <remarks>
    /// When animation frame operations materialize an image, the root frame is
    /// stored as RGBA data even if the image was originally transmitted as RGB.
    /// </remarks>
    public byte[] Data { get; }

    /// <summary>
    /// Image width in pixels.
    /// </summary>
    public uint Width { get; }

    /// <summary>
    /// Image height in pixels.
    /// </summary>
    public uint Height { get; }

    /// <summary>
    /// The pixel format of the stored root-frame data.
    /// </summary>
    /// <remarks>
    /// Transmitting an animation frame materializes the root frame as
    /// <see cref="KgpFormat.Rgba32"/>.
    /// </remarks>
    public KgpFormat Format { get; }

    /// <summary>
    /// SHA256 hash of the root-frame data for content-addressable deduplication.
    /// </summary>
    public byte[] ContentHash { get; }

    /// <summary>
    /// Creates a new KGP image data entry from raw pixel data.
    /// </summary>
    /// <param name="imageId">The image ID assigned by the client or allocated by the terminal.</param>
    /// <param name="imageNumber">The image number (I key), or 0 if unspecified.</param>
    /// <param name="data">The raw decoded pixel data.</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="format">The pixel format of the stored data.</param>
    public KgpImageData(uint imageId, uint imageNumber, byte[] data, uint width, uint height, KgpFormat format)
        : this(
            imageId,
            imageNumber,
            data,
            width,
            height,
            format,
            SHA256.HashData(data))
    {
    }

    private KgpImageData(
        uint imageId,
        uint imageNumber,
        byte[] data,
        uint width,
        uint height,
        KgpFormat format,
        byte[] contentHash,
        KgpAnimationState? animation = null)
    {
        ImageId = imageId;
        ImageNumber = imageNumber;
        Data = data;
        Width = width;
        Height = height;
        Format = format;
        ContentHash = contentHash;
        _animation = animation;
    }

    /// <summary>
    /// Validates that the data size matches the expected size for the given format and dimensions.
    /// </summary>
    public bool IsDataSizeValid()
    {
        if (Format == KgpFormat.Png)
            return Data.Length > 0;

        var bytesPerPixel = Format == KgpFormat.Rgb24 ? 3 : 4;
        var expectedSize = (long)Width * Height * bytesPerPixel;
        return Data.Length == expectedSize;
    }

    /// <summary>
    /// Whether root-frame pixels are 4-byte aligned (RGBA or PNG).
    /// </summary>
    public bool Is4ByteAligned => Format != KgpFormat.Rgb24;

    internal int FrameCount => _animation?.FrameCount ?? 1;

    internal int CurrentFrameIndex => _animation?.CurrentFrameIndex ?? 0;

    internal int CurrentFrameNumber => CurrentFrameIndex + 1;

    internal long StorageSize => _animation?.StorageSize ?? Data.LongLength;

    internal IReadOnlyList<KgpAnimationFrame>? AnimationFrames
        => _animation?.Frames;

    internal KgpAnimationState? AnimationState => _animation;

    internal byte[] CurrentFrameData
        => _animation?.GetFrame(CurrentFrameIndex).Data ?? Data;

    internal KgpFormat CurrentFrameFormat
        => _animation?.GetFrame(CurrentFrameIndex).Format ?? Format;

    internal bool TryGetFrame(
        int frameNumber,
        out byte[] data,
        out KgpFormat format,
        out int gapMilliseconds)
    {
        if (frameNumber < 1 || frameNumber > FrameCount)
        {
            data = [];
            format = default;
            gapMilliseconds = 0;
            return false;
        }

        if (_animation is null)
        {
            data = Data;
            format = Format;
            gapMilliseconds = 0;
            return true;
        }

        var frame = _animation.GetFrame(frameNumber - 1);
        data = frame.Data;
        format = frame.Format;
        gapMilliseconds = frame.GapMilliseconds;
        return true;
    }

    internal KgpImageData WithAnimation(KgpAnimationState animation)
    {
        ArgumentNullException.ThrowIfNull(animation);
        var root = animation.GetFrame(0);
        var contentHash = ReferenceEquals(root.Data, Data) &&
            Format == root.Format
                ? ContentHash
                : SHA256.HashData(root.Data);
        return new KgpImageData(
            ImageId,
            ImageNumber,
            root.Data,
            Width,
            Height,
            root.Format,
            contentHash,
            animation);
    }

    internal KgpImageData WithImageId(uint imageId)
        => new(
            imageId,
            ImageNumber,
            Data,
            Width,
            Height,
            Format,
            ContentHash,
            _animation);
}
