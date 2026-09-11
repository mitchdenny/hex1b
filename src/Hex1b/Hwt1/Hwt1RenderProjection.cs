using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hex1b.Automation;
using Hex1b.Theming;

namespace Hex1b;

internal sealed class Hwt1RenderProjection
{
    internal const uint Magic = 0x31545748;
    internal const int MaxImageCount = 4096;
    private const long ImageBudget = 64 * 1024 * 1024;
    private readonly Dictionary<string, (Hwt1RenderImage Image, uint LastUsed)> _images = [];
    private readonly ConditionalWeakTable<byte[], ImageIdentity> _identities = new();
    private Hwt1RenderCell[] _previous = [];
    private int _columns;
    private int _rows;
    public uint Revision { get; private set; }

    public byte[] Encode(Hex1bTerminalSnapshot snapshot, TerminalCapabilities capabilities,
        long workloadBytes, long outputBatches, double elapsedMs, bool forceFull = false,
        double snapshotMs = 0, Hwt1Peer? peer = null, Hwt1History? history = null)
    {
        if (snapshot.Width is < 1 or > 1024 || snapshot.Height is < 1 or > 512 ||
            (long)snapshot.Width * snapshot.Height > 262144)
            throw new InvalidDataException("The authoritative grid exceeds the HWT1 receiver limits.");

        var started = Stopwatch.GetTimestamp();
        var full = forceFull || _previous.Length == 0 || _columns != snapshot.Width || _rows != snapshot.Height;
        var plannedImages = PreflightImages(snapshot, out var sixelKeys);
        if (full)
            _images.Clear();
        TrimImages(plannedImages);
        var baseRevision = full ? 0 : Revision;
        Revision = checked(Revision + 1);
        _columns = snapshot.Width;
        _rows = snapshot.Height;
        var cells = new Hwt1RenderCell[checked(_columns * _rows)];
        var changed = new List<int>();
        var hyperlinks = new List<Hwt1Hyperlink>();
        for (var y = 0; y < _rows; y++)
        {
            string? linkUri = null;
            var linkStart = 0;
            for (var x = 0; x < _columns; x++)
            {
                var index = y * _columns + x;
                cells[index] = ProjectCell(snapshot, x, y, capabilities);
                if (full || cells[index] != _previous[index])
                    changed.Add(index);
                var source = snapshot.GetCell(x, y);
                var uri = source.IsHidden ? null : source.HyperlinkData?.Uri;
                if (linkUri != uri)
                {
                    if (!string.IsNullOrEmpty(linkUri))
                        hyperlinks.Add(new(y, linkStart, x, linkUri));
                    linkUri = uri;
                    linkStart = x;
                }
            }
            if (!string.IsNullOrEmpty(linkUri))
                hyperlinks.Add(new(y, linkStart, _columns, linkUri));
        }
        _previous = cells;

        var newImages = new List<Hwt1RenderImage>();
        var active = new HashSet<string>(StringComparer.Ordinal);
        var placements = new List<Hwt1RenderPlacement>();
        var warnings = new List<string>();
        var cw = snapshot.CellPixelWidth;
        var ch = snapshot.CellPixelHeight;

        foreach (var p in snapshot.KgpPlacements.OrderBy(p => p.ZIndex)
                     .ThenBy(p => p.ImageId).ThenBy(p => p.GraphId))
        {
            if (!snapshot.KgpImages.TryGetValue(p.ImageId, out var image))
                throw new InvalidDataException("Snapshot placement references a missing KGP image.");
            var resource = ProjectKgpImage(image);
            Retain(resource, active, newImages, full);
            var sx = (double)p.SourceX;
            var sy = (double)p.SourceY;
            var sw = p.SourceWidth == 0 ? resource.Width - sx : Math.Min(p.SourceWidth, resource.Width - sx);
            var sh = p.SourceHeight == 0 ? resource.Height - sy : Math.Min(p.SourceHeight, resource.Height - sy);
            if (sw <= 0 || sh <= 0)
            {
                warnings.Add($"KGP image {p.ImageId} has an empty source crop.");
                continue;
            }

            double x, y, width, height, clipX, clipY, clipWidth, clipHeight;
            if (p.RenderGeometry is { } geometry)
            {
                x = (p.Column + geometry.ImageOffsetXInCells) * cw;
                y = (p.Row + geometry.ImageOffsetYInCells) * ch;
                width = geometry.ImageWidthInCells * cw;
                height = geometry.ImageHeightInCells * ch;
                clipX = (p.Column + geometry.ClipOffsetXInCells) * cw;
                clipY = (p.Row + geometry.ClipOffsetYInCells) * ch;
                clipWidth = geometry.ClipWidthInCells * cw;
                clipHeight = geometry.ClipHeightInCells * ch;
                sx = sy = 0;
                sw = resource.Width;
                sh = resource.Height;
            }
            else
            {
                x = p.Column * cw + p.CellOffsetX;
                y = p.Row * ch + p.CellOffsetY;
                width = p.UsesNativeSize || p.DisplayColumns == 0 ? sw : p.DisplayColumns * cw - p.CellOffsetX;
                height = p.UsesNativeSize || p.DisplayRows == 0 ? sh : p.DisplayRows * ch - p.CellOffsetY;
                clipX = x;
                clipY = y;
                clipWidth = width;
                clipHeight = height;
            }
            placements.Add(new(resource.Key, "kgp", x, y, width, height,
                sx, sy, sw, sh, clipX, clipY, clipWidth, clipHeight, p.ZIndex));
        }

        foreach (var p in snapshot.SixelPlacements.OrderBy(p => p.Sequence))
        {
            if (!p.HasVisiblePaintedCells)
                continue;
            if (!sixelKeys.TryGetValue(p, out var key))
            {
                warnings.Add($"Sixel at {p.Column},{p.Row}: raster unavailable ({p.Image.RasterStatus}).");
                continue;
            }

            // Crop/damage belongs to a placement, not to the shared Sixel resource.
            Hwt1RenderImage resource;
            if (_images.TryGetValue(key, out var cached))
            {
                resource = cached.Image;
            }
            else
            {
                var pixels = p.GetPaintedPixels();
                if (pixels is null)
                    throw new InvalidDataException("Sixel raster disappeared from a captured placement.");
                EnsureImageSize(pixels.Width, pixels.Height);
                var rgba = new byte[checked(pixels.Width * pixels.Height * 4)];
                var offset = 0;
                foreach (var pixel in pixels.AsSpan())
                {
                    rgba[offset++] = pixel.R;
                    rgba[offset++] = pixel.G;
                    rgba[offset++] = pixel.B;
                    rgba[offset++] = pixel.A;
                }
                resource = new(key, pixels.Width, pixels.Height, "rgba", rgba);
            }
            Retain(resource, active, newImages, full);
            var px = p.PaintedLeft * cw;
            var py = p.PaintedTop * ch;
            var width = resource.Width * cw / p.Image.CellMetrics.SafeWidth;
            var height = resource.Height * ch / p.Image.CellMetrics.SafeHeight;
            placements.Add(new(resource.Key, "sixel", px, py, width, height,
                0, 0, resource.Width, resource.Height, px, py, width, height, -1));
        }

        var metadata = JsonSerializer.SerializeToUtf8Bytes(new Hwt1FrameMetadata(
            1, Revision, baseRevision, full, _columns, _rows, cw, ch,
            Hwt1Input.MouseTracking(snapshot),
            Pack(null, capabilities.DefaultBackground), Pack(null, capabilities.DefaultForeground),
            new(snapshot.CursorX, snapshot.CursorY, snapshot.CursorVisible, (int)snapshot.CursorShape),
            newImages, _images.Keys.ToArray(), placements,
            new(workloadBytes, outputBatches, elapsedMs,
                snapshotMs + Stopwatch.GetElapsedTime(started).TotalMilliseconds),
            warnings, peer ?? Hwt1Peer.Standalone, history, hyperlinks, snapshot.WindowTitle,
            Hwt1Progress.From(snapshot.Progress), Hwt1ShellIntegration.From(snapshot.ShellIntegration)),
            Hwt1JsonSerializerContext.Default.Hwt1FrameMetadata);
        if (metadata.Length > 8 * 1024 * 1024)
            throw new InvalidDataException("Frame metadata exceeds the HWT1 8 MiB limit.");

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(metadata.Length);
        writer.Write(metadata);
        writer.Write(changed.Count);
        foreach (var index in changed)
        {
            var cell = cells[index];
            var text = Encoding.UTF8.GetBytes(cell.Text);
            if (text.Length > ushort.MaxValue)
                throw new InvalidDataException("A grapheme exceeds the HWT1 text limit.");
            writer.Write(index);
            writer.Write(cell.Foreground);
            writer.Write(cell.Background);
            writer.Write(cell.UnderlineColor);
            writer.Write(cell.Attributes);
            writer.Write(cell.Width);
            writer.Write(cell.UnderlineStyle);
            writer.Write((ushort)text.Length);
            writer.Write(text);
        }
        foreach (var image in newImages)
            writer.Write(image.Bytes);
        return stream.ToArray();
    }

    private static Hwt1RenderCell ProjectCell(Hex1bTerminalSnapshot snapshot, int x, int y, TerminalCapabilities capabilities)
    {
        var cell = snapshot.GetCell(x, y);
        var text = cell.Character ?? " ";
        var width = text.Length == 0 ? 0 : Math.Max(1, DisplayWidth.GetGraphemeWidth(text));
        if (width > 1)
        {
            var owned = 1;
            while (owned < width && x + owned < snapshot.Width &&
                   snapshot.GetCell(x + owned, y) is { Character: "" } continuation &&
                   continuation.Sequence == cell.Sequence)
                owned++;
            // Width carries owned cells; glyph rasterization must clip, not reflow.
            width = owned;
        }
        if (text == "\0" || text == "\uE000")
            text = " ";
        var fg = Pack(cell.Foreground, capabilities.DefaultForeground);
        var bg = Pack(cell.Background, capabilities.DefaultBackground);
        if (cell.IsReverse)
            (fg, bg) = (bg, fg);
        if (cell.IsDim)
            fg = ((fg & 0xff) / 2) | ((((fg >> 8) & 0xff) / 2) << 8) |
                 ((((fg >> 16) & 0xff) / 2) << 16) | 0xff000000;
        if (!cell.IsReverse && (cell.Background is null || cell.Background.Value.IsDefault))
            bg &= 0x00ffffff;
        return new(text, fg, bg, cell.UnderlineColor is { IsDefault: false } ul ? Pack(ul, 0) : fg,
            (ushort)cell.Attributes, checked((byte)width), (byte)cell.UnderlineStyle);
    }

    private static uint Pack(Hex1bColor? color, int fallback)
        => color is { IsDefault: false } c
            ? (uint)(c.R | c.G << 8 | c.B << 16) | 0xff000000
            : (uint)(((fallback >> 16) & 0xff) | (fallback & 0xff00) | ((fallback & 0xff) << 16)) | 0xff000000;

    private Hwt1RenderImage ProjectKgpImage(KgpImageData image, bool dimensionsOnly = false)
    {
        var data = image.CurrentFrameData;
        var identity = _identities.GetValue(data, bytes => new(Convert.ToHexString(SHA256.HashData(bytes))));
        var format = image.CurrentFrameFormat;
        var width = checked((int)image.Width);
        var height = checked((int)image.Height);
        if (format == KgpFormat.Png && (width == 0 || height == 0))
        {
            if (data.Length < 24 || !data.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                throw new InvalidDataException("KGP PNG does not contain an IHDR.");
            width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16, 4));
            height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20, 4));
        }
        EnsureImageSize(width, height);
        var key = $"k:{identity.Hash}:{width}:{height}:{format}";
        if (dimensionsOnly)
            return new(key, width, height, format == KgpFormat.Png ? "png" : "rgba", []);
        if (_images.TryGetValue(key, out var cached))
            return cached.Image;
        if (format == KgpFormat.Png)
            return new(key, width, height, "png", data);
        var pixelCount = checked(width * height);
        if (format == KgpFormat.Rgb24)
        {
            if (data.Length != pixelCount * 3)
                throw new InvalidDataException("Invalid KGP RGB data length.");
            var rgba = new byte[pixelCount * 4];
            for (var i = 0; i < pixelCount; i++)
            {
                rgba[i * 4] = data[i * 3];
                rgba[i * 4 + 1] = data[i * 3 + 1];
                rgba[i * 4 + 2] = data[i * 3 + 2];
                rgba[i * 4 + 3] = 255;
            }
            return new(key, width, height, "rgba", rgba);
        }
        if (data.Length != pixelCount * 4)
            throw new InvalidDataException("Invalid KGP RGBA data length.");
        return new(key, width, height, "rgba", data);
    }

    private static void EnsureImageSize(int width, int height)
    {
        if (width <= 0 || height <= 0 || width > 4096 || height > 4096 || (long)width * height * 4 > 32 * 1024 * 1024)
            throw new InvalidDataException("Image exceeds the HWT1 limit (4096 per axis, 32 MiB decoded).");
    }

    private void Retain(Hwt1RenderImage resource, HashSet<string> active, List<Hwt1RenderImage> added, bool full)
    {
        if (!active.Add(resource.Key))
            return;
        if (full || !_images.ContainsKey(resource.Key))
            added.Add(resource);
        _images[resource.Key] = (resource, Revision);
    }

    private Dictionary<string, long> PreflightImages(
        Hex1bTerminalSnapshot snapshot, out Dictionary<SixelPlacement, string> sixelKeys)
    {
        var planned = new Dictionary<string, long>(StringComparer.Ordinal);
        sixelKeys = [];
        long bytes = 0;
        foreach (var placement in snapshot.KgpPlacements)
        {
            if (!snapshot.KgpImages.TryGetValue(placement.ImageId, out var image))
                throw new InvalidDataException("Snapshot placement references a missing KGP image.");
            var resource = ProjectKgpImage(image, dimensionsOnly: true);
            Reserve(resource.Key, resource.Width, resource.Height);
        }
        foreach (var placement in snapshot.SixelPlacements)
        {
            if (!placement.HasVisiblePaintedCells)
                continue;
            if (!placement.TryGetPaintedPixelDimensions(out var width, out var height))
            {
                if (placement.IsGeometryOnly)
                    continue;
                throw new InvalidDataException("Sixel raster disappeared from a captured placement.");
            }
            var key = SixelImageKey(placement);
            Reserve(key, width, height);
            sixelKeys[placement] = key;
        }
        return planned;

        void Reserve(string key, int width, int height)
        {
            EnsureImageSize(width, height);
            var size = (long)width * height * 4;
            if (!planned.TryAdd(key, size))
                return;
            bytes += size;
            if (bytes > ImageBudget)
                throw new InvalidDataException("Visible graphics exceed the HWT1 64 MiB decoded-image budget.");
            if (planned.Count > MaxImageCount)
                throw new InvalidDataException($"Visible graphics exceed the HWT1 {MaxImageCount}-image resource limit.");
        }
    }

    private static string SixelImageKey(SixelPlacement placement)
    {
        var identity = new StringBuilder(Convert.ToHexString(placement.Image.ContentHash))
            .Append(':').Append(placement.PaintedColumnOffset).Append(':').Append(placement.PaintedRowOffset)
            .Append(':').Append(placement.PaintedColumnCount).Append(':').Append(placement.PaintedRowCount);
        for (var y = placement.PaintedTop; y <= placement.PaintedBottom; y++)
            for (var x = placement.PaintedLeft; x <= placement.PaintedRight; x++)
                if (placement.IsCellDamaged(y, x))
                    identity.Append(':').Append(x - placement.Column).Append(',').Append(y - placement.Row);
        return "s:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToString())));
    }

    private void TrimImages(Dictionary<string, long> planned)
    {
        // Reserve the complete next frame and evict inactive entries before allocating replacements.
        var inactive = _images.Where(p => !planned.ContainsKey(p.Key)).OrderBy(p => p.Value.LastUsed).ToArray();
        var bytes = planned.Values.Sum() + inactive.Sum(p => (long)p.Value.Image.Width * p.Value.Image.Height * 4);
        var count = planned.Count + inactive.Length;
        foreach (var entry in inactive)
        {
            if (bytes <= ImageBudget && count <= MaxImageCount)
                break;
            bytes -= (long)entry.Value.Image.Width * entry.Value.Image.Height * 4;
            count--;
            _images.Remove(entry.Key);
        }
    }

    private sealed record ImageIdentity(string Hash);
}
