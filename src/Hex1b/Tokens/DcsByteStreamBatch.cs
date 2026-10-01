using System.Buffers;
using System.Security.Cryptography;
using Hex1b.Sixel;

namespace Hex1b.Tokens;

internal sealed record DcsByteStreamBatch(
    ReadOnlyMemory<byte> TextBytes,
    IReadOnlyList<DcsFrameBoundary> Frames)
{
    public static DcsByteStreamBatch Empty { get; } = new(
        ReadOnlyMemory<byte>.Empty,
        Array.Empty<DcsFrameBoundary>());
}
