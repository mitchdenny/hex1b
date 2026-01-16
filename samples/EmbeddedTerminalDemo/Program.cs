using Hex1b;

// Create two terminal sessions with their handles
var leftTerminal = Hex1bTerminal.CreateBuilder()
    .WithDimensions(40, 24)
    .WithPtyProcess("bash", "--norc")
    .WithTerminalWidget(out var leftHandle)
    .Build();

var rightTerminal = Hex1bTerminal.CreateBuilder()
    .WithDimensions(40, 24)
    .WithPtyProcess("bash", "--norc")
    .WithTerminalWidget(out var rightHandle)
    .Build();

// Start the terminals in the background
using var cts = new CancellationTokenSource();
var leftTask = leftTerminal.RunAsync(cts.Token);
var rightTask = rightTerminal.RunAsync(cts.Token);

// Create the TUI app that displays both terminals
await using var displayTerminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((app, options) =>
    {
        return ctx => ctx.VStack(v =>
        [
            // Header
            v.Text("Embedded Terminal Demo - Two bash sessions with splitter"),
            v.Separator(),
            
            // Horizontal splitter with two terminals
            v.HSplitter(
                // Left pane
                v.Border(
                    v.Terminal(leftHandle).Fill(),
                    title: "Left Terminal"
                ),
                // Right pane
                v.Border(
                    v.Terminal(rightHandle).Fill(),
                    title: "Right Terminal"
                ),
                leftWidth: 40
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
    await leftTerminal.DisposeAsync();
    await rightTerminal.DisposeAsync();
}
