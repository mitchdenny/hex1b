using Hex1b;

// Configure the container with a specific image and environment
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithDockerContainer(c =>
    {
        c.Image = "ubuntu:24.04";
        c.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        c.Volumes.Add("/host/data:/container/data:ro");
        c.WorkingDirectory = "/app";
    })
    .Build();

await terminal.RunAsync();
