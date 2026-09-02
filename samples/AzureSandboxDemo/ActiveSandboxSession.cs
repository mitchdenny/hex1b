using Hex1b;

namespace AzureSandboxDemo;

internal sealed record ActiveSandboxSession(
    string SandboxId,
    Hex1bTerminal Terminal,
    TerminalWidgetHandle Handle);
