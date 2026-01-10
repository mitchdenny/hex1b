using System.Diagnostics;
using System.Threading.Channels;

namespace Hex1b.Terminal;

/// <summary>
/// A workload adapter that wraps a standard .NET <see cref="Process"/> with redirected I/O.
/// </summary>
/// <remarks>
/// <para>
/// This adapter uses standard process redirection (stdin/stdout/stderr) rather than 
/// pseudo-terminal (PTY) emulation. This means:
/// </para>
/// <list type="bullet">
///   <item>No native library dependencies - works on all .NET platforms</item>
///   <item>Programs won't detect a TTY (isatty() returns false)</item>
///   <item>No ANSI escape sequence processing by the child process's libc</item>
///   <item>Programs like vim, tmux, and screen won't work correctly</item>
/// </list>
/// <para>
/// Use this for:
/// </para>
/// <list type="bullet">
///   <item>Simple command-line programs that output text</item>
///   <item>Build tools, compilers, and other non-interactive programs</item>
///   <item>Cross-platform scenarios where PTY isn't available</item>
/// </list>
/// <para>
/// For full terminal emulation with PTY support, use <see cref="Hex1bTerminalChildProcess"/> instead.
/// </para>
/// </remarks>
public sealed class StandardProcessWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    private readonly ProcessStartInfo _startInfo;
    private readonly Channel<ReadOnlyMemory<byte>> _outputChannel;
    private Process? _process;
    private bool _disposed;
    private bool _started;
    private readonly TaskCompletionSource _startedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Creates a new standard process workload adapter.
    /// </summary>
    /// <param name="startInfo">
    /// The process start info. Note that <see cref="ProcessStartInfo.RedirectStandardInput"/>,
    /// <see cref="ProcessStartInfo.RedirectStandardOutput"/>, and <see cref="ProcessStartInfo.RedirectStandardError"/>
    /// will be set to true automatically.
    /// </param>
    public StandardProcessWorkloadAdapter(ProcessStartInfo startInfo)
    {
        _startInfo = startInfo ?? throw new ArgumentNullException(nameof(startInfo));
        _outputChannel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false // stdout and stderr both write
        });
    }

    /// <summary>
    /// Gets whether the process has been started.
    /// </summary>
    public bool HasStarted => _started;

    /// <summary>
    /// Gets whether the process has exited.
    /// </summary>
    public bool HasExited => _process?.HasExited ?? false;

    /// <summary>
    /// Gets the process ID, or -1 if not started.
    /// </summary>
    public int ProcessId => _process?.Id ?? -1;

    /// <summary>
    /// Gets the exit code, or -1 if not exited.
    /// </summary>
    public int ExitCode => _process?.HasExited == true ? _process.ExitCode : -1;

    /// <summary>
    /// Starts the process.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public Task StartAsync(CancellationToken ct = default)
    {
        if (_started)
            throw new InvalidOperationException("Process has already been started.");

        if (_disposed)
            throw new ObjectDisposedException(nameof(StandardProcessWorkloadAdapter));

        // Configure for redirected I/O
        _startInfo.RedirectStandardInput = true;
        _startInfo.RedirectStandardOutput = true;
        _startInfo.RedirectStandardError = true;
        _startInfo.UseShellExecute = false;
        _startInfo.CreateNoWindow = true;

        _process = new Process { StartInfo = _startInfo };

        // Wire up output/error handlers
        _process.OutputDataReceived += OnOutputDataReceived;
        _process.ErrorDataReceived += OnErrorDataReceived;
        _process.Exited += OnProcessExited;
        _process.EnableRaisingEvents = true;

        _started = true;

        if (!_process.Start())
        {
            throw new InvalidOperationException($"Failed to start process: {_startInfo.FileName}");
        }

        // Begin async reads
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        _startedTcs.TrySetResult();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Waits for the process to exit.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The exit code of the process.</returns>
    public async Task<int> WaitForExitAsync(CancellationToken ct = default)
    {
        if (!_started)
            throw new InvalidOperationException("Process has not been started.");

        if (_process == null)
            return -1;

        await _process.WaitForExitAsync(ct);
        return _process.ExitCode;
    }

    /// <summary>
    /// Kills the process.
    /// </summary>
    public void Kill()
    {
        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Ignore errors during kill
            }
        }
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(e.Data + "\n");
            _outputChannel.Writer.TryWrite(bytes);
        }
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(e.Data + "\n");
            _outputChannel.Writer.TryWrite(bytes);
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        // Delay slightly to allow any pending output events to be processed
        // Output events can race with the Exited event
        Task.Delay(50).ContinueWith(_ =>
        {
            _outputChannel.Writer.TryComplete();
            Disconnected?.Invoke();
        });
    }

    // === IHex1bTerminalWorkloadAdapter Implementation ===

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
    {
        // Wait for process to start
        await _startedTcs.Task.WaitAsync(ct);

        if (_disposed)
            return ReadOnlyMemory<byte>.Empty;

        try
        {
            if (await _outputChannel.Reader.WaitToReadAsync(ct))
            {
                if (_outputChannel.Reader.TryRead(out var data))
                {
                    return data;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
        catch (ChannelClosedException)
        {
            // Channel closed - process exited
        }

        return ReadOnlyMemory<byte>.Empty;
    }

    /// <inheritdoc />
    public async ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (!_started || _disposed || _process == null || _process.HasExited)
            return;

        try
        {
            var text = System.Text.Encoding.UTF8.GetString(data.Span);
            await _process.StandardInput.WriteAsync(text.AsMemory(), ct);
            await _process.StandardInput.FlushAsync(ct);
        }
        catch
        {
            // Ignore write errors
        }
    }

    /// <inheritdoc />
    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
    {
        // Standard process redirection doesn't support resize signals
        // This is a no-op - only PTY-based processes can receive SIGWINCH
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;
        _outputChannel.Writer.TryComplete();

        if (_process != null)
        {
            _process.OutputDataReceived -= OnOutputDataReceived;
            _process.ErrorDataReceived -= OnErrorDataReceived;
            _process.Exited -= OnProcessExited;

            if (!_process.HasExited)
            {
                try
                {
                    _process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore
                }
            }

            _process.Dispose();
            _process = null;
        }

        return ValueTask.CompletedTask;
    }
}
