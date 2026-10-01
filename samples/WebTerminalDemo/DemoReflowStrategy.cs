using System.Text.Json.Serialization;

namespace WebTerminalDemo;

[JsonConverter(typeof(JsonStringEnumConverter<DemoReflowStrategy>))]
internal enum DemoReflowStrategy
{
    Default,
    None,
    Auto,
    Alacritty,
    Foot,
    Ghostty,
    ITerm2,
    Kitty,
    Vte,
    WezTerm,
    WindowsTerminal,
    Xterm
}
