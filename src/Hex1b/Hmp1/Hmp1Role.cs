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

/// <summary>
/// Wire-format helpers for <see cref="Hmp1Role"/>.
/// </summary>
internal static class Hmp1RoleExtensions
{
    /// <summary>
    /// Maps the enum value to the JSON wire string sent in the
    /// ClientHello payload.
    /// </summary>
    public static string ToWireString(this Hmp1Role role) => role switch
    {
        Hmp1Role.Primary => "primary",
        Hmp1Role.Secondary => "secondary",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    /// <summary>
    /// Inverse of <see cref="ToWireString"/>. Returns null on unknown /
    /// missing wire strings, including the legacy <c>"viewer"</c> and
    /// <c>"interactive"</c> values; callers should treat null as "no hint".
    /// </summary>
    public static Hmp1Role? TryParseWireString(string? wire) => wire switch
    {
        "primary" => Hmp1Role.Primary,
        "secondary" => Hmp1Role.Secondary,
        _ => null,
    };
}
