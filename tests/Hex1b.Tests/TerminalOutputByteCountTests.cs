using System.Text;
using System.Threading.Channels;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class TerminalOutputByteCountTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task OutputBytesRead_RawAndTokenizedReads_CountsOriginalBytesOnceBeforeFilters(
        bool preTokenized, bool filtered)
    {
        var workload = preTokenized ? new TokenWorkload() : new RawWorkload();
        var filter = new ReplacingPresentationFilter();
        var builder = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24);
        if (filtered)
            builder.AddPresentationFilter(filter);
        await using var terminal = builder.Build();
        filter.CountProvider = () => terminal.OutputBytesRead;
        Assert.AreEqual(0L, terminal.OutputBytesRead);

        // The explicit count serializes more compactly, and the text is multibyte.
        // Neither normalized tokens nor the replacement presentation is the byte count.
        var first = Encoding.UTF8.GetBytes("\x1b[1Aé");
        await workload.Output.Writer.WriteAsync(
            new WorkloadOutputItem(first, [new CursorMoveToken(CursorMoveDirection.Up, 1), new TextToken("é")]),
            TestContext.Current.CancellationToken);
        if (filtered)
            Assert.AreEqual((long)first.Length, await filter.Observed.Reader.ReadAsync(TestContext.Current.CancellationToken)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("é"), TimeSpan.FromSeconds(5), "first output applied")
            .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
        Assert.AreEqual((long)first.Length, terminal.OutputBytesRead);

        await workload.Output.Writer.WriteAsync(new WorkloadOutputItem(ReadOnlyMemory<byte>.Empty, []),
            TestContext.Current.CancellationToken);
        var second = Encoding.UTF8.GetBytes("世界");
        await workload.Output.Writer.WriteAsync(new WorkloadOutputItem(second, [new TextToken("世界")]),
            TestContext.Current.CancellationToken);
        var total = (long)first.Length + second.Length;
        if (filtered)
            Assert.AreEqual(total, await filter.Observed.Reader.ReadAsync(TestContext.Current.CancellationToken)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("é世界"), TimeSpan.FromSeconds(5), "second output applied")
            .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
        Assert.AreEqual(total, terminal.OutputBytesRead);
        Assert.AreEqual(preTokenized ? 0 : 3, workload.RawReads);
        if (workload is TokenWorkload tokens)
            Assert.AreEqual(3, tokens.TokenReads);
    }

    private class RawWorkload : IHex1bTerminalWorkloadAdapter
    {
        internal Channel<WorkloadOutputItem> Output { get; } = Channel.CreateUnbounded<WorkloadOutputItem>();
        internal int RawReads { get; private set; }

        public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
        {
            var item = await Output.Reader.ReadAsync(ct);
            RawReads++;
            return item.Bytes;
        }

        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public event Action? Disconnected { add { } remove { } }

        public ValueTask DisposeAsync()
        {
            Output.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TokenWorkload : RawWorkload, IHex1bTerminalTokenWorkloadAdapter
    {
        internal int TokenReads { get; private set; }

        public async ValueTask<WorkloadOutputItem> ReadOutputItemAsync(CancellationToken ct = default)
        {
            var item = await Output.Reader.ReadAsync(ct);
            TokenReads++;
            return item;
        }
    }

    private sealed class ReplacingPresentationFilter : IHex1bTerminalPresentationFilter
    {
        internal Func<long> CountProvider { get; set; } = () => -1;
        internal Channel<long> Observed { get; } = Channel.CreateUnbounded<long>();

        public ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(
            IReadOnlyList<AppliedToken> appliedTokens, TimeSpan elapsed, CancellationToken ct = default)
        {
            Observed.Writer.TryWrite(CountProvider());
            return ValueTask.FromResult<IReadOnlyList<AnsiToken>>([new TextToken("replacement")]);
        }

        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }
}
