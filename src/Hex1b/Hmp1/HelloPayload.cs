using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b;

/// <summary>
/// JSON payload for the <see cref="Hmp1FrameType.Hello"/> frame.
/// </summary>
internal sealed class HelloPayload
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int CommandMarkHistoryVersion { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ScrollbackHistoryVersion { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ScrollbackHistoryRows { get; set; }

    /// <summary>Protocol version. Always <see cref="Hmp1Protocol.Version"/>.</summary>
    public int Version { get; set; }

    /// <summary>Terminal width in columns.</summary>
    public int Width { get; set; }

    /// <summary>Terminal height in rows.</summary>
    public int Height { get; set; }

    /// <summary>
    /// Opaque peer ID assigned to the receiving client by the producer.
    /// Stable for the lifetime of the connection. Clients must treat the
    /// string as opaque (no parsing, no format assumptions) — see the
    /// "Peer IDs" section of <c>docs/muxer-protocol.md</c> for the full
    /// opacity contract.
    /// </summary>
    public string? PeerId { get; set; }

    /// <summary>
    /// The peer ID of the current primary, or null when no peer is currently
    /// primary (initial state, or after the previous primary disconnected).
    /// </summary>
    public string? PrimaryPeerId { get; set; }

    /// <summary>
    /// Roster of other peers currently connected to the same producer
    /// (excluding the receiving client).
    /// </summary>
    public List<HelloPeerInfo>? Peers { get; set; }
}
