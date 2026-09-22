using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b;

/// <summary>
/// JSON payload for the <see cref="Hmp1FrameType.PeerLeave"/> frame.
/// </summary>
internal sealed class PeerLeavePayload
{
    /// <summary>Peer ID of the leaving client.</summary>
    public string PeerId { get; set; } = string.Empty;
}
