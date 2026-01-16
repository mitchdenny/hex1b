using Hex1b;

// Exception logging
File.WriteAllText("/tmp/hex1b-crash.log", $"[{DateTime.Now:HH:mm:ss.fff}] Starting...\n");
AppDomain.CurrentDomain.UnhandledException += (s, e) => 
    File.AppendAllText("/tmp/hex1b-crash.log", $"[{DateTime.Now:HH:mm:ss.fff}] Unhandled: {e.ExceptionObject}\n");
TaskScheduler.UnobservedTaskException += (s, e) =>
    File.AppendAllText("/tmp/hex1b-crash.log", $"[{DateTime.Now:HH:mm:ss.fff}] Unobserved: {e.Exception}\n");

void Log(string message) =>
    File.AppendAllText("/tmp/hex1b-crash.log", $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");

// Create a single terminal session with its handle
Log("Creating child terminal...");
var childTerminal = Hex1bTerminal.CreateBuilder()
    .WithDimensions(80, 24)
    .WithPtyProcess("bash", "--norc")
    .WithTerminalWidget(out var childHandle)
    .Build();

// Log resize events on the handle
childHandle.OutputReceived += () => Log($"[Demo] Handle.OutputReceived (size: {childHandle.Width}x{childHandle.Height})");

Log("Starting child terminal...");
// Start the terminal in the background
using var cts = new CancellationTokenSource();
var childTask = childTerminal.RunAsync(cts.Token);

// Give child terminal a moment to initialize before starting display
// This helps ensure the display terminal's binding happens before output arrives
await Task.Delay(100);

Log("Creating display terminal...");
// Create the TUI app that displays the terminal
await using var displayTerminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((app, options) =>
    {
        return ctx => ctx.VStack(v =>
        [
            // Header (3 lines)
            v.Text("Embedded Terminal Demo - Single bash session"),
            v.Text($"Handle: {childHandle.Width}x{childHandle.Height}"),
            v.Separator(),
            
            // Single terminal with border - Fill to take remaining space
            v.Border(
                v.Terminal(childHandle),
                title: "Bash"
            ).Fill()
        ]);
    })
    .Build();

try
{
    Log("Running display terminal...");
    await displayTerminal.RunAsync(cts.Token);
    Log("Display terminal exited normally");
}
catch (Exception ex)
{
    File.AppendAllText("/tmp/hex1b-crash.log", $"[{DateTime.Now:HH:mm:ss.fff}] Main exception: {ex}\n");
    throw;
}
finally
{
    Log("Cleaning up...");
    cts.Cancel();
    
    // Clean up the child terminal
    await childTerminal.DisposeAsync();
    Log("Done");
}
