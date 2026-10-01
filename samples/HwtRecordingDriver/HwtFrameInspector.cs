using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace HwtRecordingDriver;

// A deliberately local, same-build decoder: this is not an archive format API.
internal sealed class HwtFrameInspector
{
    private string[] _cells = [];
    private readonly Dictionary<string, (int Width, int Height)> _images = [];
    private readonly HashSet<string> _observedKinds = [];
    private uint _revision;
    private int _columns;
    private int _rows;

    public string Text { get; private set; } = "";
    public bool HasBothGraphics { get; private set; }
    public int ImagePayloads { get; private set; }
    public long ImageBytes { get; private set; }
    public IReadOnlyCollection<string> ObservedKinds => _observedKinds;

    public uint Apply(ReadOnlyMemory<byte> frame)
    {
        var bytes = frame.Span;
        Require(bytes.Length >= 12 && bytes[..4].SequenceEqual("HWT1"u8), "Missing HWT1 header.");
        var metadataLength = BinaryPrimitives.ReadInt32LittleEndian(bytes[4..]);
        Require(metadataLength > 0 && metadataLength <= bytes.Length - 12, "Invalid metadata length.");
        using var document = JsonDocument.Parse(frame.Slice(8, metadataLength));
        var metadata = document.RootElement;
        Require(metadata.GetProperty("version").GetInt32() == 1, "Unexpected HWT1 version.");
        var revision = metadata.GetProperty("revision").GetUInt32();
        var full = metadata.GetProperty("full").GetBoolean();
        Require(revision == _revision + 1, "A frame was skipped or reordered.");
        Require(_revision != 0 || full, "The first frame must be a full baseline.");
        Require(metadata.GetProperty("baseRevision").GetUInt32() == (full ? 0 : _revision),
            "The delta does not refer to the previous frame.");

        var columns = metadata.GetProperty("columns").GetInt32();
        var rows = metadata.GetProperty("rows").GetInt32();
        Require(columns is > 0 and <= 1024 && rows is > 0 and <= 512 && columns * rows <= 262144,
            "Invalid grid dimensions.");
        if (full)
        {
            _columns = columns;
            _rows = rows;
            _cells = new string[columns * rows];
            _images.Clear();
        }
        Require(columns == _columns && rows == _rows, "A delta changed the grid dimensions.");

        var offset = 8 + metadataLength;
        var changedCount = BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]);
        offset += 4;
        Require(changedCount >= 0 && changedCount <= _cells.Length, "Invalid changed cell count.");
        Require(!full || changedCount == _cells.Length, "Incomplete full baseline.");
        var changed = new HashSet<int>();
        for (var i = 0; i < changedCount; i++)
        {
            Require(bytes.Length - offset >= 22, "Truncated cell.");
            var index = BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]);
            var textLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(offset + 20)..]);
            offset += 22;
            Require(index >= 0 && index < _cells.Length && changed.Add(index), "Invalid cell index.");
            Require(bytes.Length - offset >= textLength, "Truncated cell text.");
            _cells[index] = new UTF8Encoding(false, true).GetString(bytes.Slice(offset, textLength));
            offset += textLength;
        }

        var retained = metadata.GetProperty("retainedImages").EnumerateArray()
            .Select(image => image.GetString()!).ToHashSet(StringComparer.Ordinal);
        foreach (var key in _images.Keys.Where(key => !retained.Contains(key)).ToArray())
            _images.Remove(key);
        foreach (var image in metadata.GetProperty("images").EnumerateArray())
        {
            var key = image.GetProperty("key").GetString()!;
            var width = image.GetProperty("width").GetInt32();
            var height = image.GetProperty("height").GetInt32();
            var length = image.GetProperty("byteLength").GetInt32();
            Require(width is > 0 and <= 4096 && height is > 0 and <= 4096,
                "Invalid image dimensions.");
            Require(length > 0 && length <= bytes.Length - offset, "Truncated image payload.");
            Require(image.GetProperty("format").GetString() == "rgba" && (long)width * height * 4 == length,
                "This sample expects complete RGBA image payloads.");
            Require(retained.Contains(key), "Image payload is not retained.");
            _images[key] = (width, height);
            ImagePayloads++;
            ImageBytes += length;
            offset += length;
        }
        Require(offset == bytes.Length, "Trailing bytes in the frame.");
        Require(retained.All(_images.ContainsKey), "A retained image has no recorded payload.");
        Require(metadata.GetProperty("warnings").GetArrayLength() == 0, "Projection reported warnings.");

        var kinds = new HashSet<string>();
        foreach (var placement in metadata.GetProperty("placements").EnumerateArray())
        {
            var key = placement.GetProperty("key").GetString()!;
            Require(_images.TryGetValue(key, out var image), "Placement references an unrecorded image.");
            var kind = placement.GetProperty("kind").GetString()!;
            Require(kind is "kgp" or "sixel", "Unexpected graphics kind.");
            if (image.Width >= 100 && image.Height >= 60 &&
                placement.GetProperty("clipWidth").GetDouble() >= 100 &&
                placement.GetProperty("clipHeight").GetDouble() >= 60)
                kinds.Add(kind);
            _observedKinds.Add(kind);
        }
        HasBothGraphics = kinds.Contains("kgp") && kinds.Contains("sixel");
        Text = string.Join('\n', Enumerable.Range(0, _rows)
            .Select(row => string.Concat(_cells.AsSpan(row * _columns, _columns).ToArray())));
        _revision = revision;
        return revision;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
