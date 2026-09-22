using System.Buffers;
using System.Security.Cryptography;
using Hex1b.Sixel;

namespace Hex1b.Tokens;

internal enum DcsSequenceStatus
{
    Complete,
    Cancelled,
    Malformed,
    Unterminated,
}
