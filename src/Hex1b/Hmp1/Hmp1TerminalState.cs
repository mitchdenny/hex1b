namespace Hex1b;

internal sealed record Hmp1TerminalState(
    string? PeerId, string? PrimaryPeerId, int Width, int Height, bool Connected)
{
    internal bool IsPrimary => Connected && PeerId is not null && PeerId == PrimaryPeerId;
}
