#:package Hex1b@0.172.0
#:property ImportDirectoryBuildProps=false

using Hex1b;
using Hex1b.Automation;

using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
// Use the released package to host the PTY; only the child builds repository sources.
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHeadless(new TerminalCapabilities { SupportsKgp = true })
    .WithDimensions(80, 24)
    .WithPtyProcess("dotnet", "run", "--project", "samples/KgpCloudDemo", "--", "--motes", "4", "--frames", "120")
    .Build();
var run = terminal.RunAsync(timeout.Token);
await new Hex1bTerminalInputSequenceBuilder()
    .WaitUntil(snapshot => snapshot.InAlternateScreen && snapshot.KgpPlacements.Count > 0
        ? true
        : run.IsCompleted
            ? throw new InvalidOperationException($"Demo exited before rendering graphics (exit code {run.GetAwaiter().GetResult()}).")
            : false,
        TimeSpan.FromMinutes(2), "KgpCloudDemo renders kitty graphics after dotnet run")
    .Build()
    .ApplyAsync(terminal, timeout.Token);
if (await run != 0)
    throw new InvalidOperationException("KgpCloudDemo exited unsuccessfully.");
Console.WriteLine("Repository dotnet run smoke test passed.");
