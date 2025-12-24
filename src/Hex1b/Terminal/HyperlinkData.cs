using System.Security.Cryptography;

namespace Hex1b.Terminal;

/// <summary>
/// Immutable data containing OSC 8 hyperlink information.
/// </summary>
/// <remarks>
/// <para>
/// Hyperlink data is content-addressable: identical URIs and parameters share the same
/// <see cref="HyperlinkData"/> instance. This deduplicates memory when the same
/// hyperlink appears in multiple cells.
/// </para>
/// <para>
/// OSC 8 format: ESC ] 8 ; params ; URI ST
/// where ST is either ESC \ or BEL (\x07)
/// </para>
/// </remarks>
public sealed class HyperlinkData
{
    /// <summary>
    /// Gets the URI of the hyperlink.
    /// </summary>
    public string Uri { get; }

    /// <summary>
    /// Gets the optional parameters from the OSC 8 sequence (e.g., "id=xyz").
    /// </summary>
    public string Parameters { get; }

    /// <summary>
    /// Gets the content hash used for deduplication.
    /// </summary>
    internal byte[] ContentHash { get; }

    internal HyperlinkData(
        string uri,
        string parameters,
        byte[] contentHash)
    {
        Uri = uri;
        Parameters = parameters;
        ContentHash = contentHash;
    }

    /// <summary>
    /// Computes a content hash for a hyperlink based on URI and parameters.
    /// </summary>
    internal static byte[] ComputeHash(string uri, string parameters)
    {
        // Use SHA256 for robust deduplication
        var combined = $"{parameters}|{uri}";
        return SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(combined));
    }

    /// <summary>
    /// Checks if two content hashes are equal.
    /// </summary>
    internal static bool HashEquals(byte[] a, byte[] b)
    {
        return a.AsSpan().SequenceEqual(b.AsSpan());
    }
}
