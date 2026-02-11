using Hex1b;

// Create a terminal with an embedded bash session
var terminal = Hex1bTerminal.CreateBuilder()
    .WithPtyProcess("bash", "--norc")
    .WithTerminalWidget(out var bashHandle)
    .Build();

// Start the child terminal in the background
_ = terminal.RunAsync();

// Use the widget in your Hex1bApp
await using var app = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => 
        ctx.Border(
            ctx.Terminal(bashHandle)
        ).Title("Embedded Terminal"))
    .Build();

await app.RunAsync();
