using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b.Tokens;

namespace Hex1b;

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
