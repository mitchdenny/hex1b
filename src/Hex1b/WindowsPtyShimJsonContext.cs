using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Hex1b;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(WindowsPtyShimLaunchRequest))]
[JsonSerializable(typeof(WindowsPtyShimStartedResponse))]
[JsonSerializable(typeof(WindowsPtyShimResizeRequest))]
[JsonSerializable(typeof(WindowsPtyShimExitNotification))]
[JsonSerializable(typeof(WindowsPtyShimErrorResponse))]
internal sealed partial class WindowsPtyShimJsonContext : JsonSerializerContext
{
}
