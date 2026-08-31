using System.Globalization;
using System.Text;

namespace Hex1b;

internal static class Hmp1KgpStateReplay
{
    private const int MaximumBase64ChunkLength = 4096;
    private const int MaximumRawChunkLength = MaximumBase64ChunkLength / 4 * 3;
    private const int TargetFrameSize = 1024 * 1024;

    internal static async Task WriteAsync(
        Stream stream,
        IReadOnlyList<KgpPlacement> placements,
        IReadOnlyDictionary<uint, KgpImageData> images,
        int cursorX,
        int cursorY,
        CancellationToken ct)
    {
        if (placements.Count == 0 || images.Count == 0)
            return;

        var frame = new StringBuilder(TargetFrameSize);

        foreach (var image in images.Values.OrderBy(image => image.ImageId))
        {
            await AppendImageAsync(image).ConfigureAwait(false);
        }

        var placementIdentityCounts = placements
            .Where(placement => placement.PlacementId > 0)
            .GroupBy(placement => (placement.ImageId, placement.PlacementId))
            .ToDictionary(group => group.Key, group => group.Count());

        foreach (var placement in placements)
        {
            if (!images.TryGetValue(placement.ImageId, out var image))
                continue;

            var preservePlacementId =
                placement.PlacementId > 0 &&
                placementIdentityCounts[(placement.ImageId, placement.PlacementId)] == 1;
            await AppendAsync(BuildPlacementSequence(
                placement,
                image,
                preservePlacementId)).ConfigureAwait(false);
        }

        await AppendAsync(FormattableString.Invariant(
            $"\x1b[{cursorY + 1};{cursorX + 1}H")).ConfigureAwait(false);
        await FlushAsync().ConfigureAwait(false);

        async ValueTask AppendImageAsync(KgpImageData image)
        {
            var data = image.CurrentFrameData;
            var offset = 0;
            var first = true;
            do
            {
                var count = Math.Min(MaximumRawChunkLength, data.Length - offset);
                var isLast = offset + count >= data.Length;
                var payload = count == 0
                    ? string.Empty
                    : Convert.ToBase64String(data, offset, count);
                string controls;
                if (first)
                {
                    controls = FormattableString.Invariant(
                        $"a=t,f={(int)image.CurrentFrameFormat},s={image.Width},v={image.Height},{BuildImageIdentity(image)},t=d,q=2");
                    if (!isLast)
                        controls += ",m=1";
                }
                else
                {
                    controls = isLast ? "m=0,q=2" : "m=1,q=2";
                }

                await AppendAsync(BuildKgpSequence(controls, payload)).ConfigureAwait(false);
                offset += count;
                first = false;
            }
            while (offset < data.Length);
        }

        async ValueTask AppendAsync(string sequence)
        {
            if (frame.Length > 0 && frame.Length + sequence.Length > TargetFrameSize)
                await FlushAsync().ConfigureAwait(false);
            frame.Append(sequence);
        }

        async ValueTask FlushAsync()
        {
            if (frame.Length == 0)
                return;

            var payload = Encoding.UTF8.GetBytes(frame.ToString());
            frame.Clear();
            await Hmp1Protocol.WriteFrameAsync(
                stream,
                Hmp1FrameType.Output,
                payload,
                ct).ConfigureAwait(false);
        }
    }

    private static string BuildPlacementSequence(
        KgpPlacement placement,
        KgpImageData image,
        bool preservePlacementId)
    {
        var controls = new StringBuilder();
        controls.Append("a=p,");
        controls.Append(BuildImageIdentity(image));
        if (preservePlacementId)
        {
            controls.Append(",p=");
            controls.Append(placement.PlacementId.ToString(CultureInfo.InvariantCulture));
        }

        AppendNonZero(controls, 'x', placement.SourceX);
        AppendNonZero(controls, 'y', placement.SourceY);
        AppendNonZero(controls, 'w', placement.SourceWidth);
        AppendNonZero(controls, 'h', placement.SourceHeight);
        AppendNonZero(controls, 'X', placement.CellOffsetX);
        AppendNonZero(controls, 'Y', placement.CellOffsetY);
        AppendNonZero(controls, 'c', placement.DisplayColumns);
        AppendNonZero(controls, 'r', placement.DisplayRows);
        if (placement.ZIndex != 0)
        {
            controls.Append(",z=");
            controls.Append(placement.ZIndex.ToString(CultureInfo.InvariantCulture));
        }
        controls.Append(",C=1,q=2");

        return FormattableString.Invariant(
            $"\x1b[{placement.Row + 1};{placement.Column + 1}H") +
            BuildKgpSequence(controls.ToString(), string.Empty);
    }

    private static string BuildImageIdentity(KgpImageData image)
        => image.ImageNumber > 0
            ? FormattableString.Invariant($"I={image.ImageNumber}")
            : FormattableString.Invariant($"i={image.ImageId}");

    private static string BuildKgpSequence(string controls, string payload)
        => payload.Length == 0
            ? $"\x1b_G{controls}\x1b\\"
            : $"\x1b_G{controls};{payload}\x1b\\";

    private static void AppendNonZero(StringBuilder builder, char key, uint value)
    {
        if (value == 0)
            return;

        builder.Append(',');
        builder.Append(key);
        builder.Append('=');
        builder.Append(value.ToString(CultureInfo.InvariantCulture));
    }
}
