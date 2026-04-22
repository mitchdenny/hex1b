namespace Hex1b.Hmp1;

/// <summary>
/// Frame types for the Hex1b Muxer Protocol (HMP v1).
/// </summary>
/// <remarks>
/// See docs/muxer-protocol.md for the full protocol specification.
/// </remarks>
public enum Hmp1FrameType : byte
{
    /// <summary>
    /// Server → Client. Sent once on connection.
    /// Payload: JSON <c>{"version":1,"width":N,"height":N}</c>.
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
    /// Bidirectional. Terminal resize notification.
    /// Payload: <c>width:4B LE</c> + <c>height:4B LE</c> (8 bytes total).
    /// </summary>
    Resize = 0x05,

    /// <summary>
    /// Server → Client. Terminal session has ended.
    /// Payload: <c>exitCode:4B LE</c> (4 bytes).
    /// </summary>
    Exit = 0x06
}
