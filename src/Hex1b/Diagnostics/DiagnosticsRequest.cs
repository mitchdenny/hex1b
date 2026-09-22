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

    /// <summary>
    /// For "record-start" method, the output file path (.cast).
    /// </summary>
    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }

    /// <summary>
    /// For "record-start" method, the recording title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// For "record-start" method, max idle time in seconds between frames.
    /// </summary>
    [JsonPropertyName("idleLimit")]
    public double? IdleLimit { get; set; }

    /// <summary>
    /// For "capture" method with "svg" or "html" format, overrides the font family used in rendering.
    /// </summary>
    [JsonPropertyName("fontFamily")]
    public string? FontFamily { get; set; }
}
