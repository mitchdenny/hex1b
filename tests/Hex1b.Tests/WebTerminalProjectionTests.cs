using System.Text;
using System.Text.Json;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class WebTerminalProjectionTests
{
    private static readonly TerminalCapabilities Capabilities = new()
    {
        SupportsSixel = true, SupportsKgp = true, SupportsTrueColor = true,
        CellPixelWidth = 10, CellPixelHeight = 20
    };

    [TestMethod]
    public void Encode_Hyperlinks_PreservesWideCellsAndWrappedViewportRanges()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[1;39H\x1b]8;id=docs;https://example.com/docs\x1b\\\u754cAB\x1b]8;;\x1b\\plain"));
        using var snapshot = terminal.CreateSnapshot();
        using var frame = Decode(new Hwt1RenderProjection().Encode(snapshot, Capabilities, 0, 1, 1));
        var links = frame.Metadata.RootElement.GetProperty("hyperlinks");
        Assert.AreEqual(2, links.GetArrayLength());
        Assert.AreEqual(0, links[0].GetProperty("row").GetInt32());
        Assert.AreEqual(38, links[0].GetProperty("startColumn").GetInt32());
        Assert.AreEqual(40, links[0].GetProperty("endColumn").GetInt32());
        Assert.AreEqual(1, links[1].GetProperty("row").GetInt32());
        Assert.AreEqual(0, links[1].GetProperty("startColumn").GetInt32());
        Assert.AreEqual(2, links[1].GetProperty("endColumn").GetInt32());
        foreach (var link in links.EnumerateArray())
            Assert.AreEqual("https://example.com/docs", link.GetProperty("uri").GetString());
    }

    [TestMethod]
    public void Encode_HyperlinkOnlyChanges_ReplaceDestinationsWithoutResendingText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        var projection = new Hwt1RenderProjection();
        foreach (var uri in new[] { "https://example.com/first", "https://example.com/second", "" })
        {
            terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\r\x1b]8;;{uri}\x1b\\link\x1b]8;;\x1b\\"));
            using var snapshot = terminal.CreateSnapshot();
            using var frame = Decode(projection.Encode(snapshot, Capabilities, 0, 1, 1));
            var links = frame.Metadata.RootElement.GetProperty("hyperlinks");
            Assert.AreEqual(uri.Length == 0 ? 0 : 1, links.GetArrayLength());
            if (uri.Length != 0)
            {
                Assert.AreEqual(uri, links[0].GetProperty("uri").GetString());
                Assert.AreEqual(4, links[0].GetProperty("endColumn").GetInt32());
            }
            if (projection.Revision > 1)
                Assert.AreEqual(0, frame.Cells.Count);
        }
    }

    [TestMethod]
    public void Encode_HiddenAndErasedHyperlinks_DoNotLeaveClickableRanges()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        var projection = new Hwt1RenderProjection();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]8;;https://example.com\x1b\\A\x1b[8mB\x1b[28mC\x1b]8;;\x1b\\"));
        using (var snapshot = terminal.CreateSnapshot())
        using (var frame = Decode(projection.Encode(snapshot, Capabilities, 0, 1, 1)))
        {
            var links = frame.Metadata.RootElement.GetProperty("hyperlinks");
            Assert.AreEqual(2, links.GetArrayLength());
            Assert.AreEqual(1, links[0].GetProperty("endColumn").GetInt32());
            Assert.AreEqual(2, links[1].GetProperty("startColumn").GetInt32());
        }
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2J"));
        using var erased = terminal.CreateSnapshot();
        using var cleared = Decode(projection.Encode(erased, Capabilities, 0, 2, 2));
        Assert.AreEqual(0, cleared.Metadata.RootElement.GetProperty("hyperlinks").GetArrayLength());
        using var resync = Decode(projection.Encode(erased, Capabilities, 0, 2, 3, forceFull: true));
        Assert.AreEqual(0, resync.Metadata.RootElement.GetProperty("hyperlinks").GetArrayLength());
    }

    [TestMethod]
    public void Encode_MouseModeOnlyChange_AdvertisesTrackingWithoutCellChanges()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        var projection = new Hwt1RenderProjection();
        using (var snapshot = terminal.CreateSnapshot())
        using (var initial = Decode(projection.Encode(snapshot, Capabilities, 0, 1, 1)))
            Assert.AreEqual(0, initial.Metadata.RootElement.GetProperty("mouseTracking").GetInt32());
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1003;1006h"));
        using var enabled = terminal.CreateSnapshot();
        using var frame = Decode(projection.Encode(enabled, Capabilities, 0, 2, 2));
        Assert.AreEqual(1003, frame.Metadata.RootElement.GetProperty("mouseTracking").GetInt32());
        Assert.AreEqual(0, frame.Cells.Count);
    }

    [TestMethod]
    public void Encode_InitialFrame_ContainsPositionedCellsAndCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[31mA\u754c\x1b[?25l"));
        using var snapshot = terminal.CreateSnapshot();
        using var frame = Decode(new Hwt1RenderProjection().Encode(snapshot, Capabilities, 10, 1, 1));
        Assert.IsTrue(frame.Metadata.RootElement.GetProperty("full").GetBoolean());
        Assert.AreEqual(40 * 12, frame.Cells.Count);
        Assert.AreEqual("A", frame.Cells[0].Cell.Text);
        Assert.AreEqual("\u754c", frame.Cells[1].Cell.Text);
        Assert.AreEqual((byte)2, frame.Cells[1].Cell.Width);
        Assert.AreEqual((byte)0, frame.Cells[2].Cell.Width);
        Assert.AreEqual(0u, frame.Cells[0].Cell.Background >> 24);
        Assert.IsFalse(frame.Metadata.RootElement.GetProperty("cursor").GetProperty("visible").GetBoolean());
    }

    [TestMethod]
    public void Encode_TextDelta_MatchesDocumentedHwt1CellBytes()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        var capabilities = new TerminalCapabilities
        {
            CellPixelWidth = 10, CellPixelHeight = 20,
            DefaultForeground = 0xdedede, DefaultBackground = 0x181818
        };
        var projection = new Hwt1RenderProjection();
        using (var initial = terminal.CreateSnapshot())
            projection.Encode(initial, capabilities, 0, 0, 0);
        terminal.ApplyTokens([new TextToken("A")]);
        using var snapshot = terminal.CreateSnapshot();

        var bytes = projection.Encode(snapshot, capabilities, 1, 1, 1);

        var metadataLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4));
        byte[] expected =
        [
            0x01, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0xde, 0xde, 0xde, 0xff,
            0x18, 0x18, 0x18, 0x00,
            0xde, 0xde, 0xde, 0xff,
            0x00, 0x00,
            0x01, 0x00,
            0x01, 0x00,
            0x41
        ];
        TestSeq.AreEqual(expected, bytes[(8 + metadataLength)..]);
    }

    [TestMethod]
    public void Encode_CursorOnlyChange_DoesNotResendCells()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        var projection = new Hwt1RenderProjection();
        using (var snapshot = terminal.CreateSnapshot())
            projection.Encode(snapshot, Capabilities, 0, 1, 1);
        terminal.ApplyTokens([new CursorPositionToken(4, 5), CursorShapeToken.SteadyBar]);
        using var next = terminal.CreateSnapshot();
        using var frame = Decode(projection.Encode(next, Capabilities, 8, 2, 2));
        Assert.AreEqual(0, frame.Cells.Count);
        Assert.AreEqual(1u, frame.Metadata.RootElement.GetProperty("baseRevision").GetUInt32());
        Assert.AreEqual(4, frame.Metadata.RootElement.GetProperty("cursor").GetProperty("x").GetInt32());
        Assert.AreEqual(6, frame.Metadata.RootElement.GetProperty("cursor").GetProperty("shape").GetInt32());
    }

    [TestMethod]
    public void Encode_WideCellOverwritten_ReflectsTerminalClearingItsLead()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\u754c"));
        var projection = new Hwt1RenderProjection();
        using (var snapshot = terminal.CreateSnapshot())
            projection.Encode(snapshot, Capabilities, 0, 1, 1);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;2HX"));
        using var next = terminal.CreateSnapshot();
        using var frame = Decode(projection.Encode(next, Capabilities, 8, 2, 2));
        var lead = frame.Cells.Single(c => c.Index == 0).Cell;
        Assert.AreEqual(next.GetCell(0, 0).Character, lead.Text);
        Assert.AreEqual(" ", lead.Text);
        Assert.AreEqual((byte)1, lead.Width);
        Assert.AreEqual("X", frame.Cells.Single(c => c.Index == 1).Cell.Text);
    }

    [TestMethod]
    public void Encode_KgpPlacementMove_ReusesTextureAndResyncResendsIt()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b_Ga=T,f=32,s=1,v=1,i=1,p=1,c=3,r=2,C=1,q=2;/wAA/w==\x1b\\"));
        var projection = new Hwt1RenderProjection();
        using (var snapshot = terminal.CreateSnapshot())
        using (var first = Decode(projection.Encode(snapshot, Capabilities, 10, 1, 1)))
        {
            Assert.AreEqual(1, first.Metadata.RootElement.GetProperty("images").GetArrayLength());
            CollectionAssert.AreEqual(new byte[] { 255, 0, 0, 255 }, first.Pixels);
        }
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3;4H\x1b_Ga=p,i=1,p=1,c=3,r=2,C=1,q=2\x1b\\"));
        using var moved = terminal.CreateSnapshot();
        using var delta = Decode(projection.Encode(moved, Capabilities, 20, 2, 2));
        Assert.AreEqual(0, delta.Metadata.RootElement.GetProperty("images").GetArrayLength());
        Assert.AreEqual(30, delta.Metadata.RootElement.GetProperty("placements")[0].GetProperty("x").GetDouble());
        using var full = Decode(projection.Encode(moved, Capabilities, 20, 2, 3, forceFull: true));
        Assert.IsTrue(full.Metadata.RootElement.GetProperty("full").GetBoolean());
        Assert.AreEqual(0u, full.Metadata.RootElement.GetProperty("baseRevision").GetUInt32());
        Assert.AreEqual(1, full.Metadata.RootElement.GetProperty("images").GetArrayLength());
    }

    [TestMethod]
    public void Encode_KgpNativeSpriteMovement_PreservesPixelSizeAndReusesImage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        var projection = new Hwt1RenderProjection();
        var pixels = KgpTestHelper.CreatePixelData(3, 3);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[2;3H" + KgpTestHelper.BuildCommand(
                "a=T,f=32,s=3,v=3,i=1,p=1,C=1,q=2", pixels)));

        var offsets = new[] { (X: 0, Y: 0), (X: 5, Y: 10), (X: 8, Y: 18), (X: 9, Y: 19) };
        for (var i = 0; i < offsets.Length; i++)
        {
            var (x, y) = offsets[i];
            if (i > 0)
            {
                terminal.ApplyTokens(AnsiTokenizer.Tokenize(
                    $"\x1b_Ga=p,i=1,p=1,X={x},Y={y},C=1,q=2\x1b\\"));
            }

            using var snapshot = terminal.CreateSnapshot();
            var placement = TestSeq.Single(snapshot.KgpPlacements);
            Assert.IsTrue(placement.UsesNativeSize);
            Assert.AreEqual((uint)((x + 3 + 9) / 10), placement.DisplayColumns);
            Assert.AreEqual((uint)((y + 3 + 19) / 20), placement.DisplayRows);
            using var frame = Decode(projection.Encode(snapshot, Capabilities, 0, i, i));
            var rendered = frame.Metadata.RootElement.GetProperty("placements")[0];
            Assert.AreEqual(20d + x, rendered.GetProperty("x").GetDouble());
            Assert.AreEqual(20d + y, rendered.GetProperty("y").GetDouble());
            Assert.AreEqual(3d, rendered.GetProperty("width").GetDouble());
            Assert.AreEqual(3d, rendered.GetProperty("height").GetDouble());
            Assert.AreEqual(3d, rendered.GetProperty("sourceWidth").GetDouble());
            Assert.AreEqual(3d, rendered.GetProperty("sourceHeight").GetDouble());
            Assert.AreEqual(i == 0 ? 1 : 0,
                frame.Metadata.RootElement.GetProperty("images").GetArrayLength());
        }
    }

    [TestMethod]
    [DataRow("", 3d, 3d)]
    [DataRow(",c=1,r=1", 5d, 16d)]
    [DataRow(",c=2,r=2", 15d, 36d)]
    public void Encode_KgpSourceCrop_PreservesNativeAndExplicitSizing(
        string sizing, double expectedWidth, double expectedHeight)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(KgpTestHelper.BuildCommand(
            $"a=T,f=32,s=8,v=8,i=1,x=2,y=3,w=3,h=3,X=5,Y=4,C=1,q=2{sizing}",
            KgpTestHelper.CreatePixelData(8, 8))));
        using var snapshot = terminal.CreateSnapshot();
        using var frame = Decode(new Hwt1RenderProjection().Encode(snapshot, Capabilities, 0, 1, 1));
        var placement = frame.Metadata.RootElement.GetProperty("placements")[0];
        Assert.AreEqual(5d, placement.GetProperty("x").GetDouble());
        Assert.AreEqual(4d, placement.GetProperty("y").GetDouble());
        Assert.AreEqual(2d, placement.GetProperty("sourceX").GetDouble());
        Assert.AreEqual(3d, placement.GetProperty("sourceY").GetDouble());
        Assert.AreEqual(3d, placement.GetProperty("sourceWidth").GetDouble());
        Assert.AreEqual(3d, placement.GetProperty("sourceHeight").GetDouble());
        Assert.AreEqual(expectedWidth, placement.GetProperty("width").GetDouble());
        Assert.AreEqual(expectedHeight, placement.GetProperty("height").GetDouble());
    }

    [TestMethod]
    public void Encode_KgpNativeSpriteAtViewportEdge_ClipsWithoutStretching()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[12;40H" + KgpTestHelper.BuildCommand(
                "a=T,f=32,s=3,v=3,i=1,X=9,Y=19,C=1,q=2",
                KgpTestHelper.CreatePixelData(3, 3))));
        using var snapshot = terminal.CreateSnapshot();
        using var frame = Decode(new Hwt1RenderProjection().Encode(snapshot, Capabilities, 0, 1, 1));
        var placement = frame.Metadata.RootElement.GetProperty("placements")[0];
        Assert.AreEqual(399d, placement.GetProperty("x").GetDouble());
        Assert.AreEqual(239d, placement.GetProperty("y").GetDouble());
        Assert.AreEqual(1d, placement.GetProperty("width").GetDouble());
        Assert.AreEqual(1d, placement.GetProperty("height").GetDouble());
        Assert.AreEqual(1d, placement.GetProperty("sourceWidth").GetDouble());
        Assert.AreEqual(1d, placement.GetProperty("sourceHeight").GetDouble());
    }

    [TestMethod]
    public void Encode_KgpAnimation_UsesCurrentFrameInsteadOfRoot()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b_Ga=T,f=32,s=1,v=1,i=1,p=1,c=3,r=2,C=1,q=2;/wAA/w==\x1b\\" +
            "\x1b_Ga=f,f=32,s=1,v=1,i=1,z=40,q=2;AAD//w==\x1b\\" +
            "\x1b_Ga=a,i=1,c=2,s=1,q=2\x1b\\"));
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(2, snapshot.KgpImages[1].CurrentFrameNumber);
        using var frame = Decode(new Hwt1RenderProjection().Encode(snapshot, Capabilities, 10, 1, 1));
        CollectionAssert.AreEqual(new byte[] { 0, 0, 255, 255 }, frame.Pixels);
    }

    [TestMethod]
    public void Encode_SixelDamage_ChangesResourceAndPreservesPlacement()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1bP0;1q\"1;1;20;6#1;2;100;0;0#1!20~\x1b\\"));
        var projection = new Hwt1RenderProjection();
        string key;
        using (var snapshot = terminal.CreateSnapshot())
        using (var first = Decode(projection.Encode(snapshot, Capabilities, 10, 1, 1)))
        {
            Assert.AreEqual(1, first.Metadata.RootElement.GetProperty("placements").GetArrayLength());
            Assert.AreEqual(6d, first.Metadata.RootElement.GetProperty("placements")[0].GetProperty("height").GetDouble());
            key = first.Metadata.RootElement.GetProperty("images")[0].GetProperty("key").GetString()!;
            Assert.IsTrue(first.Pixels.Length > 0);
        }
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1HX"));
        using var damaged = terminal.CreateSnapshot();
        using var frame = Decode(projection.Encode(damaged, Capabilities, 20, 2, 2));
        Assert.AreEqual(1, frame.Metadata.RootElement.GetProperty("placements").GetArrayLength());
        Assert.AreNotEqual(key, frame.Metadata.RootElement.GetProperty("images")[0].GetProperty("key").GetString());
        Assert.AreEqual((byte)0, frame.Pixels[3]);
    }

    [TestMethod]
    public void Encode_HundredsOfSmallSixels_RetainsActiveImagesAndEvictsOldResources()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        terminal.Resize(40, 24);
        var projection = new Hwt1RenderProjection();

        for (var frameIndex = 0; frameIndex < 6; frameIndex++)
        {
            var output = new StringBuilder("\x1b[H\x1b[2J");
            for (var i = 0; i < 700; i++)
            {
                var color = frameIndex * 700 + i;
                output.Append($"\x1b[{i / 40 + 1};{i % 40 + 1}H")
                    .Append($"\x1bP0;1q\"1;1;2;6#1;2;{color % 100};{color / 100};0#1!2~\x1b\\");
            }
            terminal.ApplyTokens(AnsiTokenizer.Tokenize(output.ToString()));
            using var snapshot = terminal.CreateSnapshot();
            using var frame = Decode(projection.Encode(snapshot, Capabilities, 0, frameIndex, frameIndex));
            var metadata = frame.Metadata.RootElement;
            Assert.AreEqual(700, metadata.GetProperty("images").GetArrayLength());
            Assert.AreEqual(700, metadata.GetProperty("placements").GetArrayLength());
            Assert.AreEqual(700 * 2 * 6 * 4, frame.Pixels.Length);
            var retained = metadata.GetProperty("retainedImages").EnumerateArray()
                .Select(key => key.GetString()!).ToHashSet(StringComparer.Ordinal);
            Assert.AreEqual(Math.Min((frameIndex + 1) * 700, Hwt1RenderProjection.MaxImageCount), retained.Count);
            Assert.IsTrue(metadata.GetProperty("placements").EnumerateArray()
                .All(placement => retained.Contains(placement.GetProperty("key").GetString()!)));
        }
    }

    [TestMethod]
    public void Encode_OverBudgetDamageVariants_RejectsBeforeDenseAllocationAndPreservesBaseline()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        terminal.Resize(256, 128);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b_Ga=T,f=32,s=1,v=1,i=1,C=1,q=2;/wAA/w==\x1b\\"));
        var projection = new Hwt1RenderProjection();
        using var baseline = terminal.CreateSnapshot();
        projection.Encode(baseline, Capabilities, 0, 0, 0);
        AddLargeSixels(terminal, 9, 2048, 1024);
        using var snapshot = terminal.CreateSnapshot();
        var source = TestSeq.Single(snapshot.SixelImages.Values);
        Assert.AreEqual(9, snapshot.SixelPlacements.Count);
        Assert.IsFalse(source.HasMaterializedPixels);

        var error = Assert.Throws<InvalidDataException>(() =>
            projection.Encode(snapshot, Capabilities, 0, 1, 1));

        Assert.Contains("64 MiB", error.Message);
        Assert.IsFalse(source.HasMaterializedPixels);
        Assert.AreEqual(1u, projection.Revision);
        Assert.AreEqual(9, terminal.SixelPlacementCount);
        Assert.AreEqual(1, terminal.TrackedSixelCount);
        using var resumed = Decode(projection.Encode(baseline, Capabilities, 0, 2, 2));
        Assert.AreEqual(1u, resumed.Metadata.RootElement.GetProperty("baseRevision").GetUInt32());
        Assert.AreEqual(1, resumed.Metadata.RootElement.GetProperty("retainedImages").GetArrayLength());
        Assert.AreEqual(0, resumed.Metadata.RootElement.GetProperty("images").GetArrayLength());
    }

    [TestMethod]
    [DataRow(4096, 4096)]
    [DataRow(4097, 6)]
    public void Encode_OversizedSixel_RejectsBeforeDenseAllocation(int width, int height)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        terminal.Resize(512, 256);
        AddLargeSixels(terminal, 1, width, height);
        using var snapshot = terminal.CreateSnapshot();
        var source = TestSeq.Single(snapshot.SixelImages.Values);

        Assert.Throws<InvalidDataException>(() =>
            new Hwt1RenderProjection().Encode(snapshot, Capabilities, 0, 1, 1));

        Assert.IsFalse(source.HasMaterializedPixels);
        Assert.AreEqual(1, terminal.SixelPlacementCount);
    }

    [TestMethod]
    public void Encode_ExactDecodedBudget_AdmitsVariantsAndReplacesInactiveResources()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        terminal.Resize(512, 128);
        var projection = new Hwt1RenderProjection();
        HashSet<string>? previous = null;
        for (var iteration = 0; iteration < 2; iteration++)
        {
            terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2J"));
            AddLargeSixels(terminal, 2, 4096, 2048, iteration * 2);
            using var snapshot = terminal.CreateSnapshot();
            var bytes = projection.Encode(snapshot, Capabilities, 0, iteration, iteration);
            var metadataLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4));
            using var metadata = JsonDocument.Parse(bytes.AsMemory(8, metadataLength));
            var images = metadata.RootElement.GetProperty("images");
            Assert.AreEqual(2, images.GetArrayLength());
            Assert.AreEqual(64L * 1024 * 1024, images.EnumerateArray().Sum(image =>
                (long)image.GetProperty("width").GetInt32() * image.GetProperty("height").GetInt32() * 4));
            var retained = metadata.RootElement.GetProperty("retainedImages").EnumerateArray()
                .Select(key => key.GetString()!).ToHashSet(StringComparer.Ordinal);
            Assert.AreEqual(2, retained.Count);
            if (previous is not null)
                Assert.IsFalse(previous.Overlaps(retained));
            previous = retained;
        }
    }

    private static void AddLargeSixels(Hex1bTerminal terminal, int count, int width, int height, int damageOffset = 0)
    {
        for (var index = 0; index < count; index++)
        {
            terminal.ApplyTokens(AnsiTokenizer.Tokenize(
                $"\x1b[H\x1bP0;1q\"1;1;{width};{height}#1;2;100;0;0#1@\x1b\\" +
                $"\x1b[1;{damageOffset + index + 2}HX"));
        }
    }

    [TestMethod]
    public void Encode_Resize_StartsNewFullBaseline()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        var projection = new Hwt1RenderProjection();
        using (var snapshot = terminal.CreateSnapshot())
            projection.Encode(snapshot, Capabilities, 0, 1, 1);
        terminal.Resize(50, 15);
        using var resized = terminal.CreateSnapshot();
        using var frame = Decode(projection.Encode(resized, Capabilities, 0, 2, 2));
        Assert.AreEqual(750, frame.Cells.Count);
        Assert.IsTrue(frame.Metadata.RootElement.GetProperty("full").GetBoolean());
        Assert.AreEqual(0u, frame.Metadata.RootElement.GetProperty("baseRevision").GetUInt32());
    }

    [TestMethod]
    public void Encode_Resync_DoesNotRequirePreviouslyCachedInactiveImages()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        var projection = new Hwt1RenderProjection();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b_Ga=T,f=32,s=1,v=1,i=1,p=1,c=1,r=1,C=1,q=2;/wAA/w==\x1b\\"));
        using (var snapshot = terminal.CreateSnapshot())
            projection.Encode(snapshot, Capabilities, 0, 1, 1);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b_Ga=d,d=i,i=1,q=2\x1b\\" +
            "\x1b_Ga=T,f=32,s=1,v=1,i=2,p=1,c=1,r=1,C=1,q=2;AAD//w==\x1b\\"));
        using var next = terminal.CreateSnapshot();
        using var delta = Decode(projection.Encode(next, Capabilities, 0, 2, 2));
        Assert.AreEqual(2, delta.Metadata.RootElement.GetProperty("retainedImages").GetArrayLength());
        using var full = Decode(projection.Encode(next, Capabilities, 0, 2, 3, forceFull: true));
        Assert.AreEqual(1, full.Metadata.RootElement.GetProperty("retainedImages").GetArrayLength());
        Assert.AreEqual(1, full.Metadata.RootElement.GetProperty("images").GetArrayLength());
    }

    [TestMethod]
    public void EncodeKey_ApplicationCursorMode_IsOwnedByServer()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        using var command = JsonDocument.Parse("""{"key":"ArrowUp"}""");
        Assert.AreEqual("\x1b[A", Hwt1Input.EncodeKey(command.RootElement, terminal));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1h"));
        Assert.AreEqual("\x1bOA", Hwt1Input.EncodeKey(command.RootElement, terminal));
    }

    [TestMethod]
    [DataRow("Backspace", false, "\x7f")]
    [DataRow("Backspace", true, "\x08")]
    [DataRow("Delete", false, "\x1b[3~")]
    [DataRow("Delete", true, "\x1b[3;5~")]
    public void EncodeKey_EditingKey_PreservesDistinctControlSequences(string key, bool ctrl, string expected)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        using var command = JsonDocument.Parse(JsonSerializer.Serialize(new { key, ctrl }));
        Assert.AreEqual(expected, Hwt1Input.EncodeKey(command.RootElement, terminal));
    }

    private static Hex1bTerminal CreateTerminal(Hex1bAppWorkloadAdapter workload)
        => Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithHeadless(Capabilities).WithDimensions(40, 12).Build();

    private static DecodedFrame Decode(byte[] bytes)
    {
        using var reader = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8);
        Assert.AreEqual(Hwt1RenderProjection.Magic, reader.ReadUInt32());
        var metadata = JsonDocument.Parse(reader.ReadBytes(reader.ReadInt32()));
        var count = reader.ReadInt32();
        var cells = new List<(int, Hwt1RenderCell)>();
        for (var i = 0; i < count; i++)
        {
            var index = reader.ReadInt32();
            var fg = reader.ReadUInt32();
            var bg = reader.ReadUInt32();
            var ul = reader.ReadUInt32();
            var attrs = reader.ReadUInt16();
            var width = reader.ReadByte();
            var style = reader.ReadByte();
            var text = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadUInt16()));
            cells.Add((index, new(text, fg, bg, ul, attrs, width, style)));
        }
        return new(metadata, cells, reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position)));
    }

    private sealed record DecodedFrame(JsonDocument Metadata, List<(int Index, Hwt1RenderCell Cell)> Cells, byte[] Pixels) : IDisposable
    {
        public void Dispose() => Metadata.Dispose();
    }
}
