namespace Hex1b.Aot.Tests;

internal sealed record Scenario(string Name, IReadOnlyList<CodeFamily> Required, IReadOnlyList<CodeFamily> Forbidden)
{
    // Anchor on the declaring family: generic arguments can name unretained types.
    public static readonly CodeFamily Terminal = new("terminal", @"^Hex1b_Hex1b_Hex1bTerminal_");
    public static readonly CodeFamily Builder = new("terminal builder", @"^Hex1b_Hex1b_Hex1bTerminalBuilder_");
    public static readonly CodeFamily Player = new("tape player", @"^Hex1b_Hex1b_Automation_TapePlayer_");
    public static readonly CodeFamily ExistingPlayback = new("existing-terminal playback",
        @"^Hex1b_Hex1b_Automation_TapePlayer_[^<]*PlayWithExistingTerminalAsync");
    public static readonly CodeFamily CreatedPlayback = new("created-terminal playback",
        @"^Hex1b_Hex1b_Automation_TapePlayer_[^<]*PlayWithCreatedTerminalAsync");
    public static readonly CodeFamily OwnedBuilder = new("ConfigureOwnedBuilder",
        @"^Hex1b_Hex1b_Automation_TapePlayer_[^<]*ConfigureOwnedBuilder");
    public static readonly CodeFamily ChildProcess = new("child-process implementation",
        @"^Hex1b_Hex1b_(?:Hex1bTerminalChildProcess|StandardProcessWorkloadAdapter|UnixPtyHandle|NativeUnixPtyStartupInterop|WindowsPtyHandle|WindowsProxyPtyHandle|WindowsShimPtyHandle)_");

    public static readonly Scenario DirectTerminal = new("DirectTerminal", [Terminal], [Player, Builder, ChildProcess]);
    public static readonly Scenario ExistingTerminalTape = new("ExistingTerminalTape",
        [Terminal, ExistingPlayback], [Builder, ChildProcess, OwnedBuilder, CreatedPlayback]);
    public static readonly Scenario CreatedTerminalTape = new("CreatedTerminalTape", [Terminal, CreatedPlayback, Builder], []);
}
