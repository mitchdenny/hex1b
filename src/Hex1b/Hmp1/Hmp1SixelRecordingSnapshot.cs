using System.Text;
using Hex1b.Sixel;
using Hex1b.Tokens;

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
    /// Replays every placement into a fresh terminal in
    /// <see cref="Hmp1SixelRecordedPlacement.Sequence"/> order. Version 2
    /// recordings apply each image's captured protocol metrics before feeding
    /// its cursor-position and Sixel DCS through the ordinary tokenizer/apply
    /// path. Version 1 recordings retain their legacy behavior and use the
    /// target terminal's current metrics because that format did not persist
    /// per-image metric context.
    /// </summary>
    /// <param name="terminal">The fresh terminal that receives the recording.</param>
    /// <param name="cancellationToken">Stops validation or construction before completion.</param>
    /// <exception cref="Hmp1SixelRecordingException">
    /// The expanded replay would exceed the internal aggregate replay limit,
    /// or the target cannot reproduce the recorded placement geometry.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled.
    /// </exception>
    public void ReplayInto(
        Hex1bTerminal terminal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(terminal);

        var orderedPlacements = Placements.OrderBy(p => p.Sequence).ToArray();
        long totalBytes = 0;
        foreach (var placement in orderedPlacements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var image = Images[placement.ImageIndex];
            var cursor = FormattableString.Invariant(
                $"\x1b[{placement.Row + 1};{placement.Column + 1}H");
            var payloadBytes = Encoding.UTF8.GetByteCount(image.Payload);
            if (image.IsGeometryOnly && !Hmp1SixelStateReplay.HasDcsFraming(image.Payload))
                payloadBytes = checked(payloadBytes + 4);

            totalBytes = checked(totalBytes + Encoding.UTF8.GetByteCount(cursor) + payloadBytes);
            if (totalBytes > Hmp1SixelLimits.MaximumTotalPayloadBytes)
            {
                throw new Hmp1SixelRecordingException(
                    Hmp1SixelRecordingFailureReason.ResourceLimitExceeded,
                    $"Expanded replay payload exceeds the limit of {Hmp1SixelLimits.MaximumTotalPayloadBytes} bytes.");
            }
        }

        var originalMetricsOverride = terminal.SixelCellMetricsOverride;
        try
        {
            foreach (var placement in orderedPlacements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var image = Images[placement.ImageIndex];
                terminal.SetSixelCellMetrics(image.CellMetrics ?? originalMetricsOverride);

                var placementCountBefore = terminal.SixelPlacementCount;
                var sequence = FormattableString.Invariant(
                    $"\x1b[{placement.Row + 1};{placement.Column + 1}H") +
                    (image.IsGeometryOnly
                        ? Hmp1SixelStateReplay.FramePayload(image.Payload)
                        : image.Payload);
                terminal.ApplyTokens(AnsiTokenizer.Tokenize(sequence));

                if (terminal.SixelPlacementCount != placementCountBefore + 1)
                {
                    throw new Hmp1SixelRecordingException(
                        Hmp1SixelRecordingFailureReason.InvalidGeometry,
                        "Replay did not create exactly one active Sixel placement. The target must be a fresh terminal large enough for the recorded viewport geometry.");
                }

                var replayed = terminal.SixelPlacements[^1];
                ValidateReplayedPlacement(image, placement, replayed);
                try
                {
                    replayed.RestoreVisibleState(
                        placement.PaintedRowOffset,
                        placement.PaintedRowCount,
                        placement.PaintedColumnOffset,
                        placement.PaintedColumnCount,
                        placement.DamagedCells);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    throw new Hmp1SixelRecordingException(
                        Hmp1SixelRecordingFailureReason.InvalidGeometry,
                        ex.Message);
                }
            }
        }
        finally
        {
            terminal.SetSixelCellMetrics(originalMetricsOverride);
        }
    }

    private static void ValidateReplayedPlacement(
        Hmp1SixelRecordedImage image,
        Hmp1SixelRecordedPlacement placement,
        SixelPlacement replayed)
    {
        if (replayed.Row != placement.Row ||
            replayed.Column != placement.Column ||
            replayed.WidthInCells != placement.WidthInCells ||
            replayed.HeightInCells != placement.HeightInCells)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.InvalidGeometry,
                "The target terminal could not reproduce the recorded placement geometry.");
        }

        if (image.CellMetrics is { } recordedMetrics &&
            !MetricsEqual(recordedMetrics, replayed.Image.CellMetrics))
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.InvalidGeometry,
                "The target terminal did not preserve the recording's protocol cell metrics.");
        }
    }

    private static bool MetricsEqual(SixelCellMetrics left, SixelCellMetrics right) =>
        BitConverter.DoubleToInt64Bits(left.Width) == BitConverter.DoubleToInt64Bits(right.Width) &&
        BitConverter.DoubleToInt64Bits(left.Height) == BitConverter.DoubleToInt64Bits(right.Height) &&
        left.Source == right.Source &&
        left.Reliability == right.Reliability;
}
