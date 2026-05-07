using System.Security.Cryptography;

namespace Hex1b;

/// <summary>
/// Base-36 (<c>0-9a-z</c>) encoder used for compact human-friendly
/// identifiers in HMP1. Used to auto-generate
/// <see cref="Hmp1ClientOptions.DisplayName"/> when the caller doesn't
/// supply one.
/// </summary>
internal static class Hmp1Base36
{
    private static ReadOnlySpan<char> Alphabet => "0123456789abcdefghijklmnopqrstuvwxyz";

    /// <summary>
    /// Generates a 13-character base-36 identifier from 8 bytes of
    /// cryptographically-strong randomness (~64 bits of entropy).
    /// Sufficient to avoid collisions across producer lifetimes for any
    /// realistic peer count.
    /// </summary>
    public static string GenerateDisplayName()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);

        // Treat the 8 bytes as a big-endian ulong, then divmod into base36.
        // 13 chars covers ulong.MaxValue exactly (36^13 > 2^64).
        ulong value =
            ((ulong)bytes[0] << 56) |
            ((ulong)bytes[1] << 48) |
            ((ulong)bytes[2] << 40) |
            ((ulong)bytes[3] << 32) |
            ((ulong)bytes[4] << 24) |
            ((ulong)bytes[5] << 16) |
            ((ulong)bytes[6] << 8) |
            bytes[7];

        Span<char> buffer = stackalloc char[13];
        for (var i = buffer.Length - 1; i >= 0; i--)
        {
            buffer[i] = Alphabet[(int)(value % 36)];
            value /= 36;
        }
        return new string(buffer);
    }
}
