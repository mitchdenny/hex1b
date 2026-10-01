using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b.Tokens;

namespace Hex1b;

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
