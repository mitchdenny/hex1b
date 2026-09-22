using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b.Tokens;

namespace Hex1b;

internal sealed class AsciinemaEventConverter : JsonConverter<AsciinemaEvent>
{
    public override AsciinemaEvent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException();

        reader.Read();
        var time = reader.GetDouble();
        reader.Read();
        var code = reader.GetString() ?? "";
        reader.Read();
        var data = reader.GetString() ?? "";
        reader.Read(); // EndArray

        return new AsciinemaEvent(time, code, data);
    }

    public override void Write(Utf8JsonWriter writer, AsciinemaEvent value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(Math.Round(value.Time, 6));
        writer.WriteStringValue(value.Code);
        writer.WriteStringValue(value.Data);
        writer.WriteEndArray();
    }
}
