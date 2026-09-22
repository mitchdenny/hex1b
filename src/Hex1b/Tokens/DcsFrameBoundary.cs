using System.Buffers;
using System.Security.Cryptography;
using Hex1b.Sixel;

namespace Hex1b.Tokens;

internal readonly record struct DcsFrameBoundary(int TextByteOffset, DcsFrame Frame);
