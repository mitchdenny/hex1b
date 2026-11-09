namespace Hex1b;

/// <summary>
/// Specifies how a workload receives presentation input and injected input events.
/// </summary>
public enum Hex1bTerminalInputMode
{
    /// <summary>
    /// Receives original presentation bytes and encoded injected events.
    /// The workload performs its own input decoding.
    /// </summary>
    RawBytes,

    /// <summary>
    /// Receives parsed keyboard, mouse, and streaming paste events.
    /// The terminal handles UTF-8 buffering and escape-sequence timeouts.
    /// </summary>
    ParsedEvents
}
