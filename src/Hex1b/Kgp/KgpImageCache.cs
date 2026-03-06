using System.Security.Cryptography;

namespace Hex1b;

/// <summary>
/// Tracks which images have been transmitted to the terminal in the current session.
/// Used to avoid re-transmitting identical image data — once an image is transmitted
/// with a given ID, subsequent renders can use <c>a=p</c> (put) instead of <c>a=T</c>.
/// </summary>
/// <remarks>
/// Per the KGP spec, images persist for the lifetime of the active terminal buffer.
/// Kitty's quota is 320MB per buffer. This cache mirrors that state so nodes can
/// determine whether to transmit or just place.
/// </remarks>
internal sealed class KgpImageCache
{
    private readonly Dictionary<string, uint> _transmittedImages = new();
    private uint _nextImageId = 1;

    /// <summary>
    /// Allocates a new unique image ID.
    /// </summary>
    public uint AllocateImageId() => _nextImageId++;

    /// <summary>
    /// Checks whether an image with the given content hash has already been transmitted.
    /// </summary>
    /// <param name="contentHash">SHA256 hash of the pixel data.</param>
    /// <param name="imageId">The image ID assigned during transmission, if found.</param>
    /// <returns><c>true</c> if the image was previously transmitted.</returns>
    public bool TryGetImageId(byte[] contentHash, out uint imageId)
    {
        var key = Convert.ToHexString(contentHash);
        return _transmittedImages.TryGetValue(key, out imageId);
    }

    /// <summary>
    /// Registers a newly transmitted image in the cache.
    /// </summary>
    /// <param name="contentHash">SHA256 hash of the pixel data.</param>
    /// <param name="imageId">The image ID used in the transmission.</param>
    public void RegisterTransmission(byte[] contentHash, uint imageId)
    {
        var key = Convert.ToHexString(contentHash);
        _transmittedImages[key] = imageId;
    }

    /// <summary>
    /// Clears all cached entries (e.g., on buffer switch or app dispose).
    /// </summary>
    public void Clear()
    {
        _transmittedImages.Clear();
    }

    /// <summary>
    /// Gets the number of images currently tracked.
    /// </summary>
    public int Count => _transmittedImages.Count;
}
