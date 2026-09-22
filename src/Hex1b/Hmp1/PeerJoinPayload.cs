using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b;

/// <summary>
/// JSON payload for the <see cref="Hmp1FrameType.PeerJoin"/> frame.
/// </summary>
internal sealed class PeerJoinPayload
{
    /// <summary>Peer ID of the joining client.</summary>
    public string PeerId { get; set; } = string.Empty;

    /// <summary>Optional human-readable label of the joining client.</summary>
    public string? DisplayName { get; set; }
}
