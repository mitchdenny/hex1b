using System.Net.Sockets;
using Hex1b;
using Hex1b.Input;
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

    // Connect to the muxer server
    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath));
    var stream = new NetworkStream(socket, ownsSocket: true);

    var muxer = new MuxerWorkloadAdapter(stream);
    await muxer.ConnectAsync(CancellationToken.None);

    // Create an embedded terminal that renders the remote session
    await using var embeddedTerminal = Hex1bTerminal.CreateBuilder()
        .WithDimensions(muxer.RemoteWidth, muxer.RemoteHeight)
        .WithWorkload(muxer)
        .WithTerminalWidget(out var handle)
        .Build();

    using var appCts = new CancellationTokenSource();
    Hex1bApp? app = null;

    _ = embeddedTerminal.RunAsync(appCts.Token);

    // Display TUI: terminal widget + info bar with detach chord
    await using var displayTerminal = Hex1bTerminal.CreateBuilder()
        .WithHex1bApp((a, _) =>
        {
            app = a;
            return ctx => ctx.VStack(v =>
            [
                v.Terminal(handle).Fill(),
                v.InfoBar(s =>
                [
                    s.Section("Ctrl+B D"),
                    s.Section("Detach"),
                    s.Spacer(),
                    s.Section($"{muxer.RemoteWidth}\u00d7{muxer.RemoteHeight}")
                ]).WithDefaultSeparator(" ")
            ])
            .WithInputBindings(bindings =>
            {
                bindings.Ctrl().Key(Hex1bKey.B).Then().Key(Hex1bKey.D)
                    .Global().OverridesCapture()
                    .Action(_ => app?.RequestStop(), "Detach");
            });
        })
        .Build();

    await displayTerminal.RunAsync(appCts.Token);
    await appCts.CancelAsync();
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
