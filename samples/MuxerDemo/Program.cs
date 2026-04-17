using Hex1b;
using Hex1b.Muxer;

var socketPath = Path.Combine(Path.GetTempPath(), "hex1b-muxer-demo.sock");

if (args is ["server", ..])
{
    Console.WriteLine($"Starting muxer server on {socketPath}...");
    Console.WriteLine("Press Ctrl+C to stop.");

    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithPtyProcess(options =>
        {
            options.FileName = GetShell();
            if (OperatingSystem.IsWindows())
                options.WindowsPtyMode = WindowsPtyMode.RequireProxy;
        })
        .WithMuxerServer(server => server.ListenUnixSocket(socketPath))
        .Build();

    await terminal.RunAsync();
}
else if (args is ["client", ..])
{
    Console.WriteLine($"Connecting to muxer server at {socketPath}...");

    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithMuxerClient(client => client.ConnectUnixSocket(socketPath))
        .Build();

    await terminal.RunAsync();
}
else
{
    Console.WriteLine("Usage: dotnet run -- server   (start a muxer server)");
    Console.WriteLine("       dotnet run -- client   (connect to a running server)");
}

static string GetShell()
{
    if (OperatingSystem.IsWindows())
        return "pwsh.exe";
    
    var shell = Environment.GetEnvironmentVariable("SHELL");
    return !string.IsNullOrEmpty(shell) ? shell : "bash";
}
