using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Hex1b;

internal sealed record WindowsPtyShimResizeRequest(int Width, int Height);
