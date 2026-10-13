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
[JsonSerializable(typeof(Hmp1KgpAnimationState))]
[JsonSerializable(typeof(Hmp1ActivityState))]
[JsonSerializable(typeof(Hmp1ProgramStatusState))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class Hmp1JsonContext : JsonSerializerContext
{
}
