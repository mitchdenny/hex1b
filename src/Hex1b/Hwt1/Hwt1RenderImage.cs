using System.Text.Json.Serialization;

namespace Hex1b;

internal sealed record Hwt1RenderImage(string Key, int Width, int Height, string Format, [property: JsonIgnore] byte[] Bytes)
{
    public int ByteLength => Bytes.Length;
}
