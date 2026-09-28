using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Hex1b.Automation;
using Hex1b.Reflow;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class LineRenditionTests
{
    [TestMethod]
    [DataRow('3', LineRendition.DoubleHeightTop)]
    [DataRow('4', LineRendition.DoubleHeightBottom)]
    [DataRow('5', LineRendition.SingleWidth)]
    [DataRow('6', LineRendition.DoubleWidth)]
    public void Tokenize_LineRendition_RoundTrips(char code, LineRendition rendition)
    {
        var sequence = $"\x1b#{code}";
        var token = TestSeq.IsType<LineRenditionToken>(TestSeq.Single(AnsiTokenizer.Tokenize(sequence)));
        Assert.AreEqual(rendition, token.Rendition);
        Assert.AreEqual(sequence, AnsiTokenSerializer.Serialize(token));
        Assert.AreEqual(sequence, Encoding.UTF8.GetString(AnsiTokenUtf8Serializer.Serialize(token).Span));
    }

    [TestMethod]
    public void Apply_WidthChange_DiscardsRightHalfAndClampsCursor()
    {
        using var terminal = Create();
        Feed(terminal, "abcdefghij\x1b#6");
        using var wide = terminal.CreateSnapshot();
        Assert.AreEqual("abcde", wide.GetLineTrimmed(0));
        Assert.AreEqual(4, wide.CursorX);
        Assert.AreEqual(LineRendition.DoubleWidth, wide.GetLineRendition(0));
        Feed(terminal, "\x1b#5");
        using var normal = terminal.CreateSnapshot();
        Assert.AreEqual("abcde", normal.GetLineTrimmed(0));
    }

    [TestMethod]
    public void Apply_Autowrap_UsesDestinationRowsOwnRendition()
    {
        using var terminal = Create();
        Feed(terminal, "\x1b#3abcdef");
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("abcde", snapshot.GetLineTrimmed(0));
        Assert.AreEqual("f", snapshot.GetLineTrimmed(1));
        Assert.AreEqual(LineRendition.DoubleHeightTop, snapshot.GetLineRendition(0));
        Assert.AreEqual(LineRendition.SingleWidth, snapshot.GetLineRendition(1));
        Assert.AreEqual(1, snapshot.CursorX);
    }

    [TestMethod]
    public void Apply_NoAutowrapAndCursorAddressing_UseHalfWidth()
    {
        using var terminal = Create();
        Feed(terminal, "\x1b#6\x1b[?7labcdefg\x1b[99G");
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("abcdg", snapshot.GetLineTrimmed(0));
        Assert.AreEqual(4, snapshot.CursorX);
        Feed(terminal, "\r\t");
        Assert.AreEqual(4, terminal.CreateSnapshot().CursorX);
    }

    [TestMethod]
    public void Apply_ScrollAndLineEditing_MoveRenditionsWithRows()
    {
        using var terminal = Create();
        Feed(terminal, "\x1b#3Top\r\n\x1b#4Low\r\nnormal\r\nlast\r\n");
        using var scrolled = terminal.CreateSnapshot();
        Assert.AreEqual(LineRendition.DoubleHeightBottom, scrolled.GetLineRendition(0));
        Assert.AreEqual(LineRendition.SingleWidth, scrolled.GetLineRendition(3));
        Feed(terminal, "\x1b[H\x1b[L");
        using var inserted = terminal.CreateSnapshot();
        Assert.AreEqual(LineRendition.SingleWidth, inserted.GetLineRendition(0));
        Assert.AreEqual(LineRendition.DoubleHeightBottom, inserted.GetLineRendition(1));
        Feed(terminal, "\x1b[M");
        Assert.AreEqual(LineRendition.DoubleHeightBottom, terminal.CreateSnapshot().GetLineRendition(0));
    }

    [TestMethod]
    public void Apply_EraseAndMargins_ResetOnlySpecifiedRenditions()
    {
        using var terminal = Create();
        Feed(terminal, "\x1b#3abc\x1b[2K");
        Assert.AreEqual(LineRendition.DoubleHeightTop, terminal.CreateSnapshot().GetLineRendition(0));
        Feed(terminal, "\x1b[2J");
        Assert.AreEqual(LineRendition.SingleWidth, terminal.CreateSnapshot().GetLineRendition(0));
        Feed(terminal, "\x1b#6\x1b[?69h\x1b#3");
        Assert.AreEqual(LineRendition.SingleWidth, terminal.CreateSnapshot().GetLineRendition(0));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Resize_EnlargedRows_TruncateWithoutReflow(bool reflow)
    {
        using var terminal = Create(reflow);
        Feed(terminal, "\x1b#3ABCDE\r\n\x1b#4ABCDE\r\nnormal");
        terminal.Resize(6, 4);
        using var narrow = terminal.CreateSnapshot();
        Assert.AreEqual("ABC", narrow.GetLineTrimmed(0));
        Assert.AreEqual("ABC", narrow.GetLineTrimmed(1));
        Assert.AreEqual("normal", narrow.GetLineTrimmed(2));
        Assert.AreEqual(LineRendition.DoubleHeightTop, narrow.GetLineRendition(0));
        Assert.AreEqual(LineRendition.DoubleHeightBottom, narrow.GetLineRendition(1));
        terminal.Resize(10, 4);
        Assert.AreEqual("ABC", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    [TestMethod]
    public void Apply_AlternateScreen_RestoresMainRendition()
    {
        using var terminal = Create();
        Feed(terminal, "\x1b#6Main\x1b[?1049h\x1b[HAlt\x1b[?1049l");
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("Main", snapshot.GetLineTrimmed(0));
        Assert.AreEqual(LineRendition.DoubleWidth, snapshot.GetLineRendition(0));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ToAnsi_EnlargedRows_RoundTripsSnapshot(bool replay)
    {
        using var terminal = Create();
        using var replica = Create();
        Feed(terminal, "\x1b#3Hello\r\n\x1b#4Hello\r\nnormal");
        using var snapshot = terminal.CreateSnapshot();
        Feed(replica, snapshot.ToAnsi(new TerminalAnsiOptions { IncludeClearScreen = true },
            includeHyperlinks: replay, preserveSoftWrap: replay));
        using var copy = replica.CreateSnapshot();
        for (var row = 0; row < 4; row++)
        {
            Assert.AreEqual(snapshot.GetLineTrimmed(row), copy.GetLineTrimmed(row));
            Assert.AreEqual(snapshot.GetLineRendition(row), copy.GetLineRendition(row));
        }
    }

    [TestMethod]
    public async Task ReadFrame_RenditionOnlyChange_TransmitsRowMetadata()
    {
        await using var presentation = new Hwt1PresentationAdapter(10, 4);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithPresentation(presentation)
            .WithDimensions(10, 4).WithScrollback(20).Build();
        Feed(terminal, "\x1b#3Hello\r\n\x1b#4Hello");
        using var first = Metadata(await presentation.ReadFrameAsync(TestContext.Current.CancellationToken));
        Assert.AreEqual(2, first.RootElement.GetProperty("lineRenditions")[0].GetInt32());
        Assert.AreEqual(3, first.RootElement.GetProperty("lineRenditions")[1].GetInt32());
        await presentation.HandleMessageAsync("""{"type":"ack","revision":1}"""u8.ToArray());
        Feed(terminal, "\x1b[H\x1b#6");
        using var delta = Metadata(await presentation.ReadFrameAsync(TestContext.Current.CancellationToken));
        Assert.IsFalse(delta.RootElement.GetProperty("full").GetBoolean());
        Assert.AreEqual(1, delta.RootElement.GetProperty("lineRenditions")[0].GetInt32());
    }

    private static JsonDocument Metadata(ReadOnlyMemory<byte> frame) =>
        JsonDocument.Parse(frame.Slice(8, BinaryPrimitives.ReadInt32LittleEndian(frame.Span[4..])));

    [TestMethod]
    public void Apply_WideUnicodeAtBoundary_WrapsAndNeverLeavesHalfAGlyph()
    {
        using var terminal = Create();
        Feed(terminal, "\x1b#6abcd界");
        using var wrapped = terminal.CreateSnapshot();
        Assert.AreEqual("abcd", wrapped.GetLineTrimmed(0));
        Assert.AreEqual("界", wrapped.GetLineTrimmed(1));
        Feed(terminal, "\x1b[H\x1b#5abc界\x1b#6");
        terminal.Resize(8, 4);
        using var cropped = terminal.CreateSnapshot();
        Assert.AreEqual("abc", cropped.GetLineTrimmed(0));
    }

    [TestMethod]
    public void Apply_SplitCombiningAndVariationSelectors_RespectLogicalWidth()
    {
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithDimensions(10, 4).WithHeadless(new TerminalCapabilities
            {
                SupportsRetroactiveVariationSelectors = true
            }).Build();
        Feed(terminal, "\x1b#6abcd♥");
        Feed(terminal, "\ufe0f");
        using var emoji = terminal.CreateSnapshot();
        Assert.AreEqual("abcd", emoji.GetLineTrimmed(0));
        Assert.AreEqual("♥\ufe0f", emoji.GetLineTrimmed(1));
        Feed(terminal, "\x1b[Habcde");
        Feed(terminal, "\u0301");
        using var combining = terminal.CreateSnapshot();
        Assert.AreEqual("abcde\u0301", combining.GetLineTrimmed(0));
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(9, 4)]
    public void Apply_NarrowAndOddViewports_UseBoundedLogicalCapacity(int width, int capacity)
    {
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(width, 4).Build();
        Feed(terminal, "\x1b#6" + new string('a', capacity) + "b");
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(new string('a', capacity), snapshot.GetLineTrimmed(0));
        Assert.AreEqual("b", snapshot.GetLineTrimmed(1));
    }

    [TestMethod]
    public void Apply_ReverseWrapAcrossEnlargedRow_CountsLogicalColumns()
    {
        using var terminal = Create();
        Feed(terminal, "\x1b#6ABCDE\r\n\x1b[?1045h\x1b[2D");
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(0, snapshot.CursorY);
        Assert.AreEqual(3, snapshot.CursorX);
    }

    [TestMethod]
    public void Apply_EraseToStartAtRightEdge_NormalizesEntireErasedRow()
    {
        using var terminal = Create();
        Feed(terminal, "\x1b#6abcde\x1b[1J");
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(LineRendition.SingleWidth, snapshot.GetLineRendition(0));
        Assert.AreEqual("", snapshot.GetLineTrimmed(0));
    }

    [TestMethod]
    public async Task History_EnlargedRows_PreserveRenditionAndCopyBothHalves()
    {
        await using var presentation = new Hwt1PresentationAdapter(10, 4);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(presentation).WithDimensions(10, 4).WithScrollback(20).Build();
        Feed(terminal, "\x1b#3Hello\r\n\x1b#4Hello\r\nnormal\r\nlast\r\nmore\r\n");
        await presentation.HandleMessageAsync("""{"type":"viewport","requestId":1,"delta":-100}"""u8.ToArray());
        using var historical = Metadata(await presentation.ReadFrameAsync(TestContext.Current.CancellationToken));
        Assert.AreEqual(2, historical.RootElement.GetProperty("lineRenditions")[0].GetInt32());
        Assert.AreEqual(3, historical.RootElement.GetProperty("lineRenditions")[1].GetInt32());
        var history = historical.RootElement.GetProperty("history");
        await presentation.HandleMessageAsync("""{"type":"ack","revision":1}"""u8.ToArray());
        foreach (var (row, action) in new[] { (0, "start"), (1, "extend") })
            await presentation.HandleMessageAsync(JsonSerializer.SerializeToUtf8Bytes(new
            {
                type = "selection", action, mode = "line", requestId = row + 2,
                generation = history.GetProperty("generation").GetString(),
                rowId = history.GetProperty("rowIds")[row].GetString(), column = 0
            }));
        await presentation.HandleMessageAsync("""{"type":"copy","requestId":4}"""u8.ToArray());
        using var copied = Metadata(await presentation.ReadFrameAsync(TestContext.Current.CancellationToken));
        Assert.AreEqual("Hello\nHello", copied.RootElement.GetProperty("history").GetProperty("copy").GetProperty("text").GetString());
    }

    private static Hex1bTerminal Create(bool reflow = false)
    {
        var builder = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless().WithDimensions(10, 4).WithScrollback(20);
        if (reflow)
            builder.WithReflow(GhosttyReflowStrategy.Instance);
        return builder.Build();
    }

    private static void Feed(Hex1bTerminal terminal, string text) =>
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(text));
}
