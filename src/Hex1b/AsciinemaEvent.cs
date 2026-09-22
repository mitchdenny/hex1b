using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b.Tokens;

namespace Hex1b;

/// <summary>
/// Represents an event in the asciicast format.
/// Serializes as a 3-element JSON array: [time, code, data]
/// </summary>
[JsonConverter(typeof(AsciinemaEventConverter))]
internal readonly record struct AsciinemaEvent(double Time, string Code, string Data);
