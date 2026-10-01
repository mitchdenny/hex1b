using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b;

/// <summary>
/// A single HMP protocol frame.
/// </summary>
/// <param name="Type">The frame type.</param>
/// <param name="Payload">The raw payload bytes.</param>
internal readonly record struct Hmp1Frame(Hmp1FrameType Type, ReadOnlyMemory<byte> Payload);
