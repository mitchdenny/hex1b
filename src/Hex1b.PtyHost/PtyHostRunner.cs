using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Hex1b;
using Microsoft.Extensions.Logging;

namespace Hex1b.PtyHost;

internal sealed record PtyHostOptions(string SocketPath, string SessionToken, string? LogFilePath);

internal sealed class PtyHostRunner(ILogger<PtyHostRunner> logger)
{
    private readonly ILogger<PtyHostRunner> _logger = logger;

    public async Task<int> RunAsync(PtyHostOptions options, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogError("hex1bpty only runs on Windows.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(options.SocketPath))
        {
            _logger.LogError("The PTY host requires a socket path.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(options.SessionToken))
        {
            _logger.LogError("The PTY host requires a non-empty launch token.");
            return 1;
        }

        WindowsPtySocketPaths.EnsureSocketDirectoryExistsForPath(options.SocketPath);
        DeleteSocketFile(options.SocketPath, "startup cleanup");

        await using var ptyHandle = new WindowsPtyHandle();
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        Socket? clientSocket = null;
        NetworkStream? stream = null;

        listener.Bind(new UnixDomainSocketEndPoint(options.SocketPath));
        listener.Listen(1);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            _logger.LogInformation("Listening for Hex1b PTY client on {SocketPath}.", options.SocketPath);

            clientSocket = await listener.AcceptAsync(cts.Token).ConfigureAwait(false);
            stream = new NetworkStream(clientSocket, ownsSocket: false);

            var launchFrame = await WindowsPtyShimProtocol.ReadFrameAsync(stream, cts.Token).ConfigureAwait(false);
            if (launchFrame is null || launchFrame.Value.Type != WindowsPtyShimFrameType.LaunchRequest)
            {
                throw new InvalidOperationException("The first PTY shim frame must be a launch request.");
            }

            var request = WindowsPtyShimProtocol.ReadJson<WindowsPtyShimLaunchRequest>(launchFrame.Value.Payload);
            if (!TokensMatch(options.SessionToken, request.SessionToken))
            {
                _logger.LogWarning("Rejected PTY launch request due to an invalid session token.");
                await TrySendErrorAsync(
                    stream,
                    "The PTY launch request was rejected because the session token was invalid.",
                    CancellationToken.None).ConfigureAwait(false);
                return 1;
            }

            var environment = request.Environment ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            environment["HEX1B_PTY_SHIM_ACTIVE"] = "1";

            _logger.LogInformation(
                "Starting PTY child process {FileName} in {WorkingDirectory}.",
                request.FileName,
                string.IsNullOrWhiteSpace(request.WorkingDirectory) ? Environment.CurrentDirectory : request.WorkingDirectory);

            await ptyHandle.StartAsync(
                request.FileName,
                request.Arguments ?? [],
                request.WorkingDirectory,
                environment,
                request.Width,
                request.Height,
                cts.Token).ConfigureAwait(false);

            await WindowsPtyShimProtocol.WriteJsonAsync(
                stream,
                WindowsPtyShimFrameType.Started,
                new WindowsPtyShimStartedResponse(ptyHandle.ProcessId),
                cts.Token).ConfigureAwait(false);

            var exitTask = ptyHandle.WaitForExitAsync(cts.Token);
            var outputPumpTask = PumpOutputAsync(stream, ptyHandle, exitTask, cts.Token);
            var commandPumpTask = PumpCommandsAsync(stream, ptyHandle, cts);

            var exitCode = await exitTask.ConfigureAwait(false);
            _logger.LogInformation("PTY child exited with code {ExitCode}.", exitCode);

            await TryWriteExitAsync(stream, exitCode).ConfigureAwait(false);

            cts.Cancel();
            await AwaitPumpAsync(outputPumpTask, "output").ConfigureAwait(false);
            await AwaitPumpAsync(commandPumpTask, "command").ConfigureAwait(false);

            return exitCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("hex1bpty shutdown was cancelled by the parent process.");
            return 130;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("hex1bpty shutdown completed after cancellation.");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "hex1bpty failed.");
            if (stream != null)
            {
                await TrySendErrorAsync(stream, ex.Message, CancellationToken.None).ConfigureAwait(false);
            }

            return 1;
        }
        finally
        {
            if (stream != null)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }

            clientSocket?.Dispose();
            DeleteSocketFile(options.SocketPath, "shutdown cleanup");
        }
    }

    private async Task PumpOutputAsync(Stream stream, WindowsPtyHandle ptyHandle, Task<int> exitTask, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var data = await ptyHandle.ReadAsync(ct).ConfigureAwait(false);
            if (!data.IsEmpty)
            {
                await WindowsPtyShimProtocol.WriteFrameAsync(
                    stream,
                    WindowsPtyShimFrameType.Output,
                    data,
                    ct).ConfigureAwait(false);
                continue;
            }

            if (exitTask.IsCompleted)
            {
                return;
            }

            await Task.Delay(10, ct).ConfigureAwait(false);
        }
    }

    private async Task PumpCommandsAsync(Stream stream, WindowsPtyHandle ptyHandle, CancellationTokenSource cts)
    {
        while (!cts.IsCancellationRequested)
        {
            var frame = await WindowsPtyShimProtocol.ReadFrameAsync(stream, cts.Token).ConfigureAwait(false);
            if (frame is null)
            {
                _logger.LogDebug("The PTY client disconnected from the command pump.");
                cts.Cancel();
                return;
            }

            switch (frame.Value.Type)
            {
                case WindowsPtyShimFrameType.Input:
                    await ptyHandle.WriteAsync(frame.Value.Payload, cts.Token).ConfigureAwait(false);
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

                default:
                    _logger.LogWarning("Ignoring unexpected PTY shim frame type {FrameType}.", frame.Value.Type);
                    break;
            }
        }
    }

    private async Task AwaitPumpAsync(Task task, string pumpName)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("{PumpName} pump stopped during cancellation.", pumpName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{PumpName} pump ended with an error during shutdown.", pumpName);
        }
    }

    private async Task TryWriteExitAsync(Stream stream, int exitCode)
    {
        try
        {
            await WindowsPtyShimProtocol.WriteJsonAsync(
                stream,
                WindowsPtyShimFrameType.Exit,
                new WindowsPtyShimExitNotification(exitCode),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to send final exit notification to the PTY client.");
        }
    }

    private async Task TrySendErrorAsync(Stream stream, string message, CancellationToken cancellationToken)
    {
        try
        {
            await WindowsPtyShimProtocol.WriteJsonAsync(
                stream,
                WindowsPtyShimFrameType.Error,
                new WindowsPtyShimErrorResponse(message),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to send PTY error response to the client.");
        }
    }

    private void DeleteSocketFile(string socketPath, string reason)
    {
        try
        {
            WindowsPtySocketPaths.DeleteSocketFile(socketPath);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ignoring socket cleanup failure during {Reason}.", reason);
        }
    }

    private static bool TokensMatch(string expectedToken, string? actualToken)
    {
        if (string.IsNullOrEmpty(actualToken) || expectedToken.Length != actualToken.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expectedToken),
            Encoding.ASCII.GetBytes(actualToken));
    }
}
