using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b;

/// <summary>
/// JSON payload for the <see cref="Hmp1FrameType.RoleChange"/> frame.
/// </summary>
internal sealed class RoleChangePayload
{
    /// <summary>The peer ID of the new primary, or null when no peer is primary.</summary>
    public string? PrimaryPeerId { get; set; }

    /// <summary>Current PTY width.</summary>
    public int Width { get; set; }

    /// <summary>Current PTY height.</summary>
    public int Height { get; set; }

    /// <summary>
    /// Reason for the change (free-form, currently one of <c>"RequestPrimary"</c>
    /// or <c>"PrimaryDisconnected"</c>).
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}
