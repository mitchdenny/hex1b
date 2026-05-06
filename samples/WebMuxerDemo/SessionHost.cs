using Hex1b;

namespace WebMuxerDemo;

/// <summary>
/// One session = one common <see cref="Hex1bTerminal"/> hosting a real PTY
/// and serving HMP1 over a Unix domain socket. Per-WebSocket proxies dial in
/// to that UDS and forward bytes back to the browser.
/// </summary>
internal sealed class SessionHost : IAsyncDisposable
{
    private readonly Hex1bTerminal _terminal;
    private readonly CancellationTokenSource _cts;
    private readonly Task _runTask;

    private SessionHost(string name, string socketPath, Hex1bTerminal terminal, CancellationTokenSource cts, Task runTask)
    {
        Name = name;
        SocketPath = socketPath;
        _terminal = terminal;
        _cts = cts;
        _runTask = runTask;
    }

    public string Name { get; }

    public string SocketPath { get; }

    public static SessionHost Start(string name, string socketPath, string shell, IReadOnlyList<string>? args = null)
    {
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithPtyProcess(options =>
            {
                options.FileName = shell;
                if (args is not null)
                {
                    options.Arguments = new List<string>(args);
                }
                if (OperatingSystem.IsWindows())
                {
                    options.WindowsPtyMode = WindowsPtyMode.RequireProxy;
                }
            })
            .WithDimensions(80, 24)
            .WithHmp1UdsServer(socketPath)
            .Build();

        var cts = new CancellationTokenSource();
        var runTask = Task.Run(async () =>
        {
            try
            {
                await terminal.RunAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            finally
            {
                try { File.Delete(socketPath); } catch { /* best effort */ }
            }
        }, CancellationToken.None);

        return new SessionHost(name, socketPath, terminal, cts, runTask);
    }

    public async ValueTask DisposeAsync()
    {
        try { await _cts.CancelAsync().ConfigureAwait(false); } catch { }
        try { await _runTask.ConfigureAwait(false); } catch { }
        try { await _terminal.DisposeAsync().ConfigureAwait(false); } catch { }
        _cts.Dispose();
    }
}
