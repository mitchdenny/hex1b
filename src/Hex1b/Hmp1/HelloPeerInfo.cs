using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b;

/// <summary>
/// Roster entry inside <see cref="HelloPayload.Peers"/> and
/// <see cref="Hmp1FrameType.PeerJoin"/> payloads.
/// </summary>
internal sealed class HelloPeerInfo
{
    /// <summary>Peer ID.</summary>
    public string PeerId { get; set; } = string.Empty;

    /// <summary>Optional human-readable label.</summary>
    public string? DisplayName { get; set; }
}
