using System.Text.Json.Serialization;

namespace Hex1b;

/// <summary>
/// Source-generated JSON context for muxer protocol types.
/// </summary>
[JsonSerializable(typeof(HelloPayload))]
[JsonSerializable(typeof(HelloPeerInfo))]
[JsonSerializable(typeof(ClientHelloPayload))]
[JsonSerializable(typeof(RequestPrimaryPayload))]
[JsonSerializable(typeof(RoleChangePayload))]
[JsonSerializable(typeof(PeerJoinPayload))]
[JsonSerializable(typeof(PeerLeavePayload))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class Hmp1JsonContext : JsonSerializerContext
{
}
