using System.Security.Cryptography;

namespace Hex1b.Kgp;

/// <summary>
/// Immutable data for a KGP (Kitty Graphics Protocol) image placement on a surface cell.
/// </summary>
/// <remarks>
/// <para>
/// Similar to <see cref="SixelData"/>, this is content-addressable: identical payloads
/// share the same instance. The raw APC sequence is stored for emission during rendering.
/// </para>
/// </remarks>
public sealed class KgpCellData
{
    /// <summary>
    /// Gets the complete KGP APC escape sequence (ESC _G ... ESC \).
    /// </summary>
    public string Payload { get; }

    /// <summary>
    /// Gets the width of the placement in terminal columns.
    /// </summary>
    public int WidthInCells { get; }

    /// <summary>
    /// Gets the height of the placement in terminal rows.
    /// </summary>
    public int HeightInCells { get; }

    /// <summary>
    /// Gets the content hash for deduplication.
    /// </summary>
    public byte[] ContentHash { get; }

    /// <summary>
    /// Creates a new KGP cell data instance.
    /// </summary>
    /// <param name="payload">The complete APC escape sequence.</param>
    /// <param name="widthInCells">Width in terminal columns.</param>
    /// <param name="heightInCells">Height in terminal rows.</param>
    public KgpCellData(string payload, int widthInCells, int heightInCells)
    {
        Payload = payload;
        WidthInCells = widthInCells;
        HeightInCells = heightInCells;
        ContentHash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload));
    }

    /// <summary>
    /// Compares content hashes for equality.
    /// </summary>
    public static bool HashEquals(byte[] a, byte[] b)
        => a.AsSpan().SequenceEqual(b);
}
