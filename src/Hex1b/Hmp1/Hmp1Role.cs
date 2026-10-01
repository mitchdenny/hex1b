namespace Hex1b;

/// <summary>
/// Role hint sent to the producer in the
/// <see cref="Hmp1FrameType.ClientHello"/> frame. The producer's
/// presentation adapter does not change behaviour based on this value
/// (in this iteration); it is preserved as a UX-discoverability hint
/// reachable through server-side consumer code.
/// </summary>
/// <remarks>
/// The naming reflects the runtime concept of "primary" — the peer
/// whose chosen dimensions drive the producer PTY's size. Secondaries
/// are still fully interactive (input passes through, primary requests
/// can be made at any time); they simply don't drive the producer's
/// dimensions until they're promoted to primary.
/// </remarks>
public enum Hmp1Role
{
    /// <summary>
    /// Hints that this client wants to drive the producer's
    /// PTY dimensions. The producer does not currently auto-promote
    /// based on this hint; callers must still call
    /// <see cref="IHmp1ConnectionHandle.RequestPrimaryAsync"/>.
    /// </summary>
    Primary,

    /// <summary>
    /// Hints that this client is happy to follow another peer's
    /// dimensions. Default for most consumers.
    /// </summary>
    Secondary,
}
