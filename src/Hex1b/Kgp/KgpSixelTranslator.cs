using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Hex1b;
using Hex1b.Sixel;
using Hex1b.Surfaces;

namespace Hex1b.Kgp;

/// <summary>
/// Translates the authoritative Sixel raster state observed via
/// <see cref="Sixel.SixelRasterRouter"/> into outbound Kitty Graphics Protocol (KGP)
/// wire bytes, for presentations that understand KGP but not Sixel.
/// </summary>
/// <remarks>
/// <para>
/// This is a wire-level emission concern only: translated content is never stored
/// into <see cref="Hex1bTerminal"/>'s own authoritative KGP graphics state
/// (<see cref="KgpTerminalGraphicsState"/>), so existing KGP-native handling and
/// terminal-state tests are unaffected regardless of whether translation is active.
/// Hex1b's authoritative model of what is on screen remains solely the Sixel model;
/// this type only decides what bytes, if any, to additionally send to the
/// presentation so a KGP-capable-but-not-Sixel-capable client can render it.
/// </para>
/// <para>
/// Image and placement IDs are allocated with the high bit
/// (<c>0x8000_0000</c>) always set, reserving a namespace that a workload
/// authoring its own KGP sequences directly (typically small, low-valued IDs) is
/// exceedingly unlikely to also choose — see
/// <see href="https://github.com/mitchdenny/hex1b/issues/458">#458</see>. This is a
/// documented, testable convention (not a cryptographic guarantee), consistent with
/// the existing content-hash-derived allocation <see cref="Hex1bRenderContext"/>
/// already uses for outbound widget/surface KGP images.
/// </para>
/// <para>
/// Content identity for translation purposes is derived from each placement's
/// currently <em>painted</em> pixels (<see cref="Hex1b.SixelPlacement.GetPaintedPixels"/>),
/// not <see cref="Hex1b.SixelData.ContentHash"/>: two placements sharing the same
/// underlying Sixel image can diverge once one of them is destructively damaged, so
/// painted-pixel identity is what actually determines whether unchanged content can
/// be skipped and whether a placement's pixels need retransmission.
/// </para>
/// </remarks>
internal sealed class KgpSixelTranslator
{
    private const uint ReservedIdBit = 0x8000_0000;

    private sealed record TrackedPlacement(uint ImageId, uint PlacementId, byte[] PaintedHash);

    private readonly Dictionary<long, TrackedPlacement> _tracked = [];
    private readonly Dictionary<byte[], int> _imageRefCounts = new(SixelContentHashComparer.Instance);
    private readonly Dictionary<byte[], uint> _imageIdByPaintedHash = new(SixelContentHashComparer.Instance);
    private uint _nextPlacementId = ReservedIdBit;

    /// <summary>
    /// Resets all translation bookkeeping, without emitting any wire bytes. Used
    /// when translation is (re)attached fresh, so historical placement metrics are
    /// never rewritten but bookkeeping starts clean.
    /// </summary>
    internal void ResetBookkeeping()
    {
        _tracked.Clear();
        _imageRefCounts.Clear();
        _imageIdByPaintedHash.Clear();
        _nextPlacementId = ReservedIdBit;
    }

    /// <summary>
    /// Applies a batch of raster events, deriving and writing the minimal KGP wire
    /// bytes needed to keep a KGP-capable presentation's view consistent with the
    /// authoritative Sixel state.
    /// </summary>
    /// <param name="events">The ordered raster events for this batch.</param>
    /// <param name="currentPlacements">
    /// The active screen's current live placements, keyed by <see cref="Hex1b.SixelPlacement.Sequence"/>,
    /// used to resolve the placement referenced by a damage or update event.
    /// </param>
    /// <param name="presentation">The presentation to write translated KGP bytes to.</param>
    /// <param name="cursorRow">
    /// The terminal's own authoritative cursor row, restored after this batch's
    /// translated placements (each of which repositions the cursor to its target
    /// cell, since KGP paints at the current cursor position) so ordinary text
    /// output is never displaced by a translated graphic.
    /// </param>
    /// <param name="cursorColumn">The terminal's own authoritative cursor column.</param>
    /// <param name="ct">Cancellation token.</param>
    internal async ValueTask ApplyAsync(
        IReadOnlyList<SixelRasterEvent> events,
        IReadOnlyDictionary<long, SixelPlacement> currentPlacements,
        IHex1bTerminalPresentationAdapter presentation,
        int cursorRow,
        int cursorColumn,
        CancellationToken ct)
    {
        var movedCursor = false;

        foreach (var evt in events)
        {
            switch (evt)
            {
                case SixelRasterPlacementUpdated updated:
                    movedCursor |= await SyncPlacementAsync(updated.Placement, presentation, ct).ConfigureAwait(false);
                    break;

                case SixelRasterPlacementDamaged damaged when currentPlacements.TryGetValue(damaged.PlacementSequence, out var damagedPlacement):
                    movedCursor |= await SyncPlacementAsync(damagedPlacement, presentation, ct).ConfigureAwait(false);
                    break;

                case SixelRasterPlacementReleased released:
                    await ReleasePlacementAsync(released.PlacementSequence, presentation, ct).ConfigureAwait(false);
                    break;

                case SixelRasterReset:
                    await ReleaseAllAsync(presentation, ct).ConfigureAwait(false);
                    break;

                // SixelRasterScreenTransition's individual SixelRasterPlacementReleased
                // events (emitted by SixelRasterRouter for everything visible on the
                // screen being left) already drive release through the case above; the
                // screen being entered is re-announced via ordinary
                // SixelRasterPlacementUpdated(isNew: true) events. SixelRasterContentDefined
                // / SixelRasterContentReleased are sink-oriented dedup signals keyed by
                // original Sixel content identity and are intentionally not used here (see
                // remarks on painted-pixel identity). SixelRasterRouteDiagnostic is surfaced
                // separately via Hex1bTerminal.SixelRouteDiagnosticRaised.
            }
        }

        if (movedCursor)
        {
            // Placement commands above each repositioned the cursor to their own
            // target cell (KGP paints at the current cursor position); restore the
            // terminal's own authoritative cursor position once, at the end of the
            // batch, exactly as Hmp1KgpStateReplay does for the same reason.
            await presentation.WriteOutputAsync(
                Encoding.ASCII.GetBytes(FormattableString.Invariant($"\x1b[{cursorRow + 1};{cursorColumn + 1}H")),
                ct).ConfigureAwait(false);
        }
    }

    private async ValueTask<bool> SyncPlacementAsync(
        SixelPlacement placement,
        IHex1bTerminalPresentationAdapter presentation,
        CancellationToken ct)
    {
        var pixels = placement.GetPaintedPixels();
        if (pixels is null)
        {
            // Geometry-only or zero-extent: nothing to show under KGP. If a prior,
            // now-stale translation exists for this placement, drop it.
            await ReleasePlacementAsync(placement.Sequence, presentation, ct).ConfigureAwait(false);
            return false;
        }

        var rgba = ToRgbaBytes(pixels);
        var paintedHash = SHA256.HashData(rgba);

        if (_tracked.TryGetValue(placement.Sequence, out var existing))
        {
            if (SixelContentHashComparer.Instance.Equals(existing.PaintedHash, paintedHash))
            {
                // Pixels unchanged; only the placement's cell geometry may have moved
                // (scroll/reflow shift Row/Column in place without changing pixels).
                await WritePlacementAsync(presentation, existing.ImageId, existing.PlacementId, placement, ct).ConfigureAwait(false);
                return true;
            }

            // Painted content changed (typically due to destructive damage): drop the
            // old placement/image reference before adopting new content.
            await ReleasePlacementAsync(placement.Sequence, presentation, ct).ConfigureAwait(false);
        }

        var imageId = await ResolveOrTransmitImageAsync(rgba, pixels.Width, pixels.Height, paintedHash, presentation, ct).ConfigureAwait(false);
        var placementId = _nextPlacementId++;
        if (_nextPlacementId == 0)
        {
            _nextPlacementId = ReservedIdBit;
        }

        _tracked[placement.Sequence] = new TrackedPlacement(imageId, placementId, paintedHash);
        await WritePlacementAsync(presentation, imageId, placementId, placement, ct).ConfigureAwait(false);
        return true;
    }

    private async ValueTask<uint> ResolveOrTransmitImageAsync(
        byte[] rgba,
        int width,
        int height,
        byte[] paintedHash,
        IHex1bTerminalPresentationAdapter presentation,
        CancellationToken ct)
    {
        if (_imageIdByPaintedHash.TryGetValue(paintedHash, out var existingImageId))
        {
            _imageRefCounts[paintedHash] = _imageRefCounts.GetValueOrDefault(paintedHash) + 1;
            return existingImageId;
        }

        var imageId = ComputeDeterministicImageId(paintedHash);
        _imageIdByPaintedHash[paintedHash] = imageId;
        _imageRefCounts[paintedHash] = 1;
        await WriteTransmitAsync(presentation, imageId, rgba, width, height, ct).ConfigureAwait(false);
        return imageId;
    }

    private static uint ComputeDeterministicImageId(byte[] contentHash)
    {
        var baseId = BinaryPrimitives.ReadUInt32BigEndian(contentHash) | ReservedIdBit;
        return baseId == ReservedIdBit ? baseId | 1 : baseId;
    }

    private async ValueTask ReleasePlacementAsync(
        long sequence,
        IHex1bTerminalPresentationAdapter presentation,
        CancellationToken ct)
    {
        if (!_tracked.Remove(sequence, out var tracked))
        {
            return;
        }

        await presentation.WriteOutputAsync(
            Encoding.ASCII.GetBytes($"\x1b_Ga=d,d=i,i={tracked.ImageId},p={tracked.PlacementId},q=2\x1b\\"),
            ct).ConfigureAwait(false);

        var paintedHash = FindPaintedHash(tracked.ImageId);
        if (paintedHash is null)
        {
            return;
        }

        var refCount = _imageRefCounts.GetValueOrDefault(paintedHash) - 1;
        if (refCount <= 0)
        {
            _imageRefCounts.Remove(paintedHash);
            _imageIdByPaintedHash.Remove(paintedHash);
            await presentation.WriteOutputAsync(
                Encoding.ASCII.GetBytes($"\x1b_Ga=d,d=I,i={tracked.ImageId},q=2\x1b\\"),
                ct).ConfigureAwait(false);
        }
        else
        {
            _imageRefCounts[paintedHash] = refCount;
        }
    }

    private byte[]? FindPaintedHash(uint imageId)
    {
        foreach (var (hash, id) in _imageIdByPaintedHash)
        {
            if (id == imageId)
            {
                return hash;
            }
        }

        return null;
    }

    private async ValueTask ReleaseAllAsync(IHex1bTerminalPresentationAdapter presentation, CancellationToken ct)
    {
        foreach (var sequence in _tracked.Keys.ToArray())
        {
            await ReleasePlacementAsync(sequence, presentation, ct).ConfigureAwait(false);
        }
    }

    private static async ValueTask WriteTransmitAsync(
        IHex1bTerminalPresentationAdapter presentation,
        uint imageId,
        byte[] rgba,
        int width,
        int height,
        CancellationToken ct)
    {
        var base64 = Convert.ToBase64String(rgba);
        const int maxChunk = 4096;

        if (base64.Length <= maxChunk)
        {
            await presentation.WriteOutputAsync(
                Encoding.ASCII.GetBytes($"\x1b_Ga=t,f=32,s={width},v={height},i={imageId},t=d,q=2;{base64}\x1b\\"),
                ct).ConfigureAwait(false);
            return;
        }

        var offset = 0;
        var isFirst = true;
        while (offset < base64.Length)
        {
            var remaining = base64.Length - offset;
            var chunkLen = Math.Min(remaining, maxChunk);
            var isLast = offset + chunkLen >= base64.Length;
            var chunk = base64.Substring(offset, chunkLen);

            var sequence = isFirst
                ? $"\x1b_Ga=t,f=32,s={width},v={height},i={imageId},t=d,q=2,m=1;{chunk}\x1b\\"
                : isLast
                    ? $"\x1b_Gm=0;{chunk}\x1b\\"
                    : $"\x1b_Gm=1;{chunk}\x1b\\";

            await presentation.WriteOutputAsync(Encoding.ASCII.GetBytes(sequence), ct).ConfigureAwait(false);
            offset += chunkLen;
            isFirst = false;
        }
    }

    private static async ValueTask WritePlacementAsync(
        IHex1bTerminalPresentationAdapter presentation,
        uint imageId,
        uint placementId,
        SixelPlacement placement,
        CancellationToken ct)
    {
        // KGP placements paint at the current cursor position (there is no absolute
        // row/column addressing key), so — mirroring the established convention
        // Hmp1KgpStateReplay already uses for the same problem — position the cursor
        // at the placement's target cell immediately before the placement command.
        // C=1 additionally tells the terminal not to advance the cursor as a side
        // effect of painting; ApplyAsync restores the terminal's own authoritative
        // cursor position once after all of a batch's placements are written.
        var cup = FormattableString.Invariant($"\x1b[{placement.PaintedTop + 1};{placement.PaintedLeft + 1}H");
        var placementCommand =
            $"\x1b_Ga=p,i={imageId},p={placementId}" +
            $",c={placement.PaintedColumnCount},r={placement.PaintedRowCount}" +
            ",C=1,q=2\x1b\\";

        // Stacking order for overlapping placements is preserved through wire
        // transmission order, not an explicit z value: events are delivered in
        // ascending SixelPlacement.Sequence order (see SixelRasterRouter), matching
        // "later sequence paints on top", and KGP implementations stack same-z
        // placements in the order their placement commands were received.
        await presentation.WriteOutputAsync(Encoding.ASCII.GetBytes(cup + placementCommand), ct).ConfigureAwait(false);
    }

    private static byte[] ToRgbaBytes(SixelPixelBuffer pixels)
    {
        var bytes = new byte[pixels.Width * pixels.Height * 4];
        var offset = 0;
        for (var y = 0; y < pixels.Height; y++)
        {
            for (var x = 0; x < pixels.Width; x++)
            {
                var pixel = pixels[x, y];
                bytes[offset++] = pixel.R;
                bytes[offset++] = pixel.G;
                bytes[offset++] = pixel.B;
                bytes[offset++] = pixel.A;
            }
        }

        return bytes;
    }
}
