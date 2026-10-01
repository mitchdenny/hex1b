using System.Buffers;
using System.Security.Cryptography;
using Hex1b.Sixel;

namespace Hex1b.Tokens;

internal readonly record struct DcsIntroducer(
    byte? PrivateMarker,
    IReadOnlyList<int?> Parameters,
    IReadOnlyList<byte> Intermediates,
    byte? FinalByte,
    bool IsValid)
{
    public bool IsSixel =>
        IsValid &&
        PrivateMarker is null &&
        Intermediates.Count == 0 &&
        FinalByte == (byte)'q';
}
