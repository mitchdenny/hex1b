using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

namespace Hex1b;

/// <summary>
/// A peer connected to the same producer as this adapter.
/// </summary>
/// <param name="PeerId">The peer's ID.</param>
/// <param name="DisplayName">Optional human-readable label.</param>
public readonly record struct PeerInfo(string PeerId, string? DisplayName);
