using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.Diagnostics;

/// <summary>
/// Request sent to the diagnostics socket.
/// </summary>
internal sealed class DiagnosticsRequest
{
    /// <summary>
    /// The method to invoke: "info", "capture", or "input".
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    /// <summary>
    /// For "capture" method, the format: "ansi" or "svg".
    /// </summary>
    [JsonPropertyName("format")]
    public string? Format { get; set; }

    /// <summary>
    /// For "input" method, the characters to send to the terminal.
    /// </summary>
    [JsonPropertyName("data")]
    public string? Data { get; set; }

    /// <summary>
    /// For "key" method, the key name (e.g., "Enter", "Tab", "N", "F1").
    /// </summary>
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    /// <summary>
    /// For "key" method, the modifiers (e.g., ["Alt"], ["Ctrl", "Shift"]).
    /// </summary>
    [JsonPropertyName("modifiers")]
    public string[]? Modifiers { get; set; }

    /// <summary>
    /// For "click" method, the X position (column, 0-based).
    /// </summary>
    [JsonPropertyName("x")]
    public int? X { get; set; }

    /// <summary>
    /// For "click" method, the Y position (row, 0-based).
    /// </summary>
    [JsonPropertyName("y")]
    public int? Y { get; set; }

    /// <summary>
    /// For "click" method, the mouse button ("left", "right", "middle").
    /// </summary>
    [JsonPropertyName("button")]
    public string? Button { get; set; }

    /// <summary>
    /// For "capture" method, the number of scrollback lines to include (default 0).
    /// </summary>
    [JsonPropertyName("scrollbackLines")]
    public int? ScrollbackLines { get; set; }

    /// <summary>
    /// For "drag" method, the destination X position (column, 0-based).
    /// </summary>
    [JsonPropertyName("x2")]
    public int? X2 { get; set; }

    /// <summary>
    /// For "drag" method, the destination Y position (row, 0-based).
    /// </summary>
    [JsonPropertyName("y2")]
    public int? Y2 { get; set; }
}

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
}

/// <summary>
/// JSON serialization options for diagnostics protocol.
/// </summary>
internal static class DiagnosticsJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
