namespace Hex1b.Tests.NativeAot;

internal sealed record Scenario(string Name, IReadOnlyList<CodeFamily> Required, IReadOnlyList<CodeFamily> Forbidden)
{
    public string ConsumerName { get; init; } = Name;
    public IReadOnlyDictionary<string, bool> FeatureSwitches { get; init; } = new Dictionary<string, bool>();

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

    public const string PoolingSwitch = "Hex1b.SurfacePooling.IsSupported";
    public static readonly CodeFamily App = new("app rendering", @"^Hex1b_Hex1b_Hex1bApp_");
    public static readonly CodeFamily Surface = new("surface rendering", @"^Hex1b_Hex1b_Surfaces_Surface_");
    public static readonly CodeFamily SurfaceNode = new("surface layer rendering", @"^Hex1b_Hex1b_Nodes_SurfaceNode_");
    public static readonly CodeFamily BarChart = new("bar chart rendering", @"^Hex1b_Hex1b_Nodes_BarChartNode_");
    public static readonly CodeFamily EffectPanel = new("effect panel rendering", @"^Hex1b_Hex1b_Nodes_EffectPanelNode_");
    public static readonly CodeFamily Pool = new("surface pooling implementation", @"^Hex1b_Hex1b_Surfaces_SurfacePool_");

    public static Scenario HeadlessSurfacePooling(bool? supported) => new(
        "SurfacePooling" + (supported is null ? "Default" : supported.Value ? "Enabled" : "Disabled"),
        supported == false ? [Terminal, App, Surface, SurfaceNode, BarChart, EffectPanel] : [Terminal, App, Surface, SurfaceNode, BarChart, EffectPanel, Pool],
        supported == false ? [Pool] : [])
    {
        ConsumerName = "HeadlessSurfacePooling",
        FeatureSwitches = supported is null ? new Dictionary<string, bool>() : new Dictionary<string, bool> { [PoolingSwitch] = supported.Value }
    };
}
