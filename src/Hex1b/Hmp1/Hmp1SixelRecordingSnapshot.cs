using System.Text;

namespace Hex1b;

/// <summary>
/// A decoded, versioned Sixel state recording: sufficient to reconstruct image
/// definitions, placements, damage, source crops, geometry-only outcomes, and
/// metrics without a live upstream terminal.
/// </summary>
internal sealed class Hmp1SixelRecordingSnapshot(
    int version,
    IReadOnlyList<Hmp1SixelRecordedImage> images,
    IReadOnlyList<Hmp1SixelRecordedPlacement> placements)
{
    /// <summary>The format version this recording was written with.</summary>
    public int Version { get; } = version;

    /// <summary>The recording's distinct image table, ordered by first appearance.</summary>
    public IReadOnlyList<Hmp1SixelRecordedImage> Images { get; } = images;

    /// <summary>The recording's placements, in their original creation order.</summary>
    public IReadOnlyList<Hmp1SixelRecordedPlacement> Placements { get; } = placements;

    /// <summary>
    /// Builds the cursor-position + Sixel DCS escape sequence text needed to
    /// reconstruct every placement in this recording on a fresh terminal, in
    /// <see cref="Hmp1SixelRecordedPlacement.Sequence"/> order. Feeding the result
    /// through the same tokenizer/apply path a live terminal uses for incoming
    /// output (rather than a bespoke reconstruction) is what lets replay be
    /// verified against the same authoritative parser/raster invariants used by
    /// live terminal processing.
    /// </summary>
    public string BuildReplayEscapeSequence()
    {
        var sb = new StringBuilder();
        foreach (var placement in Placements.OrderBy(p => p.Sequence))
        {
            var image = Images[placement.ImageIndex];
            sb.Append(FormattableString.Invariant($"\x1b[{placement.Row + 1};{placement.Column + 1}H"));
            sb.Append(image.Payload);
        }

        return sb.ToString();
    }
}
