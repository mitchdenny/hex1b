using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b.Tokens;

namespace Hex1b;

[JsonSerializable(typeof(AsciinemaHeader))]
[JsonSerializable(typeof(AsciinemaEvent))]
[JsonSourceGenerationOptions(WriteIndented = false)]
internal sealed partial class AsciinemaJsonContext : JsonSerializerContext
{
}
