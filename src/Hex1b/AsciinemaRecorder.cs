using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b.Tokens;

namespace Hex1b;

/// <summary>
/// Records terminal sessions in asciicast v2 format (Asciinema).
/// </summary>
/// <remarks>
/// <para>
/// This filter captures terminal output and optionally input, producing files
/// compatible with the Asciinema player and ecosystem.
/// </para>
/// <para>
/// The asciicast v2 format is newline-delimited JSON:
/// <list type="bullet">
///   <item>First line: Header with metadata (version, dimensions, timestamp)</item>
///   <item>Subsequent lines: Events as [time, type, data] tuples</item>
/// </list>
/// </para>
/// <example>
/// <code>
/// var options = new Hex1bTerminalOptions { ... };
/// var recorder = options.AddAsciinemaRecorder("demo.cast", new AsciinemaRecorderOptions { Title = "Demo" });
/// var terminal = new Hex1bTerminal(options);
/// // ... run application ...
/// // Recording is automatically saved on dispose, or call FlushAsync() to flush buffered events
/// </code>
/// </example>
/// </remarks>
/// <seealso href="https://docs.asciinema.org/manual/asciicast/v2/"/>
public sealed class AsciinemaRecorder : IHex1bTerminalWorkloadFilter, IAsyncDisposable, IDisposable
{
    private AsciinemaRecorderOptions _options;
    private string? _filePath;
    private readonly List<AsciinemaEvent> _pendingEvents = new();
    private readonly object _lock = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private FileStream? _fileStream;
    private StreamWriter? _writer;
    private int _width;
    private int _height;
    private DateTimeOffset _timestamp;
    private DateTimeOffset _recordingStartTime;
    private bool _headerWritten;
    private bool _disposed;
    private bool _isRecording;

    /// <summary>
    /// Creates a new Asciinema recorder in idle mode (not recording).
    /// </summary>
    /// <remarks>
    /// Use <see cref="StartRecording"/> to begin recording to a file.
    /// This constructor is useful for dynamic recording scenarios where
    /// recording starts after the terminal session has already begun.
    /// </remarks>
    public AsciinemaRecorder()
    {
        _filePath = null;
        _options = new AsciinemaRecorderOptions();
        _isRecording = false;
    }

    /// <summary>
    /// Creates a new Asciinema recorder that writes to the specified file.
    /// Recording starts immediately when the session starts.
    /// </summary>
    /// <param name="filePath">Path to the output file (typically with .cast extension).</param>
    /// <param name="options">Recording options. If null, defaults are used.</param>
    public AsciinemaRecorder(string filePath, AsciinemaRecorderOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        _options = options ?? new AsciinemaRecorderOptions();
        _isRecording = true; // Start recording immediately for backward compatibility
    }

    /// <summary>
    /// Gets the recording options.
    /// </summary>
    public AsciinemaRecorderOptions Options => _options;

    /// <summary>
    /// Gets the file path being written to, or null if not recording.
    /// </summary>
    public string? FilePath => _filePath;

    /// <summary>
    /// Gets whether the recorder is currently recording.
    /// </summary>
    public bool IsRecording => _isRecording;

    /// <summary>
    /// Gets the number of events pending flush.
    /// </summary>
    public int PendingEventCount
    {
        get
        {
            lock (_lock)
            {
                return _pendingEvents.Count;
            }
        }
    }

    /// <summary>
    /// Starts recording to the specified file.
    /// </summary>
    /// <param name="filePath">Path to the output file (typically with .cast extension).</param>
    /// <param name="width">Terminal width in columns.</param>
    /// <param name="height">Terminal height in rows.</param>
    /// <param name="options">Recording options. If null, defaults are used.</param>
    /// <exception cref="InvalidOperationException">Thrown if already recording.</exception>
    /// <remarks>
    /// <para>
    /// This method is used for dynamic recording scenarios where recording starts
    /// after the terminal session has already begun. Use <see cref="WriteInitialStateAsync"/>
    /// to capture the current terminal state before continuing.
    /// </para>
    /// </remarks>
    public void StartRecording(string filePath, int width, int height, AsciinemaRecorderOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        
        lock (_lock)
        {
            if (_isRecording)
            {
                throw new InvalidOperationException("Already recording. Call StopRecordingAsync() first.");
            }

            _filePath = filePath;
            _options = options ?? new AsciinemaRecorderOptions();
            _width = width;
            _height = height;
            _recordingStartTime = DateTimeOffset.UtcNow;
            _timestamp = _recordingStartTime;
            _headerWritten = false;
            _pendingEvents.Clear();
            _isRecording = true;
        }
    }

    /// <summary>
    /// Stops recording and finalizes the current file.
    /// </summary>
    /// <returns>The path to the finalized recording file, or null if not recording.</returns>
    /// <remarks>
    /// After calling this method, <see cref="StartRecording"/> can be called again
    /// to start a new recording to a different file.
    /// </remarks>
    public async Task<string?> StopRecordingAsync(CancellationToken ct = default)
    {
        string? completedFilePath;
        
        lock (_lock)
        {
            if (!_isRecording)
            {
                return null;
            }

            _isRecording = false;
            completedFilePath = _filePath;
        }

        // Flush any remaining events and close the file
        await FlushAsync(ct);
        
        await _writeLock.WaitAsync(ct);
        try
        {
            if (_writer != null)
            {
                await _writer.DisposeAsync();
                _writer = null;
            }
            _fileStream = null;
        }
        finally
        {
            _writeLock.Release();
        }

        return completedFilePath;
    }

    /// <summary>
    /// Writes synthesized initial state as the first output event.
    /// </summary>
    /// <param name="ansiContent">ANSI content representing the current terminal state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// <para>
    /// This method is used when starting a recording mid-session to capture
    /// the current terminal state. The content should be ANSI escape sequences
    /// that recreate the current screen (clear, position cursor, set attributes, print text).
    /// </para>
    /// <para>
    /// The event is written at time 0, before any subsequent output events.
    /// </para>
    /// </remarks>
    public async Task WriteInitialStateAsync(string ansiContent, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(ansiContent)) return;

        lock (_lock)
        {
            if (!_isRecording) return;
            
            // Insert at the beginning with time 0
            _pendingEvents.Insert(0, new AsciinemaEvent(0, "o", ansiContent));
        }

        if (_options.AutoFlush)
        {
            await FlushAsync(ct);
        }
    }

    /// <summary>
    /// Adds a marker event at the current time.
    /// </summary>
    /// <param name="label">Optional label for the marker.</param>
    /// <param name="elapsed">Time elapsed since session start.</param>
    public void AddMarker(string label = "", TimeSpan? elapsed = null)
    {
        lock (_lock)
        {
            if (!_isRecording) return;
            
            var time = elapsed ?? (_headerWritten ? DateTimeOffset.UtcNow - _timestamp : TimeSpan.Zero);
            _pendingEvents.Add(new AsciinemaEvent(time.TotalSeconds, "m", label));
        }
    }

    /// <inheritdoc />
    async ValueTask IHex1bTerminalWorkloadFilter.OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct)
    {
        lock (_lock)
        {
            _width = width;
            _height = height;
            _timestamp = timestamp;
            _recordingStartTime = timestamp;
        }
        await ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    async ValueTask IHex1bTerminalWorkloadFilter.OnOutputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct)
    {
        if (tokens.Count == 0) return;

        // Serialize tokens back to ANSI text for recording
        var text = AnsiTokenSerializer.Serialize(tokens);
        lock (_lock)
        {
            if (!_isRecording) return;
            
            // Calculate elapsed time relative to when recording started
            var recordingElapsed = GetRecordingElapsed(elapsed);
            _pendingEvents.Add(new AsciinemaEvent(recordingElapsed.TotalSeconds, "o", text));
        }

        if (_options.AutoFlush)
        {
            await FlushAsync(ct);
        }
    }

    /// <inheritdoc />
    ValueTask IHex1bTerminalWorkloadFilter.OnFrameCompleteAsync(TimeSpan elapsed, CancellationToken ct)
    {
        // Frame boundaries are implicit in Asciinema - we don't need to record them
        // But this could be useful for adding markers or other analysis
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    async ValueTask IHex1bTerminalWorkloadFilter.OnInputAsync(IReadOnlyList<Tokens.AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct)
    {
        // Only record input if explicitly enabled (per Asciinema spec recommendation)
        if (!_options.CaptureInput) return;
        if (tokens.Count == 0) return;

        var text = Tokens.AnsiTokenSerializer.Serialize(tokens);
        lock (_lock)
        {
            if (!_isRecording) return;
            
            var recordingElapsed = GetRecordingElapsed(elapsed);
            _pendingEvents.Add(new AsciinemaEvent(recordingElapsed.TotalSeconds, "i", text));
        }

        if (_options.AutoFlush)
        {
            await FlushAsync(ct);
        }
    }

    /// <inheritdoc />
    async ValueTask IHex1bTerminalWorkloadFilter.OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct)
    {
        lock (_lock)
        {
            _width = width;
            _height = height;
            
            if (!_isRecording) return;
            
            var recordingElapsed = GetRecordingElapsed(elapsed);
            _pendingEvents.Add(new AsciinemaEvent(recordingElapsed.TotalSeconds, "r", $"{width}x{height}"));
        }

        if (_options.AutoFlush)
        {
            await FlushAsync(ct);
        }
    }

    /// <inheritdoc />
    ValueTask IHex1bTerminalWorkloadFilter.OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct)
    {
        // Session end is implicit - no specific event needed
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Calculates elapsed time relative to when recording started.
    /// </summary>
    /// <remarks>
    /// For recordings started at session start, this returns the session elapsed time.
    /// For recordings started mid-session, this returns time since StartRecording was called.
    /// </remarks>
    private TimeSpan GetRecordingElapsed(TimeSpan sessionElapsed)
    {
        // If recording started at session start, use session elapsed directly
        if (_recordingStartTime == _timestamp)
        {
            return sessionElapsed;
        }
        
        // For mid-session recordings, calculate from when recording started
        var sessionStart = _timestamp;
        var recordingOffset = _recordingStartTime - sessionStart;
        var recordingElapsed = sessionElapsed - recordingOffset;
        
        return recordingElapsed < TimeSpan.Zero ? TimeSpan.Zero : recordingElapsed;
    }

    /// <summary>
    /// Flushes any pending events to the file.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        List<AsciinemaEvent> eventsToWrite;
        AsciinemaHeader? header = null;

        lock (_lock)
        {
            // Can't flush if no file path is set
            if (_filePath == null)
                return;
                
            if (_pendingEvents.Count == 0 && _headerWritten)
                return;

            if (!_headerWritten)
            {
                header = new AsciinemaHeader
                {
                    Version = 2,
                    Width = _width,
                    Height = _height,
                    Timestamp = _recordingStartTime.ToUnixTimeSeconds(),
                    Title = _options.Title,
                    Command = _options.Command,
                    IdleTimeLimit = _options.IdleTimeLimit,
                    Env = _options.CaptureEnvironment ? new Dictionary<string, string>
                    {
                        ["TERM"] = Environment.GetEnvironmentVariable("TERM") ?? "xterm-256color",
                        ["SHELL"] = Environment.GetEnvironmentVariable("SHELL") ?? ""
                    } : null,
                    Theme = _options.Theme
                };
                _headerWritten = true;
            }

            eventsToWrite = new List<AsciinemaEvent>(_pendingEvents);
            _pendingEvents.Clear();
        }

        await _writeLock.WaitAsync(ct);
        try
        {
            await EnsureStreamOpenAsync();

            if (header != null)
            {
                var headerJson = JsonSerializer.Serialize(header, AsciinemaJsonContext.Default.AsciinemaHeader);
                await _writer!.WriteLineAsync(headerJson);
            }

            foreach (var evt in eventsToWrite)
            {
                ct.ThrowIfCancellationRequested();
                var eventJson = JsonSerializer.Serialize(evt, AsciinemaJsonContext.Default.AsciinemaEvent);
                await _writer!.WriteLineAsync(eventJson);
            }

            await _writer!.FlushAsync(ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task EnsureStreamOpenAsync()
    {
        if (_fileStream == null && _filePath != null)
        {
            _fileStream = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            // Use UTF-8 without BOM - asciinema player doesn't handle BOM
            _writer = new StreamWriter(_fileStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: false);
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Clears all pending events (events already flushed to disk remain).
    /// </summary>
    public void ClearPending()
    {
        lock (_lock)
        {
            _pendingEvents.Clear();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Flush any remaining events
        try
        {
            await FlushAsync();
        }
        catch
        {
            // Best effort flush on dispose
        }

        await _writeLock.WaitAsync();
        try
        {
            if (_writer != null)
            {
                await _writer.DisposeAsync();
                _writer = null;
            }
            _fileStream = null;
        }
        finally
        {
            _writeLock.Release();
        }
        
        _writeLock.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}

/// <summary>
/// Options for configuring the Asciinema recorder.
/// </summary>
public sealed class AsciinemaRecorderOptions
{
    /// <summary>
    /// Title of the recording.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Command that was recorded.
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// Idle time limit - delays longer than this are compressed during playback.
    /// </summary>
    public float? IdleTimeLimit { get; set; }

    /// <summary>
    /// Whether to capture keyboard input. Off by default per Asciinema spec.
    /// </summary>
    public bool CaptureInput { get; set; }

    /// <summary>
    /// Whether to capture environment variables (TERM, SHELL).
    /// </summary>
    public bool CaptureEnvironment { get; set; } = true;

    /// <summary>
    /// Whether to automatically flush events to the file as they are received.
    /// When false, events are buffered until <see cref="AsciinemaRecorder.FlushAsync"/> is called
    /// or the recorder is disposed.
    /// </summary>
    public bool AutoFlush { get; set; } = true;

    /// <summary>
    /// Terminal color theme for playback.
    /// </summary>
    public AsciinemaTheme? Theme { get; set; }
}

/// <summary>
/// Terminal color theme for Asciinema recordings.
/// </summary>
public sealed class AsciinemaTheme
{
    /// <summary>
    /// Foreground color in CSS #rrggbb format.
    /// </summary>
    [JsonPropertyName("fg")]
    public string? Foreground { get; set; }

    /// <summary>
    /// Background color in CSS #rrggbb format.
    /// </summary>
    [JsonPropertyName("bg")]
    public string? Background { get; set; }

    /// <summary>
    /// Color palette (8 or 16 colors, colon-separated, CSS #rrggbb format).
    /// </summary>
    [JsonPropertyName("palette")]
    public string? Palette { get; set; }
}

// Internal types for JSON serialization

internal sealed class AsciinemaHeader
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("timestamp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long Timestamp { get; set; }

    [JsonPropertyName("duration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Duration { get; set; }

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; }

    [JsonPropertyName("command")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Command { get; set; }

    [JsonPropertyName("idle_time_limit")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? IdleTimeLimit { get; set; }

    [JsonPropertyName("env")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Env { get; set; }

    [JsonPropertyName("theme")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AsciinemaTheme? Theme { get; set; }
}

/// <summary>
/// Represents an event in the asciicast format.
/// Serializes as a 3-element JSON array: [time, code, data]
/// </summary>
[JsonConverter(typeof(AsciinemaEventConverter))]
internal readonly record struct AsciinemaEvent(double Time, string Code, string Data);

internal sealed class AsciinemaEventConverter : JsonConverter<AsciinemaEvent>
{
    public override AsciinemaEvent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException();

        reader.Read();
        var time = reader.GetDouble();
        reader.Read();
        var code = reader.GetString() ?? "";
        reader.Read();
        var data = reader.GetString() ?? "";
        reader.Read(); // EndArray

        return new AsciinemaEvent(time, code, data);
    }

    public override void Write(Utf8JsonWriter writer, AsciinemaEvent value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(Math.Round(value.Time, 6));
        writer.WriteStringValue(value.Code);
        writer.WriteStringValue(value.Data);
        writer.WriteEndArray();
    }
}

[JsonSerializable(typeof(AsciinemaHeader))]
[JsonSerializable(typeof(AsciinemaEvent))]
[JsonSourceGenerationOptions(WriteIndented = false)]
internal sealed partial class AsciinemaJsonContext : JsonSerializerContext
{
}
