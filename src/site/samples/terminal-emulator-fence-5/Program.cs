using Hex1b;

// Build from a Dockerfile (automatically skips rebuild if unchanged)
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithDockerContainer(c =>
    {
        c.DockerfilePath = "./test-env/Dockerfile";
        c.BuildArgs["SDK_VERSION"] = "10.0";
    })
    .Build();

await terminal.RunAsync();
