using System.Text;
using System.Threading.Channels;

namespace Hex1b;

/// <summary>
/// A workload adapter that connects to a remote muxer server over the Hex1b Muxer Protocol (HMP).
/// </summary>
/// <remarks>
/// <para>
/// This adapter reads terminal output from a remote server and forwards local keyboard
/// input and resize events to it. On connection, the server sends a Hello frame with
/// dimensions and a StateSync frame with the current screen content.
/// </para>
/// <para>
/// The adapter is transport-agnostic: provide any bidirectional <see cref="Stream"/>
/// (Unix domain socket, TCP, named pipe, etc.).
/// </para>
/// </remarks>
public sealed class Hmp1WorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    private readonly Func<CancellationToken, Task<Stream>> _streamFactory;
    private Stream? _stream;
    private readonly Channel<ReadOnlyMemory<byte>> _outputChannel;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;
    private bool _disposed;

    /// <summary>
    /// Creates a muxer workload adapter that connects via the given stream factory.
    /// </summary>
    /// <param name="streamFactory">Factory that creates a bidirectional stream to the server.</param>
    public Hmp1WorkloadAdapter(Func<CancellationToken, Task<Stream>> streamFactory)
    {
        _streamFactory = streamFactory ?? throw new ArgumentNullException(nameof(streamFactory));
        _outputChannel = Channel.CreateBounded<ReadOnlyMemory<byte>>(
            new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true
            });
    }

    /// <summary>
    /// Creates a muxer workload adapter with an already-connected stream.
    /// </summary>
    /// <param name="stream">A bidirectional stream connected to the server.</param>
    public Hmp1WorkloadAdapter(Stream stream)
        : this(_ => Task.FromResult(stream))
    {
        ArgumentNullException.ThrowIfNull(stream);
    }

    /// <summary>
    /// Gets the remote terminal width reported in the Hello frame.
    /// </summary>
    public int RemoteWidth { get; private set; }

    /// <summary>
    /// Gets the remote terminal height reported in the Hello frame.
    /// </summary>
    public int RemoteHeight { get; private set; }

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <summary>
    /// Connects to the server, reads the Hello and StateSync frames,
    /// and starts the background read pump.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct)
    {
        _stream = await _streamFactory(ct).ConfigureAwait(false);

        // Read Hello frame
        var helloFrame = await Hmp1Protocol.ReadFrameAsync(_stream, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Server closed connection before Hello frame.");

        if (helloFrame.Type != Hmp1FrameType.Hello)
            throw new InvalidOperationException($"Expected Hello frame, got {helloFrame.Type}.");

        var hello = Hmp1Protocol.ParseHello(helloFrame.Payload);
        RemoteWidth = hello.Width;
        RemoteHeight = hello.Height;

        // Read StateSync frame
        var syncFrame = await Hmp1Protocol.ReadFrameAsync(_stream, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Server closed connection before StateSync frame.");

        if (syncFrame.Type != Hmp1FrameType.StateSync)
            throw new InvalidOperationException($"Expected StateSync frame, got {syncFrame.Type}.");

        // Queue the initial screen content so the terminal displays it immediately
        if (!syncFrame.Payload.IsEmpty)
        {
            _outputChannel.Writer.TryWrite(syncFrame.Payload);
        }

        // Start the background read pump
        _readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _readTask = Task.Run(() => ReadPumpAsync(_readCts.Token), _readCts.Token);
    }

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
    {
        if (_disposed)
            return new ValueTask<ReadOnlyMemory<byte>>(ReadOnlyMemory<byte>.Empty);

        return ReadOutputCoreAsync(ct);
    }

    private async ValueTask<ReadOnlyMemory<byte>> ReadOutputCoreAsync(CancellationToken ct)
    {
        try
        {
            if (await _outputChannel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                if (_outputChannel.Reader.TryRead(out var data))
                    return data;
            }
        }
        catch (OperationCanceledException) { }
        catch (ChannelClosedException) { }

        return ReadOnlyMemory<byte>.Empty;
    }

    /// <inheritdoc />
    public async ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed || _stream is null) return;

        try
        {
            await Hmp1Protocol.WriteFrameAsync(_stream, Hmp1FrameType.Input, data, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // Stream closed
        }
    }

    /// <inheritdoc />
    public async ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
    {
        if (_disposed || _stream is null) return;

        try
        {
            await Hmp1Protocol.WriteResizeAsync(_stream, width, height, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // Stream closed
        }
    }

    /// <summary>
    /// Background pump that reads frames from the server.
    /// </summary>
    private async Task ReadPumpAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _stream is not null)
            {
                var maybeFrame = await Hmp1Protocol.ReadFrameAsync(_stream, ct).ConfigureAwait(false);
                if (maybeFrame is not { } frame)
                    break; // Server disconnected

                switch (frame.Type)
                {
                    case Hmp1FrameType.Output:
                        _outputChannel.Writer.TryWrite(frame.Payload);
                        break;

                    case Hmp1FrameType.Resize:
                        var (width, height) = Hmp1Protocol.ParseResize(frame.Payload);
                        RemoteWidth = width;
                        RemoteHeight = height;
                        break;

                    case Hmp1FrameType.Exit:
                        // Server terminal has exited
                        return;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // Stream error
        }
        finally
        {
            _outputChannel.Writer.TryComplete();
            Disconnected?.Invoke();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_readCts is not null)
        {
            await _readCts.CancelAsync().ConfigureAwait(false);
            try
            {
                if (_readTask is not null)
                    await _readTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            _readCts.Dispose();
        }

        if (_stream is not null)
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
        }

        _outputChannel.Writer.TryComplete();
    }
}
