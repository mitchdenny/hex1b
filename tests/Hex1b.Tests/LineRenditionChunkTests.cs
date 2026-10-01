using System.Text;
using Hex1b.Automation;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class LineRenditionChunkTests
{
    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(7)]
    [DataRow(31)]
    public async Task Output_Utf8AndEscapeChunks_MatchesWholeStream(int chunkSize)
    {
        const string output = "\x1b#3\x1b[31;4mAB\u754c\u2764\ufe0f\r\n\x1b#4" +
            "\x1b]8;id=chunk;https://example.test\aA\u0301\U0001f469\u200d\U0001f4bb\x1b]8;;\x1b\\" +
            "\r\n\x1b#6abcdefghijkl\r\n\x1b#5normal\r\n" +
            "\x1b[?1049h\x1b#3ALT\x1b[?1049l\x1b[1;2H\x1bH\tZ";
        var bytes = Encoding.UTF8.GetBytes(output + "\x1b]2;CHUNK_DONE\a");
        using var reference = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless(new TerminalCapabilities { SupportsRetroactiveVariationSelectors = true })
            .WithDimensions(12, 4).WithScrollback(20).Build();
        reference.ApplyTokens(AnsiTokenizer.Tokenize(Encoding.UTF8.GetString(bytes)));
        await using var actual = Hex1bTerminal.CreateBuilder().WithWorkload(new ChunkWorkload(bytes, chunkSize))
            .WithHeadless(new TerminalCapabilities { SupportsRetroactiveVariationSelectors = true })
            .WithDimensions(12, 4).WithScrollback(20).Build();
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.WindowTitle == "CHUNK_DONE", TimeSpan.FromSeconds(5), "chunked output completed")
            .Build().ApplyAsync(actual, TestContext.Current.CancellationToken);
        using var expected = reference.CreateSnapshot(20);
        using var snapshot = actual.CreateSnapshot(20);
        Assert.AreEqual(expected.Height, snapshot.Height);
        Assert.AreEqual(expected.CursorX, snapshot.CursorX);
        Assert.AreEqual(expected.CursorY, snapshot.CursorY);
        for (var row = 0; row < expected.Height; row++)
        {
            Assert.AreEqual(expected.GetLineRendition(row), snapshot.GetLineRendition(row));
            Assert.AreEqual(expected.GetLogicalWidth(row), snapshot.GetLogicalWidth(row));
            Assert.AreEqual(expected.IsLineSoftWrapped(row), snapshot.IsLineSoftWrapped(row));
            for (var col = 0; col < expected.Width; col++)
            {
                var a = expected.GetCell(col, row);
                var b = snapshot.GetCell(col, row);
                Assert.AreEqual(a.Character, b.Character, $"({col},{row}), chunk {chunkSize}");
                Assert.AreEqual(a.Attributes, b.Attributes);
                Assert.AreEqual(a.Foreground, b.Foreground);
                Assert.AreEqual(a.Background, b.Background);
                Assert.AreEqual(a.UnderlineColor, b.UnderlineColor);
                Assert.AreEqual(a.UnderlineStyle, b.UnderlineStyle);
                Assert.AreEqual(a.IsWideWrapPadding, b.IsWideWrapPadding);
                Assert.AreEqual(a.HyperlinkData?.Uri, b.HyperlinkData?.Uri);
            }
        }
    }

    private sealed class ChunkWorkload(byte[] output, int chunkSize) : IHex1bTerminalWorkloadAdapter
    {
        private int _offset;
        public event Action? Disconnected { add { } remove { } }
        public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
        {
            if (_offset == output.Length)
            {
                await Task.Delay(Timeout.Infinite, ct);
                return ReadOnlyMemory<byte>.Empty;
            }
            var count = Math.Min(chunkSize, output.Length - _offset);
            var chunk = output.AsMemory(_offset, count);
            _offset += count;
            return chunk;
        }
        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
