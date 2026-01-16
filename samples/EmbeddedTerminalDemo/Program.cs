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

// State for displaying sizes
string sizeInfo = "Click to get sizes";
int resizeCount = 0;

// Track resizes on the handle
childHandle.Resized += (w, h) =>
{
    resizeCount++;
    sizeInfo = $"Resize #{resizeCount}: {w}x{h}";
};

// Create the TUI app that displays the terminal
await using var displayTerminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((app, options) =>
    {
        return ctx => ctx.VStack(v =>
        [
            // Header (3 lines)
            v.Text("Embedded Terminal Demo - Single bash session"),
            v.Text($"Handle: {childHandle.Width}x{childHandle.Height} | Resizes: {resizeCount}"),
            v.Separator(),
            
            // Single terminal with border - Fill to take remaining space
            v.Border(
                v.VStack(inner => [
                    inner.Terminal(childHandle).Fill(),
                    inner.HStack(h => [
                        h.Button("Get Sizes").OnClick(_ => 
                        {
                            sizeInfo = $"Handle: {childHandle.Width}x{childHandle.Height}";
                        }),
                        h.Text($" {sizeInfo}")
                    ])
                ]),
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
    await childTerminal.DisposeAsync();
}
