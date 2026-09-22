namespace Hex1b;

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
