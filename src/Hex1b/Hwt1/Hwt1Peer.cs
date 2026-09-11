namespace Hex1b;

internal sealed record Hwt1Peer(string? Id, string? PrimaryId, bool IsPrimary)
{
    internal static Hwt1Peer Standalone { get; } = new(null, null, true);
    internal static Hwt1Peer Unconnected { get; } = new(null, null, false);
}
