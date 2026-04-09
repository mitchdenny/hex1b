using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Channels;

namespace Hex1b;

internal sealed class WindowsProxyPtyHandle : IPtyHandle
{
    private readonly WindowsPtyMode _mode;
    private readonly string? _windowsPtyHostPath;
    private IPtyHandle? _activeHandle;

    internal WindowsProxyPtyHandle(
        WindowsPtyMode mode = WindowsPtyMode.RequireProxy,
        string? windowsPtyHostPath = null)
    {
        _mode = mode;
        _windowsPtyHostPath = windowsPtyHostPath;
    }

    public int ProcessId => _activeHandle?.ProcessId ?? -1;

    public async Task StartAsync(
        string fileName,
        string[] arguments,
        string? workingDirectory,
        Dictionary<string, string> environment,
        int width,
        int height,
        CancellationToken ct)
    {
        if (_activeHandle != null)
        {
            throw new InvalidOperationException("The Windows PTY handle has already been started.");
        }

        // Windows PTY backend selection is now explicit:
        // - RequireProxy => use hex1bpty.exe and fail if it cannot be used
        // - Direct => bypass the helper entirely
        if (_mode == WindowsPtyMode.RequireProxy)
        {
            var shimHandle = new WindowsShimPtyHandle(_windowsPtyHostPath);
            try
            {
                await shimHandle.StartAsync(fileName, arguments, workingDirectory, environment, width, height, ct).ConfigureAwait(false);
                _activeHandle = shimHandle;
                return;
            }
            catch (OperationCanceledException)
            {
                await shimHandle.DisposeAsync().ConfigureAwait(false);
                throw;
            }
            catch (Exception ex)
            {
                await shimHandle.DisposeAsync().ConfigureAwait(false);
                throw new InvalidOperationException(
                    "The Windows PTY proxy mode was required for this run, but hex1bpty.exe could not be started.",
                    ex);
            }
        }

        var directHandle = new WindowsPtyHandle();
        await directHandle.StartAsync(fileName, arguments, workingDirectory, environment, width, height, ct).ConfigureAwait(false);
        _activeHandle = directHandle;
    }

    public ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct)
    {
        if (_activeHandle == null)
        {
            throw new InvalidOperationException("The Windows PTY handle has not been started.");
        }

        return _activeHandle.ReadAsync(ct);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        if (_activeHandle == null)
        {
            throw new InvalidOperationException("The Windows PTY handle has not been started.");
        }

        return _activeHandle.WriteAsync(data, ct);
    }

    public void Resize(int width, int height)
    {
        _activeHandle?.Resize(width, height);
    }

    public void Kill(int signal)
    {
        _activeHandle?.Kill(signal);
    }

    public Task<int> WaitForExitAsync(CancellationToken ct)
    {
        if (_activeHandle == null)
        {
            throw new InvalidOperationException("The Windows PTY handle has not been started.");
        }

        return _activeHandle.WaitForExitAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_activeHandle != null)
        {
            await _activeHandle.DisposeAsync().ConfigureAwait(false);
            _activeHandle = null;
        }
    }
}

internal sealed class WindowsShimPtyHandle : IPtyHandle
{
    private readonly string? _windowsPtyHostPath;
    private readonly Channel<byte[]> _outputChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(64)
    {
        SingleReader = true,
        SingleWriter = true,
        FullMode = BoundedChannelFullMode.Wait
    });

    private readonly Channel<PendingFrame> _sendChannel = Channel.CreateUnbounded<PendingFrame>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    private readonly TaskCompletionSource<int> _exitCodeTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<(string LoopName, Exception Error)> _fatalErrorTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private CancellationTokenSource? _cts;
    private Process? _helperProcess;
    private NetworkStream? _stream;
    private Socket? _socket;
    private Task? _sendLoopTask;
    private Task? _receiveLoopTask;
    private string? _socketPath;
    private int _processId;
    private bool _disposed;

    internal WindowsShimPtyHandle(string? windowsPtyHostPath = null)
    {
        _windowsPtyHostPath = windowsPtyHostPath;
    }

    public int ProcessId => _processId;

    public async Task StartAsync(
        string fileName,
        string[] arguments,
        string? workingDirectory,
        Dictionary<string, string> environment,
        int width,
        int height,
        CancellationToken ct)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WindowsShimPtyHandle));
        }

        if (!WindowsPtyShimLocator.TryResolve(_windowsPtyHostPath, out var shimPath))
        {
            throw new FileNotFoundException(
                "The Windows PTY shim binary (hex1bpty.exe) could not be found in the application output.",
                "hex1bpty.exe");
        }

        var sessionToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _socketPath = WindowsPtySocketPaths.CreateSocketPath();
        _helperProcess = StartHelperProcess(shimPath, _socketPath, sessionToken);

        _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await ConnectWithRetriesAsync(_socket, new UnixDomainSocketEndPoint(_socketPath), _helperProcess, ct).ConfigureAwait(false);

        _stream = new NetworkStream(_socket, ownsSocket: true);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var resolvedWorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? Environment.CurrentDirectory
            : workingDirectory;

        var launchRequest = new WindowsPtyShimLaunchRequest(
            fileName,
            arguments,
            resolvedWorkingDirectory,
            environment,
            width,
            height,
            sessionToken);

        await WindowsPtyShimProtocol.WriteJsonAsync(
            _stream,
            WindowsPtyShimFrameType.LaunchRequest,
            launchRequest,
            ct).ConfigureAwait(false);

        var responseFrame = await WindowsPtyShimProtocol.ReadFrameAsync(_stream, ct).ConfigureAwait(false)
            ?? throw new IOException("The Windows PTY shim disconnected before acknowledging the launch request.");

        switch (responseFrame.Type)
        {
            case WindowsPtyShimFrameType.Started:
                _processId = WindowsPtyShimProtocol.ReadJson<WindowsPtyShimStartedResponse>(responseFrame.Payload).ProcessId;
                break;

            case WindowsPtyShimFrameType.Error:
                var error = WindowsPtyShimProtocol.ReadJson<WindowsPtyShimErrorResponse>(responseFrame.Payload).Message;
                throw new InvalidOperationException($"The Windows PTY shim failed to start the child process: {error}");

            default:
                throw new InvalidOperationException($"Unexpected PTY shim response: {responseFrame.Type}.");
        }

        await DrainInitialFramesAsync(ct).ConfigureAwait(false);

        _sendLoopTask = Task.Run(() => RunSendLoopAsync(_cts.Token), _cts.Token);
        _receiveLoopTask = Task.Run(() => RunReceiveLoopAsync(_cts.Token), _cts.Token);
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct)
    {
        if (_disposed)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        ThrowIfFatalError("read output");

        try
        {
            if (await _outputChannel.Reader.WaitToReadAsync(ct).ConfigureAwait(false) &&
                _outputChannel.Reader.TryRead(out var data))
            {
                return data;
            }
        }
        catch (OperationCanceledException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
        catch (ChannelClosedException)
        {
            ThrowIfFatalError("read output");
            return ReadOnlyMemory<byte>.Empty;
        }

        ThrowIfFatalError("read output");
        return ReadOnlyMemory<byte>.Empty;
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        if (_disposed || data.IsEmpty)
        {
            return;
        }

        ThrowIfFatalError("send input");

        try
        {
            await _sendChannel.Writer.WriteAsync(new PendingFrame(WindowsPtyShimFrameType.Input, data.ToArray()), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ChannelClosedException)
        {
            ThrowIfFatalError("send input");
        }
    }

    public void Resize(int width, int height)
    {
        if (_disposed)
        {
            return;
        }

        var payload = WindowsPtyShimProtocol.SerializeJson(new WindowsPtyShimResizeRequest(width, height));
        _sendChannel.Writer.TryWrite(new PendingFrame(WindowsPtyShimFrameType.Resize, payload));
    }

    public void Kill(int signal)
    {
        if (_disposed)
        {
            return;
        }

        _sendChannel.Writer.TryWrite(new PendingFrame(WindowsPtyShimFrameType.Kill, Array.Empty<byte>()));
    }

    public Task<int> WaitForExitAsync(CancellationToken ct)
    {
        return WaitForExitCoreAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_stream != null)
        {
            try
            {
                await WindowsPtyShimProtocol.WriteFrameAsync(_stream, WindowsPtyShimFrameType.Shutdown, Array.Empty<byte>(), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                TraceShimMessage($"Failed to send shutdown frame during dispose: {ex.Message}");
            }
        }

        _sendChannel.Writer.TryComplete();
        _cts?.Cancel();

        // Teardown strategy:
        // 1. ask the helper to shut down cleanly,
        // 2. close the socket so blocked loops observe EOF/cancellation immediately,
        // 3. kill the helper process tree as a final backstop if it still survives.
        _stream?.Dispose();
        _stream = null;
        _socket?.Dispose();
        _socket = null;

        try
        {
            if (_sendLoopTask != null)
            {
                await _sendLoopTask.ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            TraceShimMessage($"Send loop teardown observed: {ex.Message}");
        }

        try
        {
            if (_receiveLoopTask != null)
            {
                await _receiveLoopTask.ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            TraceShimMessage($"Receive loop teardown observed: {ex.Message}");
        }
        _cts?.Dispose();
        _cts = null;

        if (_helperProcess is { HasExited: false })
        {
            try
            {
                _helperProcess.Kill(entireProcessTree: true);
                await _helperProcess.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                TraceShimMessage($"Failed to terminate helper process cleanly: {ex.Message}");
            }
        }

        _helperProcess?.Dispose();
        _helperProcess = null;

        _outputChannel.Writer.TryComplete();
        _exitCodeTcs.TrySetResult(-1);

        WindowsPtySocketPaths.DeleteSocketFile(_socketPath);
    }

    private async Task RunSendLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var frame in _sendChannel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                if (_stream == null)
                {
                    break;
                }

                TraceShimFrame("send", frame.Type, frame.Payload);
                await WindowsPtyShimProtocol.WriteFrameAsync(_stream, frame.Type, frame.Payload, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException ex) when (!ct.IsCancellationRequested && !_disposed)
        {
            RecordFatalError("send loop", ex);
        }
        catch (ObjectDisposedException ex) when (!ct.IsCancellationRequested && !_disposed)
        {
            RecordFatalError("send loop", ex);
        }
    }

    private async Task RunReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _stream != null)
            {
                var frame = await WindowsPtyShimProtocol.ReadFrameAsync(_stream, ct).ConfigureAwait(false);
                if (frame is null)
                {
                    break;
                }

                if (await HandleReceivedFrameAsync(frame.Value, ct).ConfigureAwait(false))
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            TraceShimMessage("receive-loop OperationCanceledException");
        }
        catch (IOException ex) when (!ct.IsCancellationRequested && !_disposed)
        {
            RecordFatalError("receive loop", ex);
        }
        catch (ObjectDisposedException ex) when (!ct.IsCancellationRequested && !_disposed)
        {
            RecordFatalError("receive loop", ex);
        }
        finally
        {
            _outputChannel.Writer.TryComplete();

            if (!_exitCodeTcs.Task.IsCompleted)
            {
                _exitCodeTcs.TrySetResult(await GetFallbackExitCodeAsync().ConfigureAwait(false));
            }
        }
    }

    private async Task DrainInitialFramesAsync(CancellationToken ct)
    {
        if (_socket == null || _stream == null)
        {
            return;
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(500);
        var quietUntil = DateTime.UtcNow + TimeSpan.FromMilliseconds(75);

        while (DateTime.UtcNow < deadline)
        {
            if (!_socket.Poll(10_000, SelectMode.SelectRead))
            {
                if (DateTime.UtcNow >= quietUntil)
                {
                    break;
                }

                continue;
            }

            var frame = WindowsPtyShimProtocol.ReadFrame(_stream);
            if (frame is null)
            {
                break;
            }

            quietUntil = DateTime.UtcNow + TimeSpan.FromMilliseconds(75);

            if (await HandleReceivedFrameAsync(frame.Value, ct).ConfigureAwait(false))
            {
                break;
            }
        }
    }

    private async Task<bool> HandleReceivedFrameAsync(
        (WindowsPtyShimFrameType Type, byte[] Payload) frame,
        CancellationToken ct)
    {
        TraceShimFrame("recv", frame.Type, frame.Payload);

        switch (frame.Type)
        {
            case WindowsPtyShimFrameType.Output:
                await _outputChannel.Writer.WriteAsync(frame.Payload, ct).ConfigureAwait(false);
                return false;

            case WindowsPtyShimFrameType.Exit:
                var exitNotification = WindowsPtyShimProtocol.ReadJson<WindowsPtyShimExitNotification>(frame.Payload);
                _exitCodeTcs.TrySetResult(exitNotification.ExitCode);
                return true;

            case WindowsPtyShimFrameType.Error:
                var error = WindowsPtyShimProtocol.ReadJson<WindowsPtyShimErrorResponse>(frame.Payload);
                RecordFatalError("shim transport", new InvalidOperationException(error.Message));
                return true;

            default:
                return false;
        }
    }

    private static Process StartHelperProcess(string shimPath, string socketPath, string sessionToken)
    {
        var startInfo = new ProcessStartInfo(shimPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.ArgumentList.Add("--socket");
        startInfo.ArgumentList.Add(socketPath);
        startInfo.ArgumentList.Add("--token");
        startInfo.ArgumentList.Add(sessionToken);

        var logFilePath = Environment.GetEnvironmentVariable("HEX1B_PTY_SHIM_LOGFILE");
        if (!string.IsNullOrWhiteSpace(logFilePath))
        {
            startInfo.ArgumentList.Add("--logfile");
            startInfo.ArgumentList.Add(logFilePath);
        }

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to launch the Windows PTY shim at '{shimPath}'.");
    }

    private static async Task ConnectWithRetriesAsync(Socket socket, EndPoint endpoint, Process helperProcess, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (helperProcess.HasExited)
            {
                throw new InvalidOperationException($"The Windows PTY shim exited before accepting a connection (exit code {helperProcess.ExitCode}).");
            }

            try
            {
                await socket.ConnectAsync(endpoint, ct).ConfigureAwait(false);
                return;
            }
            catch (SocketException ex)
            {
                lastError = ex;
                await Task.Delay(50, ct).ConfigureAwait(false);
            }
        }

        throw new IOException("Timed out connecting to the Windows PTY shim.", lastError);
    }

    private async Task<int> GetFallbackExitCodeAsync()
    {
        if (_helperProcess == null)
        {
            return -1;
        }

        try
        {
            if (_helperProcess.HasExited)
            {
                return _helperProcess.ExitCode;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await _helperProcess.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            return _helperProcess.HasExited ? _helperProcess.ExitCode : -1;
        }
        catch (OperationCanceledException)
        {
            return -1;
        }
        catch (Exception ex)
        {
            TraceShimMessage($"Failed to query helper exit code: {ex.Message}");
            return -1;
        }
    }

    private async Task<int> WaitForExitCoreAsync(CancellationToken ct)
    {
        ThrowIfFatalError("wait for process exit");

        var completed = await Task.WhenAny(_exitCodeTcs.Task, _fatalErrorTcs.Task).WaitAsync(ct).ConfigureAwait(false);
        if (completed == _fatalErrorTcs.Task)
        {
            ThrowIfFatalError("wait for process exit");
        }

        return await _exitCodeTcs.Task.WaitAsync(ct).ConfigureAwait(false);
    }

    private void RecordFatalError(string loopName, Exception error)
    {
        TraceShimMessage($"{loopName} faulted: {error}");
        _fatalErrorTcs.TrySetResult((loopName, error));
        _sendChannel.Writer.TryComplete(error);
        _outputChannel.Writer.TryComplete(error);

        try
        {
            _stream?.Dispose();
        }
        catch
        {
        }
    }

    private void ThrowIfFatalError(string action)
    {
        if (!_fatalErrorTcs.Task.IsCompletedSuccessfully)
        {
            return;
        }

        var (loopName, error) = _fatalErrorTcs.Task.Result;
        throw new InvalidOperationException(
            $"The Windows PTY shim {loopName} failed while attempting to {action}.",
            error);
    }

    private static void TraceShimFrame(string direction, WindowsPtyShimFrameType type, ReadOnlySpan<byte> payload)
    {
        var tracePath = Environment.GetEnvironmentVariable("HEX1B_PTY_SHIM_CLIENT_TRACE_FILE");
        if (string.IsNullOrWhiteSpace(tracePath))
        {
            return;
        }

        try
        {
            var text = System.Text.Encoding.UTF8.GetString(payload)
                .Replace("\u001b", "<ESC>")
                .Replace("\r", "<CR>")
                .Replace("\n", "<LF>");
            File.AppendAllText(tracePath, $"{DateTime.UtcNow:O} {direction} {type}: {text}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private static void TraceShimMessage(string message)
    {
        var tracePath = Environment.GetEnvironmentVariable("HEX1B_PTY_SHIM_CLIENT_TRACE_FILE");
        if (string.IsNullOrWhiteSpace(tracePath))
        {
            return;
        }

        try
        {
            File.AppendAllText(tracePath, $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private sealed record PendingFrame(WindowsPtyShimFrameType Type, byte[] Payload);
}
