using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Hex1b;

/// <summary>
/// A workload adapter that plays back an asciinema (.cast) recording file.
/// </summary>
/// <remarks>
/// <para>
/// This adapter reads asciicast v2 format files and replays the terminal output
/// with proper timing. It's useful for:
/// </para>
/// <list type="bullet">
///   <item>Playing back recorded terminal sessions</item>
///   <item>Creating demos and tutorials from recordings</item>
///   <item>Testing terminal rendering with real-world data</item>
///   <item>Converting asciinema files to other formats</item>
/// </list>
/// <para>
/// The adapter reads events from the file and streams them at the appropriate times
/// based on the timestamps in the recording. Output events ("o" type) are converted
/// to bytes and returned via <see cref="ReadOutputAsync"/>. Resize events ("r" type)
/// trigger the <see cref="ResizeAsync"/> callback.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await using var terminal = Hex1bTerminal.CreateBuilder()
///     .WithAsciinemaFile("recording.cast")
///     .Build();
/// 
/// await terminal.RunAsync();
/// </code>
/// </example>
public sealed class AsciinemaFileWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    private readonly string _filePath;
    private readonly Channel<ReadOnlyMemory<byte>> _outputChannel;
    private readonly CancellationTokenSource _cts = new();
    private Task? _playbackTask;
    private bool _disposed;
    private bool _started;
    private double _speedMultiplier = 1.0;
    
    /// <summary>
    /// Creates a new asciinema file workload adapter.
    /// </summary>
    /// <param name="filePath">Path to the .cast file to play back.</param>
    public AsciinemaFileWorkloadAdapter(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        _outputChannel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
    }

    /// <summary>
    /// Gets or sets the playback speed multiplier. Default is 1.0 (normal speed).
    /// Set to 2.0 for 2x speed, 0.5 for half speed, etc.
    /// </summary>
    public double SpeedMultiplier
    {
        get => _speedMultiplier;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Speed multiplier must be greater than 0");
            _speedMultiplier = value;
        }
    }

    /// <summary>
    /// Gets the width from the asciinema header, or 0 if not yet read.
    /// </summary>
    public int Width { get; private set; }

    /// <summary>
    /// Gets the height from the asciinema header, or 0 if not yet read.
    /// </summary>
    public int Height { get; private set; }

    /// <summary>
    /// Starts playback of the asciinema file.
    /// </summary>
    public Task StartAsync(CancellationToken ct = default)
    {
        if (_started)
            throw new InvalidOperationException("Playback has already been started.");

        if (_disposed)
            throw new ObjectDisposedException(nameof(AsciinemaFileWorkloadAdapter));

        if (!File.Exists(_filePath))
            throw new FileNotFoundException($"Asciinema file not found: {_filePath}", _filePath);

        _started = true;
        _playbackTask = PlaybackLoopAsync(_cts.Token);

        return Task.CompletedTask;
    }

    private async Task PlaybackLoopAsync(CancellationToken ct)
    {
        try
        {
            using var stream = File.OpenRead(_filePath);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            // Read header
            var headerLine = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                throw new InvalidDataException("Invalid asciinema file: missing header");
            }

            var header = JsonDocument.Parse(headerLine);
            Width = header.RootElement.GetProperty("width").GetInt32();
            Height = header.RootElement.GetProperty("height").GetInt32();

            double previousTimestamp = 0;

            // Read and replay events
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(line))
                {
                    break; // End of file
                }

                var eventDoc = JsonDocument.Parse(line);
                var eventArray = eventDoc.RootElement;

                if (eventArray.GetArrayLength() < 3)
                {
                    continue; // Invalid event, skip
                }

                var timestamp = eventArray[0].GetDouble();
                var eventType = eventArray[1].GetString();
                var eventData = eventArray[2].GetString() ?? "";

                // Calculate delay based on timestamp difference
                var delay = timestamp - previousTimestamp;
                if (delay > 0)
                {
                    // Apply speed multiplier
                    var adjustedDelay = delay / _speedMultiplier;
                    await Task.Delay(TimeSpan.FromSeconds(adjustedDelay), ct);
                }
                previousTimestamp = timestamp;

                // Handle event based on type
                switch (eventType)
                {
                    case "o": // Output event
                        var bytes = Encoding.UTF8.GetBytes(eventData);
                        await _outputChannel.Writer.WriteAsync(bytes, ct);
                        break;

                    case "r": // Resize event
                        // Parse "WxH" format
                        var parts = eventData.Split('x');
                        if (parts.Length == 2 && 
                            int.TryParse(parts[0], out var width) && 
                            int.TryParse(parts[1], out var height))
                        {
                            Width = width;
                            Height = height;
                            // Note: We can't directly trigger a resize from here
                            // The terminal would need to handle this through ResizeAsync
                        }
                        break;

                    case "i": // Input event (we ignore these during playback)
                    case "m": // Marker event (we ignore these during playback)
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            // Log error or handle as needed
            System.Diagnostics.Debug.WriteLine($"Playback error: {ex.Message}");
        }
        finally
        {
            _outputChannel.Writer.TryComplete();
            Disconnected?.Invoke();
        }
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
        // Asciinema file playback is read-only, input is ignored
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
    {
        // Resize from outside is ignored - the file controls the size
        // However, we could potentially pause/resume playback here if needed
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
