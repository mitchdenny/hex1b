using Hex1b;

// Start an interactive shell with PTY
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithPtyProcess("bash", "-i")
    .Build();

await terminal.RunAsync();
