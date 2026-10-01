using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b;

/// <summary>
/// JSON payload for the <see cref="Hmp1FrameType.ClientHello"/> frame
/// (client → server, sent immediately on connect).
/// </summary>
internal sealed class ClientHelloPayload
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int CommandMarkHistoryVersion { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ScrollbackHistoryVersion { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ScrollbackHistoryRows { get; set; }

    /// <summary>
    /// Optional human-readable label that the producer surfaces in its peer
    /// roster (e.g. "dashboard", "aspire-cli").
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Optional default-role hint:
    /// <c>"secondary"</c> requests that the client not be auto-promoted on first
    /// attach; <c>"primary"</c> is the inverse hint. Currently the producer
    /// never auto-promotes, so this is preserved only as a UX hint reachable
    /// to the server-side consumer code. The strongly-typed
    /// <see cref="Hex1b.Hmp1Role"/> enum is the recommended surface; this
    /// string is what's serialised on the wire.
    /// </summary>
    public string? DefaultRole { get; set; }
}
