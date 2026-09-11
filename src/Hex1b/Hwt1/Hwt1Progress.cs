namespace Hex1b;

internal sealed record Hwt1Progress(string State, int? Percentage)
{
    internal static Hwt1Progress From(TerminalProgress progress) => new(progress.State switch
    {
        TerminalProgressState.None => "none",
        TerminalProgressState.Normal => "normal",
        TerminalProgressState.Error => "error",
        TerminalProgressState.Indeterminate => "indeterminate",
        TerminalProgressState.Warning => "warning",
        _ => throw new ArgumentOutOfRangeException(nameof(progress))
    }, progress.Percentage);
}
