// Copyright (c) Hex1b contributors. Licensed under the MIT license.

using System.Text;
using Hex1b.Sixel;
using Hex1b.Tests.Sixel;
using Hex1b.Tokens;

namespace Hex1b.Tests.Hmp1;

/// <summary>
/// Coverage for <see cref="Hmp1SixelRecording"/>: the versioned, explicit
/// binary Sixel state representation used to record/serialize/replay/compare
/// complete Sixel state without a live upstream terminal. Every documented
/// <see cref="Hmp1SixelRecordingFailureReason"/> gets its own explicit,
/// deliberately triggered test -- this format must fail loudly (no broad
/// catches, no success-shaped fallback) for unsupported versions,
/// malformed/truncated records, missing image references, invalid geometry,
/// and resource-limit violations.
/// </summary>
[TestClass]
public class Hmp1SixelRecordingTests
{
    [TestMethod]
    public void RoundTrip_WithSinglePlacement_ReconstructsAllRecordedFields()
    {
        using var producer = CreateHeadlessTerminal();
        var fixture = new SixelFixture(
            "recording-single-band",
            "One-band red probe for recording round-trip coverage.",
            "q#1;2;100;0;0#1!11~"u8.ToArray());
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[3;5H" + Encoding.ASCII.GetString(fixture.StandardBytes)));

        using var producerSnapshot = producer.CreateSnapshot();
        var placement = TestSeq.Single(producerSnapshot.SixelPlacements);

        var recorded = Hmp1SixelRecording.Serialize(producerSnapshot.SixelPlacements);
        var decoded = Hmp1SixelRecording.Deserialize(recorded);

        Assert.AreEqual(Hmp1SixelRecording.CurrentVersion, decoded.Version);
        var image = TestSeq.Single(decoded.Images);
        var recordedPlacement = TestSeq.Single(decoded.Placements);

        Assert.IsFalse(image.IsGeometryOnly);
        Assert.AreEqual(placement.Image.WidthInCells, image.WidthInCells);
        Assert.AreEqual(placement.Image.HeightInCells, image.HeightInCells);
        Assert.AreEqual(SixelRasterStatus.Rasterized, image.RasterStatus);
        CollectionAssert.AreEqual(placement.Image.ContentHash, image.ContentHash);

        Assert.AreEqual(0, recordedPlacement.ImageIndex);
        Assert.AreEqual(placement.Row, recordedPlacement.Row);
        Assert.AreEqual(placement.Column, recordedPlacement.Column);
        Assert.AreEqual(placement.WidthInCells, recordedPlacement.WidthInCells);
        Assert.AreEqual(placement.HeightInCells, recordedPlacement.HeightInCells);
        Assert.AreEqual(placement.PaintedRowOffset, recordedPlacement.PaintedRowOffset);
        Assert.AreEqual(placement.PaintedRowCount, recordedPlacement.PaintedRowCount);
        Assert.AreEqual(placement.PaintedColumnOffset, recordedPlacement.PaintedColumnOffset);
        Assert.AreEqual(placement.PaintedColumnCount, recordedPlacement.PaintedColumnCount);
        Assert.AreEqual(placement.Sequence, recordedPlacement.Sequence);
        Assert.IsEmpty(recordedPlacement.DamagedCells);
    }

    [TestMethod]
    public void RoundTrip_WithMultiplePlacementsSharingOneImage_DeduplicatesImageTable()
    {
        using var producer = CreateHeadlessTerminal();
        var fixture = new SixelFixture(
            "recording-shared-image",
            "Small band replayed at two anchors so both placements reference the same image.",
            "q#1;2;100;0;0#1!2~"u8.ToArray());

        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[1;1H" + Encoding.ASCII.GetString(fixture.StandardBytes)));
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[3;1H" + Encoding.ASCII.GetString(fixture.StandardBytes)));

        using var producerSnapshot = producer.CreateSnapshot();
        Assert.HasCount(2, producerSnapshot.SixelPlacements);

        var recorded = Hmp1SixelRecording.Serialize(producerSnapshot.SixelPlacements);
        var decoded = Hmp1SixelRecording.Deserialize(recorded);

        // Identical pixel content -> a single deduplicated image table entry,
        // even though two independent placements reference it.
        Assert.HasCount(1, decoded.Images);
        Assert.HasCount(2, decoded.Placements);
        Assert.IsTrue(decoded.Placements.All(p => p.ImageIndex == 0));
    }

    [TestMethod]
    public void RoundTrip_WithDamagedCell_PreservesAnchorRelativeDamageOffsets()
    {
        using var producer = CreateHeadlessTerminal();
        var fixture = new SixelFixture(
            "recording-damage-band",
            "A two-cell-wide band whose origin cell is later overwritten with text.",
            "q#1;2;100;0;0#1!11~"u8.ToArray());
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[2;3H" + Encoding.ASCII.GetString(fixture.StandardBytes)));
        // Overwrite only the origin cell with plain text, damaging it.
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2;3HX"));

        using var producerSnapshot = producer.CreateSnapshot();
        var placement = TestSeq.Single(producerSnapshot.SixelPlacements);
        Assert.IsTrue(placement.IsCellDamaged(placement.Row, placement.Column));

        var recorded = Hmp1SixelRecording.Serialize(producerSnapshot.SixelPlacements);
        var decoded = Hmp1SixelRecording.Deserialize(recorded);
        var recordedPlacement = TestSeq.Single(decoded.Placements);

        var damaged = TestSeq.Single(recordedPlacement.DamagedCells);
        Assert.AreEqual((0, 0), damaged);
    }

    [TestMethod]
    public void RoundTrip_WithGeometryOnlyPlacement_PreservesOutcomeAndOriginalPayload()
    {
        using var producer = CreateHeadlessTerminal();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1bP0;1q\"1;1;999999999;999999999#1@\x1b\\"));

        using var producerSnapshot = producer.CreateSnapshot();
        var placement = TestSeq.Single(producerSnapshot.SixelPlacements);
        Assert.IsTrue(placement.IsGeometryOnly);

        var recorded = Hmp1SixelRecording.Serialize(producerSnapshot.SixelPlacements);
        var decoded = Hmp1SixelRecording.Deserialize(recorded);
        var image = TestSeq.Single(decoded.Images);

        Assert.IsTrue(image.IsGeometryOnly);
        Assert.AreEqual(SixelRasterStatus.GeometryOnly, image.RasterStatus);
        Assert.AreEqual(placement.Image.Payload, image.Payload);
    }

    [TestMethod]
    public void BuildReplayEscapeSequence_FedToFreshTerminal_ReconstructsEquivalentGeometryAndPixels()
    {
        using var producer = CreateHeadlessTerminal();
        var fixture = new SixelFixture(
            "recording-replay-band",
            "One-band red probe fed through the recorded replay escape sequence.",
            "q#1;2;100;0;0#1!11~"u8.ToArray());
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[3;5H" + Encoding.ASCII.GetString(fixture.StandardBytes)));

        using var producerSnapshot = producer.CreateSnapshot();
        var recorded = Hmp1SixelRecording.Serialize(producerSnapshot.SixelPlacements);
        var decoded = Hmp1SixelRecording.Deserialize(recorded);
        var replaySequence = decoded.BuildReplayEscapeSequence();

        using var viewer = CreateHeadlessTerminal();
        // Fed through the same tokenizer/apply path a live terminal uses for
        // incoming output, so replay is verified against the same
        // authoritative parser/raster invariants live processing uses.
        viewer.ApplyTokens(AnsiTokenizer.Tokenize(replaySequence));

        using var viewerSnapshot = viewer.CreateSnapshot();
        var producerPlacement = TestSeq.Single(producerSnapshot.SixelPlacements);
        var viewerPlacement = TestSeq.Single(viewerSnapshot.SixelPlacements);

        Assert.AreEqual(producerPlacement.Row, viewerPlacement.Row);
        Assert.AreEqual(producerPlacement.Column, viewerPlacement.Column);
        Assert.AreEqual(producerPlacement.WidthInCells, viewerPlacement.WidthInCells);
        Assert.AreEqual(producerPlacement.HeightInCells, viewerPlacement.HeightInCells);

        AssertPixelsEqual(producerPlacement.Image, viewerPlacement.Image);
    }

    [TestMethod]
    public void RoundTrip_WithHistoryAndViewportPlacements_PreservesTheirDistinctUnifiedRowOffsets()
    {
        using var producer = Hex1bTerminal.CreateBuilder()
            .WithDimensions(8, 3)
            .WithWorkload(new NullWorkloadAdapter())
            .WithScrollback(3)
            .WithHeadless(new TerminalCapabilities { SupportsSixel = true, SupportsTrueColor = true })
            .Build();

        var historyFixture = new SixelFixture(
            "recording-history-band",
            "A red band painted at row 0, later scrolled into history.",
            "q#1;2;100;0;0#1~"u8.ToArray());
        var viewportFixture = new SixelFixture(
            "recording-viewport-band",
            "A green band painted on the live viewport after the scroll.",
            "q#2;2;0;100;0#2~"u8.ToArray());

        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[1;1H" + Encoding.ASCII.GetString(historyFixture.StandardBytes)));
        // Cursor on the bottom row: a plain LF scrolls the whole screen up by
        // one, pushing the row-0 placement into scrollback.
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3;1H\n"));
        // Paint on the *middle* row (not the new bottom row): painting a
        // 1-row-tall placement on the physical last row would itself trigger
        // an additional proactive pre-scroll-for-cursor-room scroll, pushing
        // a second (empty) row into history and shifting this test's
        // intended single-history-row setup out from under it.
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[2;1H" + Encoding.ASCII.GetString(viewportFixture.StandardBytes)));

        using var producerSnapshot = producer.CreateSnapshot(scrollbackLines: 1);
        Assert.HasCount(2, producerSnapshot.SixelPlacements);

        var recorded = Hmp1SixelRecording.Serialize(producerSnapshot.SixelPlacements);
        var decoded = Hmp1SixelRecording.Deserialize(recorded);
        Assert.HasCount(2, decoded.Placements);
        Assert.HasCount(2, decoded.Images);

        var producerByRow = producerSnapshot.SixelPlacements.OrderBy(p => p.Row).ToList();
        var decodedByRow = decoded.Placements.OrderBy(p => p.Row).ToList();

        // The history placement's unified row (0) and the viewport
        // placement's unified row (historyCount + its live row) must both
        // survive the round trip distinctly -- recording never collapses
        // scrollback and live-viewport placements into the same offset.
        Assert.AreEqual(producerByRow[0].Row, decodedByRow[0].Row);
        Assert.AreEqual(producerByRow[1].Row, decodedByRow[1].Row);
        Assert.IsTrue(decodedByRow[0].Row < decodedByRow[1].Row);
    }

    [TestMethod]
    public void RoundTrip_CapturedWhileOnAlternateScreen_RecordsOnlyTheActiveScreensPlacement_AndMainScreenIndependently()
    {
        using var producer = CreateHeadlessTerminal();
        var mainFixture = new SixelFixture(
            "recording-main-screen-band",
            "A red band painted on the main screen.",
            "q#1;2;100;0;0#1~"u8.ToArray());
        var alternateFixture = new SixelFixture(
            "recording-alternate-screen-band",
            "A green band painted only on the alternate screen.",
            "q#2;2;0;100;0#2~"u8.ToArray());

        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[1;1H" + Encoding.ASCII.GetString(mainFixture.StandardBytes)));

        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049h"));
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[1;1H" + Encoding.ASCII.GetString(alternateFixture.StandardBytes)));

        using var alternateSnapshot = producer.CreateSnapshot();
        Assert.IsTrue(alternateSnapshot.InAlternateScreen);
        var alternatePlacement = TestSeq.Single(alternateSnapshot.SixelPlacements);
        Assert.Contains("0;100;0", alternatePlacement.Image.Payload);

        var alternateRecorded = Hmp1SixelRecording.Serialize(alternateSnapshot.SixelPlacements);
        var alternateDecoded = Hmp1SixelRecording.Deserialize(alternateRecorded);
        Assert.Contains("0;100;0", TestSeq.Single(alternateDecoded.Images).Payload);

        using var alternateViewer = CreateHeadlessTerminal();
        alternateViewer.ApplyTokens(AnsiTokenizer.Tokenize(alternateDecoded.BuildReplayEscapeSequence()));
        using var alternateViewerSnapshot = alternateViewer.CreateSnapshot();
        var alternateViewerPlacement = TestSeq.Single(alternateViewerSnapshot.SixelPlacements);
        Assert.AreEqual(alternatePlacement.Row, alternateViewerPlacement.Row);
        Assert.AreEqual(alternatePlacement.Column, alternateViewerPlacement.Column);
        AssertPixelsEqual(alternatePlacement.Image, alternateViewerPlacement.Image);

        // Switching back to the main screen and recording+replaying *that*
        // snapshot independently reconstructs the main screen's own,
        // different graphic -- screen switching never leaks or conflates the
        // two screens' placement state through the recording format.
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049l"));
        using var mainSnapshot = producer.CreateSnapshot();
        Assert.IsFalse(mainSnapshot.InAlternateScreen);
        var mainPlacement = TestSeq.Single(mainSnapshot.SixelPlacements);
        Assert.Contains("100;0;0", mainPlacement.Image.Payload);

        var mainRecorded = Hmp1SixelRecording.Serialize(mainSnapshot.SixelPlacements);
        var mainDecoded = Hmp1SixelRecording.Deserialize(mainRecorded);
        Assert.Contains("100;0;0", TestSeq.Single(mainDecoded.Images).Payload);

        using var mainViewer = CreateHeadlessTerminal();
        mainViewer.ApplyTokens(AnsiTokenizer.Tokenize(mainDecoded.BuildReplayEscapeSequence()));
        using var mainViewerSnapshot = mainViewer.CreateSnapshot();
        AssertPixelsEqual(mainPlacement.Image, TestSeq.Single(mainViewerSnapshot.SixelPlacements).Image);
    }

    [TestMethod]
    public void Deserialize_WithWrongMagicMarker_ThrowsMalformed()
    {
        var bytes = new byte[16];
        Encoding.ASCII.GetBytes("XXXX").CopyTo(bytes, 0);

        var ex = Assert.ThrowsExactly<Hmp1SixelRecordingException>(
            () => Hmp1SixelRecording.Deserialize(bytes));
        Assert.AreEqual(Hmp1SixelRecordingFailureReason.Malformed, ex.Reason);
    }

    [TestMethod]
    public void Deserialize_WithTruncatedData_ThrowsTruncated()
    {
        using var producer = CreateHeadlessTerminal();
        var fixture = new SixelFixture(
            "recording-truncation-probe",
            "One-band probe whose serialized recording gets truncated for this test.",
            "q#1;2;100;0;0#1!11~"u8.ToArray());
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[1;1H" + Encoding.ASCII.GetString(fixture.StandardBytes)));

        using var producerSnapshot = producer.CreateSnapshot();
        var recorded = Hmp1SixelRecording.Serialize(producerSnapshot.SixelPlacements);
        var truncated = recorded[..^8];

        var ex = Assert.ThrowsExactly<Hmp1SixelRecordingException>(
            () => Hmp1SixelRecording.Deserialize(truncated));
        Assert.AreEqual(Hmp1SixelRecordingFailureReason.Truncated, ex.Reason);
    }

    [TestMethod]
    public void Deserialize_WithUnsupportedVersion_ThrowsUnsupportedVersion()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("SXRC"u8.ToArray());
            writer.Write(Hmp1SixelRecording.CurrentVersion + 1);
            writer.Write(0); // placementCount
            writer.Write(0); // imageCount
        }

        var ex = Assert.ThrowsExactly<Hmp1SixelRecordingException>(
            () => Hmp1SixelRecording.Deserialize(stream.ToArray()));
        Assert.AreEqual(Hmp1SixelRecordingFailureReason.UnsupportedVersion, ex.Reason);
    }

    [TestMethod]
    public void Deserialize_WithPlacementReferencingMissingImageIndex_ThrowsMissingImageReference()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("SXRC"u8.ToArray());
            writer.Write(Hmp1SixelRecording.CurrentVersion);
            writer.Write(1); // placementCount
            writer.Write(0); // imageCount -- no images at all.

            // One placement referencing image index 0, which does not exist.
            writer.Write(0); // imageIndex
            writer.Write(0); // row
            writer.Write(0); // column
            writer.Write(1); // widthInCells
            writer.Write(1); // heightInCells
            writer.Write(0); // paintedRowOffset
            writer.Write(1); // paintedRowCount
            writer.Write(0); // paintedColumnOffset
            writer.Write(1); // paintedColumnCount
            writer.Write(0L); // sequence
            writer.Write(0L); // createdAtUnixMs
            writer.Write(0); // damagedCellCount
        }

        var ex = Assert.ThrowsExactly<Hmp1SixelRecordingException>(
            () => Hmp1SixelRecording.Deserialize(stream.ToArray()));
        Assert.AreEqual(Hmp1SixelRecordingFailureReason.MissingImageReference, ex.Reason);
    }

    [TestMethod]
    public void Deserialize_WithNonPositiveImageCellDimension_ThrowsInvalidGeometry()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("SXRC"u8.ToArray());
            writer.Write(Hmp1SixelRecording.CurrentVersion);
            writer.Write(0); // placementCount
            writer.Write(1); // imageCount

            // One image declaring a non-positive width in cells.
            writer.Write(new byte[32]); // contentHash
            writer.Write(false); // isGeometryOnly
            writer.Write(1); // declaredPixelWidth
            writer.Write(1); // declaredPixelHeight
            writer.Write(0); // widthInCells -- invalid: must be positive.
            writer.Write(1); // heightInCells
            writer.Write((byte)SixelRasterStatus.Rasterized);
            writer.Write(0); // payloadLength
        }

        var ex = Assert.ThrowsExactly<Hmp1SixelRecordingException>(
            () => Hmp1SixelRecording.Deserialize(stream.ToArray()));
        Assert.AreEqual(Hmp1SixelRecordingFailureReason.InvalidGeometry, ex.Reason);
    }

    [TestMethod]
    public void Deserialize_WithPayloadLengthExceedingLimit_ThrowsResourceLimitExceeded()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("SXRC"u8.ToArray());
            writer.Write(Hmp1SixelRecording.CurrentVersion);
            writer.Write(0); // placementCount
            writer.Write(1); // imageCount

            writer.Write(new byte[32]); // contentHash
            writer.Write(false); // isGeometryOnly
            writer.Write(1); // declaredPixelWidth
            writer.Write(1); // declaredPixelHeight
            writer.Write(1); // widthInCells
            writer.Write(1); // heightInCells
            writer.Write((byte)SixelRasterStatus.Rasterized);
            writer.Write(int.MaxValue); // payloadLength -- declared far beyond the limit.
            // No payload bytes follow: the length check must fail before any
            // attempt to read that much data.
        }

        var ex = Assert.ThrowsExactly<Hmp1SixelRecordingException>(
            () => Hmp1SixelRecording.Deserialize(stream.ToArray()));
        Assert.AreEqual(Hmp1SixelRecordingFailureReason.ResourceLimitExceeded, ex.Reason);
    }

    [TestMethod]
    public void Serialize_WithPlacementCountExceedingLimit_ThrowsResourceLimitExceeded()
    {
        using var producer = CreateHeadlessTerminal();
        var fixture = new SixelFixture(
            "recording-limit-probe",
            "One-band probe reused to synthesize an over-limit placement list.",
            "q#1;2;100;0;0#1!11~"u8.ToArray());
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[1;1H" + Encoding.ASCII.GetString(fixture.StandardBytes)));

        using var producerSnapshot = producer.CreateSnapshot();
        var placement = TestSeq.Single(producerSnapshot.SixelPlacements);

        // The same placement reference repeated past the limit is sufficient
        // to exercise the count check -- content identity is irrelevant to
        // this specific validation.
        var oversized = Enumerable.Repeat(placement, Hmp1SixelRecording.MaxPlacementCount + 1).ToList();

        var ex = Assert.ThrowsExactly<Hmp1SixelRecordingException>(
            () => Hmp1SixelRecording.Serialize(oversized));
        Assert.AreEqual(Hmp1SixelRecordingFailureReason.ResourceLimitExceeded, ex.Reason);
    }

    private static void AssertPixelsEqual(SixelData expected, SixelData actual)
    {
        var expectedPixels = expected.GetPixels();
        var actualPixels = actual.GetPixels();
        Assert.IsNotNull(expectedPixels);
        Assert.IsNotNull(actualPixels);
        Assert.AreEqual(expectedPixels.Width, actualPixels.Width);
        Assert.AreEqual(expectedPixels.Height, actualPixels.Height);
        for (var y = 0; y < expectedPixels.Height; y++)
        {
            for (var x = 0; x < expectedPixels.Width; x++)
            {
                Assert.AreEqual(expectedPixels[x, y], actualPixels[x, y]);
            }
        }
    }

    private static Hex1bTerminal CreateHeadlessTerminal()
        => Hex1bTerminal.CreateBuilder()
            .WithDimensions(20, 10)
            .WithWorkload(new NullWorkloadAdapter())
            .WithHeadless(new TerminalCapabilities { SupportsSixel = true, SupportsTrueColor = true })
            .Build();

    private sealed class NullWorkloadAdapter : IHex1bTerminalWorkloadAdapter
    {
        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);

        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
