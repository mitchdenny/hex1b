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
    private readonly string? _windowsPtyProxySocketPath;
    private IPtyHandle? _activeHandle;

    internal WindowsProxyPtyHandle(
        WindowsPtyMode mode = WindowsPtyMode.RequireProxy,
        string? windowsPtyHostPath = null,
        string? windowsPtyProxySocketPath = null)
    {
        _mode = mode;
        _windowsPtyHostPath = windowsPtyHostPath;
        _windowsPtyProxySocketPath = windowsPtyProxySocketPath;
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

        if (_windowsPtyProxySocketPath is not null && _mode != WindowsPtyMode.RequireProxy)
        {
            throw new InvalidOperationException(
                $"{nameof(Hex1bTerminalProcessOptions.WindowsPtyProxySocketPath)} requires " +
                $"{nameof(Hex1bTerminalProcessOptions.WindowsPtyMode)}.{nameof(WindowsPtyMode.RequireProxy)}.");
        }

        // Windows PTY backend selection is now explicit:
        // - RequireProxy => use hex1bpty.exe and fail if it cannot be used
        // - Direct => bypass the helper entirely
        if (_mode == WindowsPtyMode.RequireProxy)
        {
            var shimHandle = new WindowsShimPtyHandle(_windowsPtyHostPath, _windowsPtyProxySocketPath);
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
