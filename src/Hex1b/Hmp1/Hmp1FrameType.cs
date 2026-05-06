namespace Hex1b;

/// <summary>
/// Frame types for the Hex1b Muxer Protocol (HMP v1).
/// </summary>
/// <remarks>
/// See docs/muxer-protocol.md for the full protocol specification.
/// </remarks>
internal enum Hmp1FrameType : byte
{
    /// <summary>
    /// Server → Client. Sent once on connection after the server has
    /// received a <see cref="ClientHello"/> from the client.
    /// Payload: JSON <see cref="HelloPayload"/>. The protocol version
    /// is always <see cref="Hmp1Protocol.Version"/>; payload also carries
    /// the assigned <c>peerId</c>, current <c>primaryPeerId</c>, and roster.
    /// </summary>
    Hello = 0x01,

    /// <summary>
    /// Server → Client. Sent immediately after <see cref="Hello"/>.
    /// Payload: raw ANSI bytes representing the full current screen content.
    /// </summary>
    StateSync = 0x02,

    /// <summary>
    /// Server → Client. Incremental terminal output.
    /// Payload: raw ANSI bytes.
    /// </summary>
    Output = 0x03,

    /// <summary>
    /// Client → Server. Keyboard input.
    /// Payload: raw input bytes.
    /// </summary>
    Input = 0x04,

    /// <summary>
    /// Bidirectional. Terminal resize notification. From the client this
    /// frame is silently dropped by the server unless the sending peer
    /// is the current primary (see <see cref="RequestPrimary"/>).
    /// Payload: <c>width:4B LE</c> + <c>height:4B LE</c> (8 bytes total).
    /// </summary>
    Resize = 0x05,

    /// <summary>
    /// Server → Client. Terminal session has ended.
    /// Payload: <c>exitCode:4B LE</c> (4 bytes).
    /// </summary>
    Exit = 0x06,

    /// <summary>
    /// Client → Server. Asks the server to make this peer the primary
    /// at the given dimensions. The server always grants in this iteration;
    /// PTY is resized and a <see cref="RoleChange"/> is broadcast to all peers.
    /// Payload: JSON <see cref="RequestPrimaryPayload"/>.
    /// </summary>
    RequestPrimary = 0x07,

    /// <summary>
    /// Server → Client (broadcast). Sent when the primary peer changes
    /// (including on transition to no primary after the previous primary
    /// disconnects).
    /// Payload: JSON <see cref="RoleChangePayload"/>.
    /// </summary>
    RoleChange = 0x08,

    /// <summary>
    /// Server → Client (broadcast to existing peers when a new peer joins).
    /// Payload: JSON <see cref="PeerJoinPayload"/>.
    /// </summary>
    PeerJoin = 0x09,

    /// <summary>
    /// Server → Client (broadcast when a peer disconnects).
    /// Payload: JSON <see cref="PeerLeavePayload"/>.
    /// </summary>
    PeerLeave = 0x0A,

    /// <summary>
    /// Client → Server. Sent by the client immediately on connect, before
    /// the server emits its <see cref="Hello"/>. Lets the client declare
    /// a friendly name and a default role hint.
    /// Payload: JSON <see cref="ClientHelloPayload"/>.
    /// </summary>
    ClientHello = 0x0B
}
