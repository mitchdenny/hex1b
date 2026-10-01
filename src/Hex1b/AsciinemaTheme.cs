using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b.Tokens;

namespace Hex1b;

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
