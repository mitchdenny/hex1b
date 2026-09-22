using System.Text;
using System.Text.Json;

namespace Hex1b;

/// <summary>
/// Represents a chapter marker in an asciinema recording.
/// </summary>
/// <param name="Timestamp">The position in seconds where this marker occurs.</param>
/// <param name="Label">The label/title of this marker.</param>
public sealed record AsciinemaMarker(double Timestamp, string Label);
