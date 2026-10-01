using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b;

/// <summary>
/// JSON payload for the <see cref="Hmp1FrameType.RequestPrimary"/> frame.
/// </summary>
internal sealed class RequestPrimaryPayload
{
    /// <summary>Requested PTY width in columns.</summary>
    public int Cols { get; set; }

    /// <summary>Requested PTY height in rows.</summary>
    public int Rows { get; set; }
}
