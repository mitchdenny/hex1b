using System.Net.Sockets;
using Hex1b;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("hex1bpty only runs on Windows.");
    return 1;
}

var socketPath = TryGetSocketPath(args);
if (string.IsNullOrWhiteSpace(socketPath))
{
    Console.Error.WriteLine("Usage: hex1bpty --socket <path>");
    return 1;
}

DeleteSocketFile(socketPath);

await using var ptyHandle = new WindowsPtyHandle();
using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
listener.Bind(new UnixDomainSocketEndPoint(socketPath));
listener.Listen(1);

using var cts = new CancellationTokenSource();

try
{
    using var clientSocket = await listener.AcceptAsync(cts.Token);
    await using var stream = new NetworkStream(clientSocket, ownsSocket: false);

    var launchFrame = await WindowsPtyShimProtocol.ReadFrameAsync(stream, cts.Token);
    if (launchFrame is null || launchFrame.Value.Type != WindowsPtyShimFrameType.LaunchRequest)
    {
        throw new InvalidOperationException("The first PTY shim frame must be a launch request.");
    }

    var request = WindowsPtyShimProtocol.ReadJson<WindowsPtyShimLaunchRequest>(launchFrame.Value.Payload);
    request.Environment["HEX1B_PTY_SHIM_ACTIVE"] = "1";
    await ptyHandle.StartAsync(
        request.FileName,
        request.Arguments,
        request.WorkingDirectory,
        request.Environment,
        request.Width,
        request.Height,
        cts.Token);

    await WindowsPtyShimProtocol.WriteJsonAsync(
        stream,
        WindowsPtyShimFrameType.Started,
        new WindowsPtyShimStartedResponse(ptyHandle.ProcessId),
        cts.Token);

    var exitTask = ptyHandle.WaitForExitAsync(cts.Token);
    var outputPumpTask = PumpOutputAsync(stream, ptyHandle, exitTask, cts.Token);
    var commandPumpTask = PumpCommandsAsync(stream, ptyHandle, cts);

    var exitCode = await exitTask;

    try
    {
        await WindowsPtyShimProtocol.WriteJsonAsync(
            stream,
            WindowsPtyShimFrameType.Exit,
            new WindowsPtyShimExitNotification(exitCode),
            CancellationToken.None);
    }
    catch
    {
    }

    cts.Cancel();

    try
    {
        await Task.WhenAll(outputPumpTask, commandPumpTask);
    }
    catch (OperationCanceledException)
    {
    }

    return exitCode;
}
catch (OperationCanceledException)
{
    return 0;
}
catch (Exception ex)
{
    try
    {
        using var errorSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await errorSocket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), CancellationToken.None);
        await using var errorStream = new NetworkStream(errorSocket, ownsSocket: true);
        await WindowsPtyShimProtocol.WriteJsonAsync(
            errorStream,
            WindowsPtyShimFrameType.Error,
            new WindowsPtyShimErrorResponse(ex.Message),
            CancellationToken.None);
    }
    catch
    {
    }

    Console.Error.WriteLine(ex);
    return 1;
}
finally
{
    DeleteSocketFile(socketPath);
}

static async Task PumpOutputAsync(Stream stream, WindowsPtyHandle ptyHandle, Task<int> exitTask, CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        var data = await ptyHandle.ReadAsync(ct);
        if (!data.IsEmpty)
        {
            await WindowsPtyShimProtocol.WriteFrameAsync(stream, WindowsPtyShimFrameType.Output, data, ct);
            continue;
        }

        if (exitTask.IsCompleted)
        {
            return;
        }

        await Task.Delay(10, ct);
    }
}

static async Task PumpCommandsAsync(Stream stream, WindowsPtyHandle ptyHandle, CancellationTokenSource cts)
{
    while (!cts.IsCancellationRequested)
    {
        var frame = await WindowsPtyShimProtocol.ReadFrameAsync(stream, cts.Token);
        if (frame is null)
        {
            cts.Cancel();
            return;
        }

        switch (frame.Value.Type)
        {
            case WindowsPtyShimFrameType.Input:
                await ptyHandle.WriteAsync(frame.Value.Payload, cts.Token);
                break;

            case WindowsPtyShimFrameType.Resize:
                var resize = WindowsPtyShimProtocol.ReadJson<WindowsPtyShimResizeRequest>(frame.Value.Payload);
                ptyHandle.Resize(resize.Width, resize.Height);
                break;

            case WindowsPtyShimFrameType.Kill:
                ptyHandle.Kill(15);
                break;

            case WindowsPtyShimFrameType.Shutdown:
                ptyHandle.Kill(15);
                cts.Cancel();
                return;
        }
    }
}

static string? TryGetSocketPath(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], "--socket", StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

static void DeleteSocketFile(string? socketPath)
{
    if (string.IsNullOrWhiteSpace(socketPath))
    {
        return;
    }

    try
    {
        if (File.Exists(socketPath))
        {
            File.Delete(socketPath);
        }
    }
    catch
    {
    }
}
