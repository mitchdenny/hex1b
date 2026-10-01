using System.Text;
using Hex1b.Theming;
using static Hex1b.Hmp1BinaryEncoding;

namespace Hex1b;

internal static class Hmp1ScrollbackRowCodec
{
    private const int MaxWidth = 16_384;
    private const CellAttributes AllowedAttributes = CellAttributes.Bold | CellAttributes.Dim |
        CellAttributes.Italic | CellAttributes.Underline | CellAttributes.Blink | CellAttributes.Reverse |
        CellAttributes.Hidden | CellAttributes.Strikethrough | CellAttributes.Overline |
        CellAttributes.SoftWrap | CellAttributes.Protected;

    internal static byte[] Encode(ScrollbackRow row)
    {
        if (row.OriginalWidth <= 0 || row.OriginalWidth > MaxWidth ||
            row.Cells.Length <= 0 || row.Cells.Length > MaxWidth)
            throw new InvalidDataException("Scrollback row exceeds the supported width.");
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Utf8);
        writer.Write(row.OriginalWidth);
        writer.Write(row.Timestamp.UtcTicks);
        writer.Write(row.Cells.Length);
        foreach (var cell in row.Cells)
        {
            WriteString(writer, cell.Character);
            writer.Write((ushort)cell.Attributes);
            writer.Write((byte)(cell.IsWideWrapPadding ? 1 : 0));
            WriteColor(writer, cell.Foreground);
            WriteColor(writer, cell.Background);
            WriteColor(writer, cell.UnderlineColor);
            writer.Write((byte)cell.UnderlineStyle);
            writer.Write((byte)(cell.HyperlinkData is null ? 0 : 1));
            if (cell.HyperlinkData is { } link)
            {
                WriteString(writer, link.Uri);
                WriteString(writer, link.Parameters);
            }
            if (stream.Length > Hmp1ScrollbackState.MaxChunkBytes - 4)
                throw new InvalidDataException("Scrollback row exceeds the frame limit.");
        }
        return stream.ToArray();
    }

    internal static (ScrollbackRow Row, Dictionary<int, (string Uri, string Parameters)> Links) Decode(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = new BinaryReader(stream, Utf8);
            var width = reader.ReadInt32();
            var ticks = reader.ReadInt64();
            var count = reader.ReadInt32();
            if (width <= 0 || width > MaxWidth || count <= 0 || count > MaxWidth ||
                ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
                throw new InvalidDataException("Invalid scrollback row geometry or timestamp.");
            var timestamp = new DateTimeOffset(ticks, TimeSpan.Zero);
            var cells = new TerminalCell[count];
            var links = new Dictionary<int, (string, string)>();
            for (var i = 0; i < count; i++)
            {
                var text = ReadString(reader);
                var attributes = (CellAttributes)reader.ReadUInt16();
                var padding = reader.ReadByte();
                var foreground = ReadColor(reader);
                var background = ReadColor(reader);
                var underlineColor = ReadColor(reader);
                var underline = (UnderlineStyle)reader.ReadByte();
                var hyperlink = reader.ReadByte();
                if ((attributes & ~AllowedAttributes) != 0 || padding > 1 ||
                    underline > UnderlineStyle.Dashed || hyperlink > 1)
                    throw new InvalidDataException("Invalid scrollback cell attributes.");
                cells[i] = new(text, foreground, background, attributes,
                    UnderlineColor: underlineColor, UnderlineStyle: underline)
                {
                    IsWideWrapPadding = padding != 0
                };
                if (hyperlink != 0)
                    links.Add(i, (ReadString(reader), ReadString(reader)));
            }
            if (stream.Position != stream.Length)
                throw new InvalidDataException("Trailing scrollback row data.");
            return (new(cells, width, timestamp), links);
        }
        catch (Exception error) when (error is EndOfStreamException or DecoderFallbackException)
        {
            throw new InvalidDataException("Malformed scrollback row.", error);
        }
    }

    private static void WriteColor(BinaryWriter writer, Hex1bColor? color)
    {
        writer.Write(color is not { } value ? (byte)255 : value.IsDefault ? (byte)254 : (byte)value.Kind);
        if (color is not { IsDefault: false } rgb)
            return;
        writer.Write(rgb.R);
        writer.Write(rgb.G);
        writer.Write(rgb.B);
        writer.Write(rgb.AnsiIndex);
    }

    private static Hex1bColor? ReadColor(BinaryReader reader)
    {
        var kind = reader.ReadByte();
        if (kind == 255)
            return null;
        if (kind == 254)
            return Hex1bColor.Default;
        var r = reader.ReadByte();
        var g = reader.ReadByte();
        var b = reader.ReadByte();
        var index = reader.ReadByte();
        return (Hex1bColorKind)kind switch
        {
            Hex1bColorKind.Rgb when index == 0 => Hex1bColor.FromRgb(r, g, b),
            Hex1bColorKind.Standard when index < 8 => Hex1bColor.FromStandard(index, r, g, b),
            Hex1bColorKind.Bright when index < 8 => Hex1bColor.FromBright(index, r, g, b),
            Hex1bColorKind.Indexed => Hex1bColor.FromIndexed(index, r, g, b),
            _ => throw new InvalidDataException("Invalid scrollback cell color.")
        };
    }
}
