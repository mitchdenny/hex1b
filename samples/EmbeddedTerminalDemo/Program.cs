using Hex1b;

// Create two terminal sessions with their handles
var leftTerminal = Hex1bTerminal.CreateBuilder()
    .WithDimensions(40, 20)
    .WithPtyProcess("bash", "--norc")
    .WithTerminalWidget(out var leftHandle)
    .Build();

var rightTerminal = Hex1bTerminal.CreateBuilder()
    .WithDimensions(40, 20)
    .WithPtyProcess("bash", "--norc")
    .WithTerminalWidget(out var rightHandle)
    .Build();

// Start both terminals in the background
using var cts = new CancellationTokenSource();
var leftTask = leftTerminal.RunAsync(cts.Token);
var rightTask = rightTerminal.RunAsync(cts.Token);

// Create the TUI app that displays both terminals side by side
await using var displayTerminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((app, options) =>
    {
        return ctx => ctx.VStack(v =>
        [
            // Header
            v.Text("Embedded Terminal Demo - Two bash sessions side by side"),
            v.Text("Press Ctrl+C to exit"),
            v.Separator(),
            
            // Two terminals in a horizontal splitter
            v.HSplitter(
                // Left pane: first terminal
                v.Border(
                    v.Terminal(leftHandle),
                    title: "Terminal 1"
                ),
                // Right pane: second terminal
                v.Border(
                    v.Terminal(rightHandle),
                    title: "Terminal 2"
                ),
                leftWidth: 45
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
    
    // Clean up the child terminals
    await leftTerminal.DisposeAsync();
    await rightTerminal.DisposeAsync();
}
