using Hex1b.Reflow;

namespace WebTerminalDemo;

internal sealed record CreateTerminalRequest(
    string Scene = "mixed", int Columns = 100, int Rows = 30, string? Name = null,
    DemoReflowStrategy ReflowStrategy = DemoReflowStrategy.Default)
{
    public DemoReflowStrategy ResolvedReflowStrategy => ReflowStrategy == DemoReflowStrategy.Default
        ? Scene == "shell" ? DemoReflowStrategy.Ghostty : DemoReflowStrategy.None
        : ReflowStrategy;

    public ITerminalReflowProvider? GetReflowProvider() => ResolvedReflowStrategy switch
    {
        DemoReflowStrategy.None => null,
        DemoReflowStrategy.Auto => AutoReflowStrategy.Instance,
        DemoReflowStrategy.Alacritty => AlacrittyReflowStrategy.Instance,
        DemoReflowStrategy.Foot => FootReflowStrategy.Instance,
        DemoReflowStrategy.Ghostty => GhosttyReflowStrategy.Instance,
        DemoReflowStrategy.ITerm2 => ITerm2ReflowStrategy.Instance,
        DemoReflowStrategy.Kitty => KittyReflowStrategy.Instance,
        DemoReflowStrategy.Vte => VteReflowStrategy.Instance,
        DemoReflowStrategy.WezTerm => WezTermReflowStrategy.Instance,
        DemoReflowStrategy.WindowsTerminal => WindowsTerminalReflowStrategy.Instance,
        DemoReflowStrategy.Xterm => XtermReflowStrategy.Instance,
        _ => throw new ArgumentOutOfRangeException(nameof(ReflowStrategy), ReflowStrategy, "Unknown reflow strategy.")
    };
}
