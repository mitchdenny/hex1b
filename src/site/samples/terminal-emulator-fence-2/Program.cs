using Hex1b;

// Or run a command without PTY (for build tools, scripts)
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithProcess("dotnet", "build")
    .WithHeadless()
    .Build();

await terminal.RunAsync();
