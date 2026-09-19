using System.Text;

namespace Hex1b;

internal static class Hmp1BinaryEncoding
{
    private const int MaxStringBytes = 65_536;
    internal static readonly UTF8Encoding Utf8 = new(false, true);

    internal static void WriteString(BinaryWriter writer, string text)
    {
        var length = Utf8.GetByteCount(text);
        if (length > MaxStringBytes)
            throw new InvalidDataException("HMP string exceeds its limit.");
        writer.Write(length);
        writer.Write(Utf8.GetBytes(text));
    }

    internal static string ReadString(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if (length < 0 || length > MaxStringBytes || length > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new InvalidDataException("Invalid HMP string length.");
        return Utf8.GetString(reader.ReadBytes(length));
    }
}
