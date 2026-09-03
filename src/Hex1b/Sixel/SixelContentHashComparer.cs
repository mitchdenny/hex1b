using Hex1b.Sixel;

namespace Hex1b;

/// <summary>
/// A byte-content equality comparer used to key Sixel image stores by content
/// hash, mirroring the private comparer <see cref="TrackedObjectStore"/> uses
/// for the same purpose.
/// </summary>
internal sealed class SixelContentHashComparer : IEqualityComparer<byte[]>
{
    internal static readonly SixelContentHashComparer Instance = new();

    private SixelContentHashComparer()
    {
    }

    public bool Equals(byte[]? x, byte[]? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null)
            return false;
        return x.AsSpan().SequenceEqual(y);
    }

    public int GetHashCode(byte[] obj)
    {
        // Content hashes are already cryptographically uniform (SHA-256), so a
        // bounded sample of bytes is sufficient for hash-bucket distribution.
        var hash = new HashCode();
        var step = Math.Max(1, obj.Length / 8);
        for (var i = 0; i < obj.Length; i += step)
            hash.Add(obj[i]);
        hash.Add(obj.Length);
        return hash.ToHashCode();
    }
}
