using System.Text;
using System.Threading.Channels;

namespace Hex1b;

/// <summary>
/// A workload adapter that plays back an asciinema recording with playback control support.
/// </summary>
/// <remarks>
/// This adapter is used internally by <see cref="AsciinemaRecording"/> to provide
/// the <see cref="IHex1bTerminalWorkloadAdapter"/> interface while exposing playback
/// control through the <see cref="AsciinemaRecording"/> class.
/// </remarks>
internal sealed class AsciinemaRecordingWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    private readonly AsciinemaRecording _recording;
    private readonly Channel<ReadOnlyMemory<byte>> _outputChannel;
    private readonly CancellationTokenSource _cts = new();
    private Task? _playbackTask;
    private bool _disposed;
    private bool _started;
    
    // ANSI escape sequence to clear screen and reset cursor
    private static readonly byte[] ClearScreenSequence = Encoding.UTF8.GetBytes("\x1b[2J\x1b[H");
    
    public AsciinemaRecordingWorkloadAdapter(AsciinemaRecording recording)
    {
        _recording = recording;
        _outputChannel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
        
        // Wire up the recording to send output through our channel
        _recording.SetCallbacks(
            output => _outputChannel.Writer.TryWrite(output),
            () => _outputChannel.Writer.TryWrite(ClearScreenSequence)
        );
    }
    
    /// <summary>
    /// Gets the width from the recording header.
    /// </summary>
    public int Width => _recording.Width;
    
    /// <summary>
    /// Gets the height from the recording header.
    /// </summary>
    public int Height => _recording.Height;
    
    /// <summary>
    /// Starts playback of the recording.
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_started)
            throw new InvalidOperationException("Playback has already been started.");
        
        if (_disposed)
            throw new ObjectDisposedException(nameof(AsciinemaRecordingWorkloadAdapter));
        
        _started = true;
        
        // Load events first
        await _recording.LoadEventsAsync(ct);
        
        // Start the playback loop in the background
        _playbackTask = Task.Run(async () =>
        {
            try
            {
                await _recording.PlaybackLoopAsync(_cts.Token);
            }
            finally
            {
                _outputChannel.Writer.TryComplete();
                Disconnected?.Invoke();
            }
        }, _cts.Token);
    }
    
    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
    {
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
            // Channel closed - playback ended
        }
        
        return ReadOnlyMemory<byte>.Empty;
    }
    
    /// <inheritdoc />
    public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        // Playback is read-only, input is ignored
        return ValueTask.CompletedTask;
    }
    
    /// <inheritdoc />
    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
    {
        // Resize from outside is ignored - the recording controls the size
        return ValueTask.CompletedTask;
    }
    
    /// <inheritdoc />
    public event Action? Disconnected;
    
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        
        // Cancel playback
        await _cts.CancelAsync();
        
        // Wait for playback to complete
        if (_playbackTask != null)
        {
            try
            {
                await _playbackTask;
            }
            catch
            {
                // Ignore exceptions during cleanup
            }
        }
        
        _outputChannel.Writer.TryComplete();
        _cts.Dispose();
    }
}
