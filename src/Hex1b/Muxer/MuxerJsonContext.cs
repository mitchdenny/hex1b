using System.Text.Json.Serialization;

namespace Hex1b.Muxer;

/// <summary>
/// Source-generated JSON context for muxer protocol types.
/// </summary>
[JsonSerializable(typeof(HelloPayload))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class MuxerJsonContext : JsonSerializerContext
{
}
