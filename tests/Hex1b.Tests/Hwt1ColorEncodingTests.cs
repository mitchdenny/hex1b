using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class Hwt1ColorEncodingTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReadFrameAsync_ColorKindsAndAttributes_PreservesNegotiatedSemantics(bool indexed)
    {
        await using var presentation = new Hwt1PresentationAdapter(20, 2);
        await using var terminal = CreateTerminal(presentation);
        if (indexed)
            await presentation.HandleMessageAsync("""{"type":"colorEncoding","value":"indexed-v1"}"""u8.ToArray());
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "A\x1b[31;44;58;5;1mB\x1b[0;91;104;58;5;12mC" +
            "\x1b[0;38;5;196;48;5;21;58;5;244mD" +
            "\x1b[0;38;2;1;2;3;48;2;4;5;6;58;2;7;8;9mE" +
            "\x1b[0;2;7mF\x1b[0;2;7;31;44mG\x1b[0;38;2;222;222;222;59mH"));

        var frame = await presentation.ReadFrameAsync(TestContext.Current.CancellationToken);
        using var metadata = ReadMetadata(frame);
        AssertEncoding(metadata.RootElement, indexed);
        Assert.AreEqual(0xffdededeu, metadata.RootElement.GetProperty("defaultForeground").GetUInt32());
        Assert.AreEqual(0xff181818u, metadata.RootElement.GetProperty("defaultBackground").GetUInt32());
        var cells = ReadCells(frame);
        var expected = indexed
            ? new (uint Fg, uint Bg, uint Ul)[]
            {
                (0x02000000, 0x03000000, 0x04000000),
                (0x01000001, 0x01000004, 0x01000001),
                (0x01000009, 0x0100000c, 0x0100000c),
                (0x010000c4, 0x01000015, 0x010000f4),
                (0xff030201, 0xff060504, 0xff090807),
                (0x02000000, 0x03000000, 0x04000000),
                (0x01000001, 0x01000004, 0x04000000),
                (0xffdedede, 0x03000000, 0x04000000)
            }
            : new (uint Fg, uint Bg, uint Ul)[]
            {
                (0xffdedede, 0x00181818, 0xffdedede),
                (0xff000080, 0xff800000, 0xff000080),
                (0xff0000ff, 0xffff0000, 0xffff0000),
                (0xff0000ff, 0xffff0000, 0xff808080),
                (0xff030201, 0xff060504, 0xff090807),
                (0xff0c0c0c, 0xffdedede, 0xff0c0c0c),
                (0xff400000, 0xff000080, 0xff400000),
                (0xffdedede, 0x00181818, 0xffdedede)
            };
        using var snapshot = terminal.CreateSnapshot();
        for (var index = 0; index < expected.Length; index++)
        {
            var cell = cells[index];
            Assert.AreEqual(expected[index], (cell.Foreground, cell.Background, cell.UnderlineColor), $"Cell {index}");
            Assert.AreEqual((ushort)snapshot.GetCell(index, 0).Attributes, cell.Attributes);
            Assert.AreEqual(((char)('A' + index)).ToString(), cell.Text);
        }
        Assert.AreEqual((ushort)(CellAttributes.Reverse | CellAttributes.Dim), cells[5].Attributes);
        Assert.AreEqual((ushort)(CellAttributes.Reverse | CellAttributes.Dim), cells[6].Attributes);
    }

    [TestMethod]
    public async Task ReadFrameAsync_AllPaletteIndices_EncodesForegroundBackgroundAndUnderlineReferences()
    {
        await using var presentation = new Hwt1PresentationAdapter(256, 2);
        await using var terminal = CreateTerminal(presentation);
        await presentation.HandleMessageAsync("""{"type":"colorEncoding","value":"indexed-v1"}"""u8.ToArray());
        var output = new StringBuilder();
        for (var index = 0; index < 256; index++)
            output.Append($"\x1b[38;5;{index};48;5;{index};58;5;{index}mX");
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(output.ToString()));

        var cells = ReadCells(await presentation.ReadFrameAsync(TestContext.Current.CancellationToken));
        for (var index = 0; index < 256; index++)
        {
            var expected = 0x01000000u | (uint)index;
            Assert.AreEqual(expected, cells[index].Foreground, $"Foreground {index}");
            Assert.AreEqual(expected, cells[index].Background, $"Background {index}");
            Assert.AreEqual(expected, cells[index].UnderlineColor, $"Underline {index}");
        }
    }

    [TestMethod]
    public async Task ReadFrameAsync_AllAnsiColors_MapsStandardAndBrightToPaletteSlots()
    {
        await using var presentation = new Hwt1PresentationAdapter(20, 2);
        await using var terminal = CreateTerminal(presentation);
        await presentation.HandleMessageAsync("""{"type":"colorEncoding","value":"indexed-v1"}"""u8.ToArray());
        var output = new StringBuilder();
        for (var index = 0; index < 16; index++)
        {
            var foreground = index < 8 ? 30 + index : 90 + index - 8;
            var background = index < 8 ? 40 + index : 100 + index - 8;
            output.Append($"\x1b[{foreground};{background}mX");
        }
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(output.ToString()));

        var cells = ReadCells(await presentation.ReadFrameAsync(TestContext.Current.CancellationToken));
        for (var index = 0; index < 16; index++)
        {
            Assert.AreEqual(0x01000000u | (uint)index, cells[index].Foreground);
            Assert.AreEqual(0x01000000u | (uint)index, cells[index].Background);
            Assert.AreEqual(0x04000000u, cells[index].UnderlineColor);
        }
    }

    [TestMethod]
    public async Task HandleMessageAsync_ColorEncoding_WaitsForAckThenSendsFullAndIdentitySensitiveDelta()
    {
        await using var presentation = new Hwt1PresentationAdapter(20, 2);
        await using var terminal = CreateTerminal(presentation);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[38;5;196mX"));
        var legacy = await presentation.ReadFrameAsync(TestContext.Current.CancellationToken);
        using var legacyMetadata = ReadMetadata(legacy);
        AssertEncoding(legacyMetadata.RootElement, false);
        Assert.AreEqual(0xff0000ffu, ReadCells(legacy)[0].Foreground);
        var pending = presentation.ReadFrameAsync(TestContext.Current.CancellationToken).AsTask();

        await presentation.HandleMessageAsync("""{"type":"colorEncoding","value":"indexed-v1"}"""u8.ToArray());
        await presentation.HandleMessageAsync("""{"type":"ack","revision":0}"""u8.ToArray());
        Assert.IsFalse(pending.IsCompleted, "Negotiation and stale ACKs cannot release the outstanding frame.");
        await presentation.HandleMessageAsync("""{"type":"ack","revision":1}"""u8.ToArray());

        var full = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        using var fullMetadata = ReadMetadata(full);
        AssertEncoding(fullMetadata.RootElement, true);
        Assert.IsTrue(fullMetadata.RootElement.GetProperty("full").GetBoolean());
        Assert.AreEqual(0u, fullMetadata.RootElement.GetProperty("baseRevision").GetUInt32());
        Assert.HasCount(40, ReadCells(full));
        Assert.AreEqual(0x010000c4u, ReadCells(full)[0].Foreground);
        await presentation.HandleMessageAsync("""{"type":"ack","revision":2}"""u8.ToArray());
        await presentation.HandleMessageAsync("""{"type":"colorEncoding","value":"indexed-v1"}"""u8.ToArray());
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\r\x1b[38;2;255;0;0mX"));

        var delta = await presentation.ReadFrameAsync(TestContext.Current.CancellationToken);
        using var deltaMetadata = ReadMetadata(delta);
        AssertEncoding(deltaMetadata.RootElement, true);
        Assert.IsFalse(deltaMetadata.RootElement.GetProperty("full").GetBoolean(), "Repeated opt-in must not reset the baseline.");
        Assert.AreEqual(2u, deltaMetadata.RootElement.GetProperty("baseRevision").GetUInt32());
        Assert.HasCount(1, ReadCells(delta));
        Assert.AreEqual(0xff0000ffu, ReadCells(delta)[0].Foreground,
            "Equal fallback RGB must not conceal an indexed-to-RGB identity change.");
        await presentation.HandleMessageAsync("""{"type":"ack","revision":3}"""u8.ToArray());
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\rX"));

        var unchanged = await presentation.ReadFrameAsync(TestContext.Current.CancellationToken);
        using var unchangedMetadata = ReadMetadata(unchanged);
        AssertEncoding(unchangedMetadata.RootElement, true);
        Assert.IsFalse(unchangedMetadata.RootElement.GetProperty("full").GetBoolean());
        Assert.IsEmpty(ReadCells(unchanged));
    }

    [TestMethod]
    public async Task HandleMessageAsync_ColorEncodingDuringMarkerPaging_RestartsAllPagesInNewModeAfterAck()
    {
        await using var presentation = new Hwt1PresentationAdapter(20, 2);
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            Width = 20, Height = 2, CommandMarkHistoryCapacity = 3000,
            WorkloadAdapter = new Hex1bAppWorkloadAdapter(), PresentationAdapter = presentation,
            RunCallback = _ => Task.FromResult(0)
        });
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(string.Concat(
            Enumerable.Repeat("\x1b]133;A\u0007", 2500))));
        var first = await presentation.ReadFrameAsync(TestContext.Current.CancellationToken);
        using var firstMetadata = ReadMetadata(first);
        AssertEncoding(firstMetadata.RootElement, false);
        var pending = presentation.ReadFrameAsync(TestContext.Current.CancellationToken).AsTask();

        await presentation.HandleMessageAsync("""{"type":"colorEncoding","value":"indexed-v1"}"""u8.ToArray());
        Assert.IsFalse(pending.IsCompleted);
        await presentation.HandleMessageAsync("""{"type":"ack","revision":1}"""u8.ToArray());
        using var restarted = ReadMetadata(await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        AssertEncoding(restarted.RootElement, true);
        Assert.IsTrue(restarted.RootElement.GetProperty("full").GetBoolean());
        Assert.AreEqual(0, restarted.RootElement.GetProperty("history").GetProperty("markerPage").GetProperty("offset").GetInt32());
        var revision = restarted.RootElement.GetProperty("revision").GetUInt32();
        await presentation.HandleMessageAsync(Encoding.UTF8.GetBytes($$"""{"type":"ack","revision":{{revision}}}"""));

        using var last = ReadMetadata(await presentation.ReadFrameAsync(TestContext.Current.CancellationToken));
        AssertEncoding(last.RootElement, true);
        Assert.IsFalse(last.RootElement.GetProperty("full").GetBoolean());
        Assert.AreEqual(Hwt1PresentationAdapter.MarkerPageSize,
            last.RootElement.GetProperty("history").GetProperty("markerPage").GetProperty("offset").GetInt32());
        Assert.AreEqual(restarted.RootElement.GetProperty("history").GetProperty("markerPage").GetProperty("revision").GetString(),
            last.RootElement.GetProperty("history").GetProperty("markerPage").GetProperty("revision").GetString());
    }

    [TestMethod]
    [DataRow("""{"type":"colorEncoding","value":"rgba"}""", typeof(InvalidDataException))]
    [DataRow("""{"type":"colorEncoding","value":null}""", typeof(InvalidDataException))]
    [DataRow("""{"type":"colorEncoding","value":""}""", typeof(InvalidDataException))]
    [DataRow("""{"type":"colorEncoding","value":"INDEXED-V1"}""", typeof(InvalidDataException))]
    [DataRow("""{"type":"colorEncoding","value":1}""", typeof(InvalidOperationException))]
    [DataRow("""{"type":"colorEncoding","value":true}""", typeof(InvalidOperationException))]
    [DataRow("""{"type":"colorEncoding","value":{}}""", typeof(InvalidOperationException))]
    [DataRow("""{"type":"colorEncoding"}""", typeof(KeyNotFoundException))]
    [DataRow("""{"type":"colorEncoding","value":""", typeof(JsonException))]
    public async Task HandleMessageAsync_InvalidColorEncoding_RejectsWithoutChangingMode(string json, Type errorType)
    {
        await using var presentation = new Hwt1PresentationAdapter(20, 2);
        await using var terminal = CreateTerminal(presentation);
        var error = await Assert.ThrowsAsync<Exception>(
            () => presentation.HandleMessageAsync(Encoding.UTF8.GetBytes(json)));
        Assert.IsInstanceOfType(error, errorType);
        using var metadata = ReadMetadata(await presentation.ReadFrameAsync(TestContext.Current.CancellationToken));
        AssertEncoding(metadata.RootElement, false);
    }

    [TestMethod]
    public async Task ReadFrameAsync_ReadOnlyHistoricalView_PreservesReferencesAndIndependentNegotiation()
    {
        await using var muxer = new Hmp1PresentationAdapter(20, 2);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithPresentation(muxer)
            .WithDimensions(20, 2).WithScrollback(100).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[2;7;31;104;58;5;196mX\x1b[0m\r\nsecond\r\nthird\r\nfourth"));
        await using var indexed = await muxer.CreateBrowserViewAsync();
        await using var legacy = await muxer.CreateBrowserViewAsync();
        indexed.IsReadOnly = true;
        await indexed.HandleMessageAsync("""{"type":"colorEncoding","value":"indexed-v1"}"""u8.ToArray());
        await indexed.HandleMessageAsync("""{"type":"viewport","requestId":1,"delta":-100}"""u8.ToArray());
        await legacy.HandleMessageAsync("""{"type":"viewport","requestId":1,"delta":-100}"""u8.ToArray());

        var indexedFrame = await indexed.ReadFrameAsync(TestContext.Current.CancellationToken);
        var legacyFrame = await legacy.ReadFrameAsync(TestContext.Current.CancellationToken);
        using var indexedMetadata = ReadMetadata(indexedFrame);
        using var legacyMetadata = ReadMetadata(legacyFrame);
        AssertEncoding(indexedMetadata.RootElement, true);
        AssertEncoding(legacyMetadata.RootElement, false);
        var cell = ReadCells(indexedFrame)[0];
        Assert.AreEqual("X", cell.Text);
        Assert.AreEqual(0x01000001u, cell.Foreground);
        Assert.AreEqual(0x0100000cu, cell.Background);
        Assert.AreEqual(0x010000c4u, cell.UnderlineColor);
        Assert.AreEqual((ushort)(CellAttributes.Dim | CellAttributes.Reverse), cell.Attributes);
        var legacyCell = ReadCells(legacyFrame)[0];
        Assert.AreEqual("X", legacyCell.Text);
        Assert.AreEqual(0xff7f0000u, legacyCell.Foreground);
        Assert.AreEqual(0xff000080u, legacyCell.Background);
        Assert.AreEqual(0xff0000ffu, legacyCell.UnderlineColor);
    }

    private static Hex1bTerminal CreateTerminal(Hwt1PresentationAdapter presentation)
        => new(new Hex1bTerminalOptions
        {
            Width = presentation.Width, Height = presentation.Height,
            PresentationAdapter = presentation, WorkloadAdapter = new Hex1bAppWorkloadAdapter(),
            RunCallback = _ => Task.FromResult(0)
        });

    private static JsonDocument ReadMetadata(ReadOnlyMemory<byte> frame)
    {
        Assert.IsTrue(frame.Span[..4].SequenceEqual("HWT1"u8));
        var length = BinaryPrimitives.ReadInt32LittleEndian(frame.Span[4..]);
        return JsonDocument.Parse(frame.Slice(8, length));
    }

    private static void AssertEncoding(JsonElement metadata, bool indexed)
    {
        var advertised = metadata.GetProperty("colorEncodings");
        Assert.AreEqual(JsonValueKind.Array, advertised.ValueKind);
        Assert.AreEqual(1, advertised.GetArrayLength());
        Assert.AreEqual("indexed-v1", advertised[0].GetString());
        if (indexed)
            Assert.AreEqual("indexed-v1", metadata.GetProperty("colorEncoding").GetString());
        else
            Assert.IsFalse(metadata.TryGetProperty("colorEncoding", out _));
    }

    private static Dictionary<int, Hwt1RenderCell> ReadCells(ReadOnlyMemory<byte> frame)
    {
        using var stream = new MemoryStream(frame.ToArray());
        using var reader = new BinaryReader(stream);
        stream.Position = 8 + BinaryPrimitives.ReadInt32LittleEndian(frame.Span[4..]);
        var count = reader.ReadInt32();
        var result = new Dictionary<int, Hwt1RenderCell>();
        for (var i = 0; i < count; i++)
        {
            var index = reader.ReadInt32();
            var foreground = reader.ReadUInt32();
            var background = reader.ReadUInt32();
            var underline = reader.ReadUInt32();
            var attributes = reader.ReadUInt16();
            var width = reader.ReadByte();
            var style = reader.ReadByte();
            var text = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadUInt16()));
            result.Add(index, new(text, foreground, background, underline, attributes, width, style));
        }
        return result;
    }
}
