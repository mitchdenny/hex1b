using System.Threading.Channels;

namespace Hex1b.Diagnostics;

/// <summary>
/// A single frame received from an attach session.
/// </summary>
public readonly record struct AttachFrame(AttachFrameType Type, string? Data);
