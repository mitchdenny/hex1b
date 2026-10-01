using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class WorkDoneProgressValue
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = ""; // "begin", "report", "end"

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("percentage")]
    public int? Percentage { get; set; }

    [JsonPropertyName("cancellable")]
    public bool? Cancellable { get; set; }
}
