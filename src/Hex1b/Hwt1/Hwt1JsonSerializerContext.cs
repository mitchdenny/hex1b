using System.Text.Json.Serialization;

namespace Hex1b;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Hwt1FrameMetadata))]
internal partial class Hwt1JsonSerializerContext : JsonSerializerContext;
