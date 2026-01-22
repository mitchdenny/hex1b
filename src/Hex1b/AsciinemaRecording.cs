using System.Text;
using System.Text.Json;

namespace Hex1b;

/// <summary>
/// Represents a chapter marker in an asciinema recording.
/// </summary>
/// <param name="Timestamp">The position in seconds where this marker occurs.</param>
/// <param name="Label">The label/title of this marker.</param>
public sealed record AsciinemaMarker(double Timestamp, string Label);

/// <summary>
/// Represents the playback state of an asciinema recording.
/// </summary>
public enum AsciinemaPlaybackState
{
    /// <summary>
    /// Playback has not started yet.
    /// </summary>
    NotStarted,
    
    /// <summary>
    /// Playback is currently running.
    /// </summary>
    Playing,
    
    /// <summary>
    /// Playback is paused.
    /// </summary>
    Paused,
    
    /// <summary>
    /// Playback has completed (reached end of recording).
    /// </summary>
    Completed
}

/// <summary>
/// Provides playback control for an asciinema (.cast) recording file.
/// </summary>
/// <remarks>
/// <para>
/// This class provides a high-level API for controlling asciinema playback with
/// Play, Pause, and Seek operations. It is obtained from the terminal builder
/// using <see cref="Hex1bTerminalBuilder.WithAsciinemaPlayback(string, out AsciinemaRecording, double)"/>.
/// </para>
/// <para>
/// When seeking backwards, the terminal state is automatically reset and events
/// are replayed from the beginning up to the target position.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await using var terminal = Hex1bTerminal.CreateBuilder()
///     .WithAsciinemaPlayback("demo.cast", out var recording)
///     .WithTerminalWidget(out var handle)
///     .Build();
/// 
/// // Wire up to UI controls
/// playButton.OnClick(_ => recording.Play());
/// pauseButton.OnClick(_ => recording.Pause());
/// speedPicker.OnSelectionChanged(e => recording.Play(speeds[e.SelectedIndex]));
/// seekSlider.OnValueChanged(e => recording.Seek(TimeSpan.FromSeconds(e.Value)));
/// </code>
/// </example>
public sealed class AsciinemaRecording
{
    private readonly string _filePath;
    private readonly List<AsciinemaEvent> _events = [];
    private readonly List<AsciinemaMarker> _markers = [];
    private readonly object _lock = new();
    
    private int _width;
    private int _height;
    private double _duration;
    private double _currentPosition;
    private double _speedMultiplier = 1.0;
    private int _currentEventIndex;
    private volatile AsciinemaPlaybackState _state = AsciinemaPlaybackState.NotStarted;
    private bool _eventsLoaded;
    
    // For signaling the playback loop - use SemaphoreSlim for async waiting
    private readonly SemaphoreSlim _controlSignal = new(0);
    private volatile bool _isPaused;
    private volatile bool _seekRequested;
    private double _seekTargetPosition;
    private readonly object _speedLock = new();
    
    // Callback to send output to the terminal
    private Action<ReadOnlyMemory<byte>>? _outputCallback;
    private Action? _clearScreenCallback;
    
    internal AsciinemaRecording(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }
    
    /// <summary>
    /// Gets the width of the recording from the header.
    /// </summary>
    public int Width => _width;
    
    /// <summary>
    /// Gets the height of the recording from the header.
    /// </summary>
    public int Height => _height;
    
    /// <summary>
    /// Gets the chapter markers defined in the recording.
    /// </summary>
    /// <remarks>
    /// Markers are defined in asciinema files using "m" (marker) events.
    /// They can be used to define chapters or important points in the recording
    /// that users can quickly navigate to.
    /// </remarks>
    public IReadOnlyList<AsciinemaMarker> Markers => _markers;
    
    /// <summary>
    /// Gets the total duration of the recording in seconds.
    /// </summary>
    public double Duration => _duration;
    
    /// <summary>
    /// Gets the current playback position in seconds.
    /// </summary>
    public double CurrentPosition
    {
        get { lock (_lock) return _currentPosition; }
    }
    
    /// <summary>
    /// Gets the current playback speed multiplier.
    /// </summary>
    public double SpeedMultiplier
    {
        get { lock (_speedLock) return _speedMultiplier; }
    }
    
    /// <summary>
    /// Gets the current playback state.
    /// </summary>
    public AsciinemaPlaybackState State => _state;
    
    /// <summary>
    /// Event raised when the playback state changes.
    /// </summary>
    public event Action<AsciinemaPlaybackState>? StateChanged;
    
    /// <summary>
    /// Event raised when the playback position changes.
    /// </summary>
    public event Action<double>? PositionChanged;
    
    /// <summary>
    /// Starts or resumes playback at the specified speed.
    /// </summary>
    /// <param name="speed">The playback speed multiplier. Default is 1.0 (normal speed).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when speed is less than or equal to 0.</exception>
    public void Play(double speed = 1.0)
    {
        if (speed <= 0)
            throw new ArgumentOutOfRangeException(nameof(speed), "Speed must be greater than 0");
        
        lock (_speedLock)
        {
            _speedMultiplier = speed;
        }
        
        var oldState = _state;
        if (oldState == AsciinemaPlaybackState.Paused || oldState == AsciinemaPlaybackState.Completed)
        {
            _isPaused = false;
            _state = AsciinemaPlaybackState.Playing;
            
            // Signal the playback loop to wake up
            try { _controlSignal.Release(); } catch (SemaphoreFullException) { }
            
            StateChanged?.Invoke(_state);
        }
    }
    
    /// <summary>
    /// Pauses playback at the current position.
    /// </summary>
    public void Pause()
    {
        if (_state == AsciinemaPlaybackState.Playing)
        {
            _isPaused = true;
            _state = AsciinemaPlaybackState.Paused;
            StateChanged?.Invoke(_state);
        }
    }
    
    /// <summary>
    /// Seeks to the specified position in the recording.
    /// </summary>
    /// <param name="position">The target position to seek to.</param>
    /// <remarks>
    /// <para>
    /// When seeking backwards, the terminal screen is cleared and events are
    /// replayed from the beginning up to the target position without timing delays.
    /// </para>
    /// <para>
    /// When seeking forwards, events are replayed without timing delays until
    /// the target position is reached.
    /// </para>
    /// </remarks>
    public void Seek(TimeSpan position)
    {
        lock (_lock)
        {
            _seekTargetPosition = Math.Max(0, Math.Min(position.TotalSeconds, _duration));
            _seekRequested = true;
        }
        
        // Signal the playback loop to wake up and process the seek
        try { _controlSignal.Release(); } catch (SemaphoreFullException) { }
    }
    
    /// <summary>
    /// Seeks to the specified position in seconds.
    /// </summary>
    /// <param name="seconds">The target position in seconds.</param>
    public void Seek(double seconds) => Seek(TimeSpan.FromSeconds(seconds));
    
    internal void SetCallbacks(Action<ReadOnlyMemory<byte>> outputCallback, Action clearScreenCallback)
    {
        _outputCallback = outputCallback;
        _clearScreenCallback = clearScreenCallback;
    }
    
    internal async Task LoadEventsAsync(CancellationToken ct)
    {
        if (_eventsLoaded) return;
        
        using var stream = File.OpenRead(_filePath);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        
        // Read header
        var headerLine = await reader.ReadLineAsync(ct);
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            throw new InvalidDataException("Invalid asciinema file: missing header");
        }
        
        var header = JsonDocument.Parse(headerLine);
        _width = header.RootElement.GetProperty("width").GetInt32();
        _height = header.RootElement.GetProperty("height").GetInt32();
        
        // Read all events
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line))
                break;
            
            var eventDoc = JsonDocument.Parse(line);
            var eventArray = eventDoc.RootElement;
            
            if (eventArray.GetArrayLength() < 3)
                continue;
            
            var timestamp = eventArray[0].GetDouble();
            var eventType = eventArray[1].GetString() ?? "";
            var eventData = eventArray[2].GetString() ?? "";
            
            _events.Add(new AsciinemaEvent(timestamp, eventType, eventData));
            
            // Collect markers for chapter navigation
            if (eventType == "m" && !string.IsNullOrWhiteSpace(eventData))
            {
                _markers.Add(new AsciinemaMarker(timestamp, eventData));
            }
        }
        
        // Calculate duration from last event
        if (_events.Count > 0)
        {
            _duration = _events[^1].Timestamp;
        }
        
        _eventsLoaded = true;
    }
    
    internal async Task PlaybackLoopAsync(CancellationToken ct)
    {
        await LoadEventsAsync(ct);
        
        _state = AsciinemaPlaybackState.Playing;
        StateChanged?.Invoke(AsciinemaPlaybackState.Playing);
        
        try
        {
            // Main playback loop - runs until cancelled
            // This loop continues even after playback completes, waiting for seek/restart commands
            while (!ct.IsCancellationRequested)
            {
                // Check for seek request (can happen at any time, including when completed)
                if (_seekRequested)
                {
                    double seekTarget;
                    lock (_lock)
                    {
                        seekTarget = _seekTargetPosition;
                        _seekRequested = false;
                    }
                    ProcessSeek(seekTarget);
                    
                    // If we were completed and seek brought us back, transition to paused
                    // (user needs to call Play() to resume)
                    if (_state == AsciinemaPlaybackState.Completed && _currentEventIndex < _events.Count)
                    {
                        _state = AsciinemaPlaybackState.Paused;
                        _isPaused = true;
                        StateChanged?.Invoke(_state);
                    }
                }
                
                // Check for pause or completed state
                while ((_isPaused || _state == AsciinemaPlaybackState.Completed) && !ct.IsCancellationRequested)
                {
                    // Check for seek while paused/completed
                    if (_seekRequested)
                    {
                        double seekTarget;
                        lock (_lock)
                        {
                            seekTarget = _seekTargetPosition;
                            _seekRequested = false;
                        }
                        ProcessSeek(seekTarget);
                        
                        // After seek, remain paused but update state if we can now play
                        if (_state == AsciinemaPlaybackState.Completed && _currentEventIndex < _events.Count)
                        {
                            _state = AsciinemaPlaybackState.Paused;
                            _isPaused = true;
                            StateChanged?.Invoke(_state);
                        }
                    }
                    
                    // Wait for resume signal (with timeout to check for seek)
                    try
                    {
                        await _controlSignal.WaitAsync(100, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        return;
                    }
                    
                    // Check if we should exit the pause loop
                    if (!_isPaused && _state != AsciinemaPlaybackState.Completed)
                    {
                        break;
                    }
                }
                
                // If we reached the end, mark as completed and continue waiting
                if (_currentEventIndex >= _events.Count)
                {
                    if (_state != AsciinemaPlaybackState.Completed)
                    {
                        _state = AsciinemaPlaybackState.Completed;
                        StateChanged?.Invoke(AsciinemaPlaybackState.Completed);
                    }
                    continue; // Go back to waiting for seek/restart
                }
                
                var evt = _events[_currentEventIndex];
                
                // Calculate delay from current position
                var delay = evt.Timestamp - _currentPosition;
                if (delay > 0)
                {
                    double speed;
                    lock (_speedLock)
                    {
                        speed = _speedMultiplier;
                    }
                    var adjustedDelayMs = (int)(delay * 1000 / speed);
                    
                    // Wait in small increments to be responsive to pause/seek
                    var remaining = adjustedDelayMs;
                    const int increment = 50;
                    
                    while (remaining > 0 && !ct.IsCancellationRequested && !_isPaused && !_seekRequested)
                    {
                        var waitTime = Math.Min(remaining, increment);
                        await Task.Delay(waitTime, ct).ConfigureAwait(false);
                        remaining -= waitTime;
                        
                        // Re-check speed in case it changed
                        lock (_speedLock)
                        {
                            speed = _speedMultiplier;
                        }
                    }
                    
                    // If interrupted by pause/seek, restart loop
                    if (_isPaused || _seekRequested)
                        continue;
                }
                
                // Process the event
                ProcessEvent(evt);
                
                lock (_lock)
                {
                    _currentPosition = evt.Timestamp;
                    _currentEventIndex++;
                }
                PositionChanged?.Invoke(evt.Timestamp);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
    }
    
    private void ProcessSeek(double targetPosition)
    {
        int targetIndex = FindEventIndexForPosition(targetPosition);
        
        lock (_lock)
        {
            // If seeking backwards or to a position before current event, reset and replay
            if (targetPosition < _currentPosition || targetIndex < _currentEventIndex)
            {
                _clearScreenCallback?.Invoke();
                
                // Replay all events from start to target
                for (int i = 0; i <= targetIndex && i < _events.Count; i++)
                {
                    ProcessEvent(_events[i]);
                }
                
                _currentEventIndex = Math.Max(0, targetIndex + 1);
                _currentPosition = targetPosition;
            }
            else
            {
                // Fast-forward: replay events without delays
                for (int i = _currentEventIndex; i <= targetIndex && i < _events.Count; i++)
                {
                    ProcessEvent(_events[i]);
                }
                
                _currentEventIndex = Math.Max(0, targetIndex + 1);
                _currentPosition = targetPosition;
            }
        }
        
        PositionChanged?.Invoke(targetPosition);
    }
    
    private int FindEventIndexForPosition(double position)
    {
        // Binary search for the last event at or before the target position
        int left = 0;
        int right = _events.Count - 1;
        int result = -1;
        
        while (left <= right)
        {
            int mid = left + (right - left) / 2;
            if (_events[mid].Timestamp <= position)
            {
                result = mid;
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }
        
        return result;
    }
    
    private void ProcessEvent(AsciinemaEvent evt)
    {
        switch (evt.EventType)
        {
            case "o": // Output event
                var bytes = Encoding.UTF8.GetBytes(evt.Data);
                _outputCallback?.Invoke(bytes);
                break;
            
            case "r": // Resize event
                var parts = evt.Data.Split('x');
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], out var width) &&
                    int.TryParse(parts[1], out var height))
                {
                    _width = width;
                    _height = height;
                }
                break;
            
            // "i" (input) and "m" (marker) events are ignored
        }
    }
    
    private readonly record struct AsciinemaEvent(double Timestamp, string EventType, string Data);
}
