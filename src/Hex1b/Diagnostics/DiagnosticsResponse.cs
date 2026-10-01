using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.Diagnostics;

/// <summary>
/// Base response from the diagnostics socket.
/// </summary>
internal sealed class DiagnosticsResponse
{
    /// <summary>
    /// Whether the request was successful.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Error message if not successful.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// For "info" method: the application name.
    /// </summary>
    [JsonPropertyName("appName")]
    public string? AppName { get; set; }

    /// <summary>
    /// For "info" method: the process ID.
    /// </summary>
    [JsonPropertyName("processId")]
    public int? ProcessId { get; set; }

    /// <summary>
    /// For "info" method: when the process started.
    /// </summary>
    [JsonPropertyName("startTime")]
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>
    /// Terminal width in columns.
    /// </summary>
    [JsonPropertyName("width")]
    public int? Width { get; set; }

    /// <summary>
    /// Terminal height in rows.
    /// </summary>
    [JsonPropertyName("height")]
    public int? Height { get; set; }

    /// <summary>
    /// For "capture" method: the captured content (ANSI or SVG).
    /// </summary>
    [JsonPropertyName("data")]
    public string? Data { get; set; }
    
    /// <summary>
    /// For "tree" method: the widget/node tree.
    /// </summary>
    [JsonPropertyName("tree")]
    public DiagnosticNode? Tree { get; set; }
    
    /// <summary>
    /// For "tree" method: the popup stack.
    /// </summary>
    [JsonPropertyName("popups")]
    public IReadOnlyList<DiagnosticPopupEntry>? Popups { get; set; }
    
    /// <summary>
    /// For "tree" method: focus ring information.
    /// </summary>
    [JsonPropertyName("focusInfo")]
    public DiagnosticFocusInfo? FocusInfo { get; set; }
    
    /// <summary>
    /// For "tree" method: frame-level performance metrics.
    /// </summary>
    [JsonPropertyName("frameInfo")]
    public DiagnosticFrameInfo? FrameInfo { get; set; }
    
    /// <summary>
    /// For "attach" method: whether this client is the resize leader.
    /// </summary>
    [JsonPropertyName("leader")]
    public bool? Leader { get; set; }

    /// <summary>
    /// For "info" and "record-status" methods: whether the terminal is currently recording.
    /// </summary>
    [JsonPropertyName("recording")]
    public bool? Recording { get; set; }

    /// <summary>
    /// For "record-status" and "record-stop" methods: the recording file path.
    /// </summary>
    [JsonPropertyName("recordingPath")]
    public string? RecordingPath { get; set; }
}
