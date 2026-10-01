using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx =>
        ctx.Text("Hello from Hex1b!"))
    .WithDiagnostics()  // Enables CLI discovery
    .Build();

await terminal.RunAsync();
