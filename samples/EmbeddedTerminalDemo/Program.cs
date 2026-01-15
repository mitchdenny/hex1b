using Hex1b;

// Create a single terminal session with its handle
var childTerminal = Hex1bTerminal.CreateBuilder()
    .WithDimensions(80, 24)
    .WithPtyProcess("bash", "--norc")
    .WithTerminalWidget(out var childHandle)
    .Build();

// Start the terminal in the background
using var cts = new CancellationTokenSource();
var childTask = childTerminal.RunAsync(cts.Token);

// Create the TUI app that displays the terminal
await using var displayTerminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((app, options) =>
    {
        return ctx => ctx.VStack(v =>
        [
            // Header
            v.Text("Embedded Terminal Demo - Single bash session"),
            v.Text("Press Ctrl+C to exit"),
            v.Separator(),
            
            // Single terminal with border
            v.Border(
                v.Terminal(childHandle).Fill(),
                title: "Bash"
            ).Fill()
        ]);
    })
    .Build();

try
{
    await displayTerminal.RunAsync(cts.Token);
}
finally
{
    cts.Cancel();
    
    // Clean up the child terminal
    await childTerminal.DisposeAsync();
}
