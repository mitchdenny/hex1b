using System.Security.Cryptography;

namespace Hex1b;

/// <summary>
/// Immutable data containing Sixel graphics information.
/// </summary>
/// <remarks>
/// <para>
/// Sixel data is content-addressable: identical payloads share the same
/// <see cref="SixelData"/> instance. This deduplicates memory when the same
/// image appears in multiple cells or is re-rendered.
/// </para>
/// <para>
/// The raw DCS sequence is stored so it can be re-emitted during rendering.
/// </para>
/// </remarks>
public sealed class SixelData
{
    /// <summary>
    /// Gets the raw Sixel DCS sequence (ESC P ... ESC \).
    /// </summary>
    public string Payload { get; }

    /// <summary>
    /// Gets the width of the Sixel image in cells.
    /// </summary>
    public int WidthInCells { get; }

    /// <summary>
    /// Gets the height of the Sixel image in cells.
    /// </summary>
    public int HeightInCells { get; }

    /// <summary>
    /// Gets the content hash used for deduplication.
    /// </summary>
    internal byte[] ContentHash { get; }

    internal SixelData(
        string payload,
        int widthInCells,
        int heightInCells,
        byte[] contentHash)
    {
        Payload = payload;
        WidthInCells = widthInCells;
        HeightInCells = heightInCells;
        ContentHash = contentHash;
    }

    /// <summary>
    /// Computes a content hash for a Sixel payload.
    /// </summary>
    internal static byte[] ComputeHash(string payload)
    {
        // Use SHA256 for robust deduplication
        return SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload));
    }

    /// <summary>
    /// Checks if two content hashes are equal.
    /// </summary>
    internal static bool HashEquals(byte[] a, byte[] b)
    {
        return a.AsSpan().SequenceEqual(b.AsSpan());
    }
}
