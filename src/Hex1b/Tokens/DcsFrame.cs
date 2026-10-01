using System.Buffers;
using System.Security.Cryptography;
using Hex1b.Sixel;

namespace Hex1b.Tokens;

internal sealed record DcsFrame(
    DcsSequenceStatus Status,
    DcsIntroducer Introducer,
    ReadOnlyMemory<byte> RetainedContent,
    long ByteCount,
    bool RetentionLimitExceeded,
    byte[] ContentHash,
    SixelParseResult SixelResult);
