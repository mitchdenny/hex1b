using Hex1b;

// Start a container with the default .NET SDK image
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithDockerContainer()
    .Build();

await terminal.RunAsync();
