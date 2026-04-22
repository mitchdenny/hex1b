using System.Text;
using System.Threading.Channels;
using Hex1b.Automation;

namespace Hex1b;

/// <summary>
/// A presentation adapter that serves terminal output to multiple remote clients
/// over the Hex1b Muxer Protocol (HMP).
/// </summary>
/// <remarks>
/// <para>
/// This adapter acts as a headless presentation layer. Instead of displaying output
/// on a local console, it multicasts ANSI output to all connected clients via their
/// streams. Use <see cref="AddClient"/> to add new client connections.
/// </para>
/// <para>
/// Each client receives a <see cref="Hmp1FrameType.Hello"/> frame with the protocol
/// version and current dimensions, followed by a <see cref="Hmp1FrameType.StateSync"/>
/// frame with a full screen snapshot so the client immediately sees the current state.
/// </para>
/// </remarks>
public sealed class Hmp1PresentationAdapter : ITerminalLifecycleAwarePresentationAdapter
{
    private readonly List<Hmp1ClientSession> _sessions = [];
    private readonly object _sessionsLock = new();
    private readonly Channel<ReadOnlyMemory<byte>> _inputChannel;
    private Hex1bTerminal? _terminal;
    private int _width;
    private int _height;
    private bool _disposed;

    /// <summary>
    /// Creates a new muxer presentation adapter with the specified initial dimensions.
    /// </summary>
    /// <param name="width">Initial terminal width in columns.</param>
    /// <param name="height">Initial terminal height in rows.</param>
    public Hmp1PresentationAdapter(int width = 80, int height = 24)
    {
        _width = width;
        _height = height;
        _inputChannel = Channel.CreateBounded<ReadOnlyMemory<byte>>(
            new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
    }

    /// <inheritdoc />
    public int Width => _width;

    /// <inheritdoc />
    public int Height => _height;

    /// <inheritdoc />
    public TerminalCapabilities Capabilities => new()
    {
        SupportsTrueColor = true,
        Supports256Colors = true,
        SupportsAlternateScreen = true,
        SupportsBracketedPaste = true
    };

    /// <inheritdoc />
    public event Action<int, int>? Resized;

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <summary>
    /// Gets the number of currently connected clients.
    /// </summary>
    public int ClientCount
    {
        get
        {
            lock (_sessionsLock)
            {
                return _sessions.Count;
            }
        }
    }

    /// <inheritdoc />
    public void TerminalCreated(Hex1bTerminal terminal)
    {
        _terminal = terminal;
    }

    /// <inheritdoc />
    public void TerminalStarted()
    {
    }

    /// <inheritdoc />
    public void TerminalCompleted(int exitCode)
    {
        // Notify all clients that the terminal has exited
        Hmp1ClientSession[] snapshot;
        lock (_sessionsLock)
        {
            snapshot = [.. _sessions];
        }

        foreach (var session in snapshot)
        {
            _ = TrySendExitAsync(session, exitCode);
        }

        // Complete the input channel so the terminal's input pump exits,
        // allowing RunAsync to return when the workload has disconnected.
        _inputChannel.Writer.TryComplete();
    }

    /// <summary>
    /// Adds a new client connection. The client will receive a Hello frame with the
    /// current dimensions and a StateSync frame with the current screen content.
    /// </summary>
    /// <param name="stream">A bidirectional stream connected to the client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A handle that can be disposed to disconnect the client.</returns>
    public async Task<Hmp1ClientHandle> AddClient(Stream stream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Send Hello frame
        await Hmp1Protocol.WriteHelloAsync(stream, _width, _height, ct).ConfigureAwait(false);

        // Atomically: capture snapshot + register client, so no output is lost
        // between snapshot creation and client registration.
        var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var session = new Hmp1ClientSession(stream, sessionCts);

        byte[] syncBytes;
        lock (_sessionsLock)
        {
            if (_terminal != null)
            {
                using var snapshot = _terminal.CreateSnapshot();
                var ansi = snapshot.ToAnsi(new TerminalAnsiOptions
                {
                    IncludeClearScreen = true,
                    IncludeTrailingNewline = true
                });
                syncBytes = Encoding.UTF8.GetBytes(ansi);
            }
            else
            {
                syncBytes = [];
            }

            _sessions.Add(session);
        }

        // Send StateSync frame (outside lock, but client is already registered
        // so any concurrent output will also be queued)
        await Hmp1Protocol.WriteFrameAsync(stream, Hmp1FrameType.StateSync, syncBytes, ct).ConfigureAwait(false);

        // Start per-client write pump and read pump
        session.WriteTask = Task.Run(() => WriteClientPumpAsync(session), sessionCts.Token);
        session.ReadTask = Task.Run(() => ReadClientPumpAsync(session), sessionCts.Token);

        return new Hmp1ClientHandle(session, this);
    }

    /// <inheritdoc />
    public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed || data.IsEmpty) return ValueTask.CompletedTask;

        // Enqueue to each client's write channel (non-blocking).
        // Clients that can't keep up will be disconnected.
        lock (_sessionsLock)
        {
            for (var i = _sessions.Count - 1; i >= 0; i--)
            {
                var session = _sessions[i];
                if (!session.OutputChannel.Writer.TryWrite(data.ToArray()))
                {
                    // Client can't keep up — disconnect it
                    _sessions.RemoveAt(i);
                    _ = DisposeSessionAsync(session);
                }
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
    {
        if (_disposed)
            return ReadOnlyMemory<byte>.Empty;

        try
        {
            if (await _inputChannel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                if (_inputChannel.Reader.TryRead(out var data))
                    return data;
            }
        }
        catch (OperationCanceledException) { }
        catch (ChannelClosedException) { }

        return ReadOnlyMemory<byte>.Empty;
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask EnterRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask ExitRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public (int Row, int Column) GetCursorPosition() => (0, 0);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        Hmp1ClientSession[] snapshot;
        lock (_sessionsLock)
        {
            snapshot = [.. _sessions];
            _sessions.Clear();
        }

        foreach (var session in snapshot)
        {
            await DisposeSessionAsync(session).ConfigureAwait(false);
        }

        _inputChannel.Writer.TryComplete();
        Disconnected?.Invoke();
    }

    /// <summary>
    /// Background pump that writes queued output frames to a client's stream.
    /// Each client has its own write pump to prevent slow clients from blocking others.
    /// </summary>
    private async Task WriteClientPumpAsync(Hmp1ClientSession session)
    {
        try
        {
            await foreach (var data in session.OutputChannel.Reader.ReadAllAsync(session.Cts.Token)
                .ConfigureAwait(false))
            {
                await Hmp1Protocol.WriteFrameAsync(
                    session.Stream, Hmp1FrameType.Output, data, session.Cts.Token).ConfigureAwait(false);
                // Flush periodically (after draining available items)
                if (session.OutputChannel.Reader.Count == 0)
                {
                    await session.Stream.FlushAsync(session.Cts.Token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (
            ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // Client disconnected
        }
        finally
        {
            RemoveSession(session);
        }
    }

    /// <summary>
    /// Background pump that reads frames from a client and routes input/resize.
    /// </summary>
    private async Task ReadClientPumpAsync(Hmp1ClientSession session)
    {
        try
        {
            while (!session.Cts.IsCancellationRequested)
            {
                var maybeFrame = await Hmp1Protocol.ReadFrameAsync(session.Stream, session.Cts.Token)
                    .ConfigureAwait(false);

                if (maybeFrame is not { } frame)
                    break; // Client disconnected

                switch (frame.Type)
                {
                    case Hmp1FrameType.Input:
                        _inputChannel.Writer.TryWrite(frame.Payload);
                        break;

                    case Hmp1FrameType.Resize:
                        var (width, height) = Hmp1Protocol.ParseResize(frame.Payload);
                        session.RemoteWidth = width;
                        session.RemoteHeight = height;
                        _width = width;
                        _height = height;
                        Resized?.Invoke(width, height);
                        break;
                }
            }
        }
        catch (Exception ex) when (
            ex is IOException or ObjectDisposedException or OperationCanceledException or InvalidOperationException)
        {
            // Client disconnected or stream error
        }
        finally
        {
            RemoveSession(session);
        }
    }

    internal void RemoveSession(Hmp1ClientSession session)
    {
        lock (_sessionsLock)
        {
            _sessions.Remove(session);
        }

        _ = DisposeSessionAsync(session);
    }

    private static async Task DisposeSessionAsync(Hmp1ClientSession session)
    {
        session.OutputChannel.Writer.TryComplete();

        try
        {
            await session.Cts.CancelAsync().ConfigureAwait(false);
        }
        catch { }

        session.Cts.Dispose();

        try
        {
            await session.Stream.DisposeAsync().ConfigureAwait(false);
        }
        catch { }
    }

    private static async Task TrySendExitAsync(Hmp1ClientSession session, int exitCode)
    {
        try
        {
            // Enqueue a sentinel, then write exit directly
            session.OutputChannel.Writer.TryComplete();
            await Hmp1Protocol.WriteExitAsync(session.Stream, exitCode).ConfigureAwait(false);
        }
        catch { }
    }

    /// <summary>
    /// Internal session tracking for a connected client.
    /// </summary>
    internal sealed class Hmp1ClientSession(Stream stream, CancellationTokenSource cts)
    {
        public Stream Stream { get; } = stream;
        public CancellationTokenSource Cts { get; } = cts;
        public Task? ReadTask { get; set; }
        public Task? WriteTask { get; set; }
        public int RemoteWidth { get; set; }
        public int RemoteHeight { get; set; }

        /// <summary>
        /// Per-client outbound queue. When full, the client is disconnected rather than
        /// blocking other clients or dropping frames (which would desync incremental ANSI state).
        /// </summary>
        public Channel<ReadOnlyMemory<byte>> OutputChannel { get; } =
            Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
    }
}
