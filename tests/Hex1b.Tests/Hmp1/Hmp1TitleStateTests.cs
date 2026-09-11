using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Hex1b.Automation;
using Hex1b.Tokens;

namespace Hex1b.Tests.Hmp1;

[TestClass]
public class Hmp1TitleStateTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public async Task Build_TitleReplayBudget_CountsUtf8BytesAndRejectsIncompleteReplay()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]2;saved;😀\x07\x1b]1;saved;東京\x07\x1b]22;\x07" +
            "\x1b]2;current;😀\x07\x1b]1;\x07"));
        using var snapshot = terminal.CreateSnapshot(includeAllKgpImages: true, includeSavedTitles: true);
        const string expected =
            "\x1b]2;saved;😀\x1b\\\x1b]1;saved;東京\x1b\\\x1b]22;\x1b\\" +
            "\x1b]2;current;😀\x1b\\\x1b]1;\x1b\\";
        var byteBudget = Encoding.UTF8.GetByteCount(expected);
        Assert.IsTrue(byteBudget > expected.Length);

        Assert.ThrowsExactly<InvalidDataException>(() => Hmp1TitleStateReplay.Build(snapshot, 0));
        Assert.ThrowsExactly<InvalidDataException>(() => Hmp1TitleStateReplay.Build(snapshot, expected.Length));
        Assert.ThrowsExactly<InvalidDataException>(() => Hmp1TitleStateReplay.Build(snapshot, byteBudget - 1));
        var replay = Hmp1TitleStateReplay.Build(snapshot, byteBudget);
        Assert.AreEqual(expected, replay);
        Assert.AreEqual(byteBudget, Encoding.UTF8.GetByteCount(replay));
    }

    [TestMethod]
    public async Task StateSync_LateAttachmentAndReconnect_RestoresSeparateTitlesAndSavedStackIncludingEmpty()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(server).WithDimensions(20, 5).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]2;root;title\x07\x1b]1;root;icon\x07\x1b]22;\x07" +
            "\x1b]2;middle;title\x07\x1b]1;middle;icon\x07\x1b]22;\x07" +
            "\x1b]2;current;東京 😀\x07\x1b]1;current;icon\x07"));

        var first = await ConnectAsync(server);
        await using var firstHandle = first.Handle;
        await using var firstClient = first.Client;
        await using var firstView = new Hwt1PresentationAdapter();
        await using var firstMirror = Hex1bTerminal.CreateBuilder()
            .WithWorkload(firstClient).WithPresentation(firstView).Build();
        await AssertFirstConnectedTitleAsync(firstView, "current;東京 😀");
        Assert.AreEqual("current;icon", firstMirror.IconName);

        workload.Write("\x1b]2;\x07\x1b[Hcleared");
        await WaitForScreenAsync(producer, s => s.WindowTitle == "" && s.ContainsText("cleared"));
        await ReadUntilAsync(firstView, frame => frame.GetProperty("title").GetString() == "");
        await firstMirror.DisposeAsync();
        await firstClient.DisposeAsync();
        await firstHandle.DisposeAsync();

        var reconnect = await ConnectAsync(server);
        await using var reconnectHandle = reconnect.Handle;
        await using var reconnectClient = reconnect.Client;
        await using var reconnectView = new Hwt1PresentationAdapter();
        await using var reconnectMirror = Hex1bTerminal.CreateBuilder()
            .WithWorkload(reconnectClient).WithPresentation(reconnectView).Build();
        await AssertFirstConnectedTitleAsync(reconnectView, "");
        Assert.AreEqual("current;icon", reconnectMirror.IconName);

        workload.Write("\x1b]23;\x07\x1b[2;1Hpop-one");
        await WaitForScreenAsync(producer, s => s.ContainsText("pop-one"));
        await WaitForScreenAsync(reconnectMirror, s => s.ContainsText("pop-one"));
        Assert.AreEqual("middle;title", producer.WindowTitle);
        Assert.AreEqual(producer.WindowTitle, reconnectMirror.WindowTitle);
        Assert.AreEqual("middle;icon", reconnectMirror.IconName);
        await ReadUntilAsync(reconnectView, frame => frame.GetProperty("title").GetString() == "middle;title");

        workload.Write("\x1b]23;\x07\x1b[3;1Hpop-two");
        await WaitForScreenAsync(producer, s => s.ContainsText("pop-two"));
        await WaitForScreenAsync(reconnectMirror, s => s.ContainsText("pop-two"));
        Assert.AreEqual("root;title", reconnectMirror.WindowTitle);
        Assert.AreEqual("root;icon", reconnectMirror.IconName);
        Assert.AreEqual(producer.WindowTitle, reconnectMirror.WindowTitle);
    }

    [TestMethod]
    public async Task StateSync_ReplayPausedBeforeApplication_DoesNotPublishConnectedEmptyOrSavedTitles()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(server).WithDimensions(20, 5).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]0;saved;title\x07\x1b]22;\x07\x1b]0;authoritative;title\x07"));
        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var view = new Hwt1PresentationAdapter();
        var gate = new ReplayGate();
        await using var mirror = Hex1bTerminal.CreateBuilder()
            .WithWorkload(client).WithPresentation(view).AddWorkloadFilter(gate).Build();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            await gate.Entered.Task.WaitAsync(timeout.Token);
            var pendingFrame = view.ReadFrameAsync(timeout.Token).AsTask();
            Assert.IsFalse(pendingFrame.IsCompleted,
                "Connected metadata must wait until the authoritative replay is applied.");
            gate.Release.TrySetResult();

            var bytes = await pendingFrame.WaitAsync(timeout.Token);
            var frame = ParseMetadata(bytes);
            Assert.IsNotNull(frame.GetProperty("peer").GetProperty("id").GetString());
            Assert.AreEqual("authoritative;title", frame.GetProperty("title").GetString());
            Assert.AreEqual("authoritative;title", mirror.WindowTitle);
            await AcknowledgeAsync(view, frame);
        }
        finally
        {
            gate.Release.TrySetResult();
        }
    }

    [TestMethod]
    [DataRow("😀", 1)]
    [DataRow("é", 10)]
    [DataRow("界", 10)]
    [DataRow("界", 11)]
    [DataRow("😀", 10)]
    [DataRow("😀", 11)]
    [DataRow("😀", 12)]
    [DataRow("😀", 19)]
    public async Task StateSync_AttachingInsideOscAcrossTwoHops_PreservesIntroducerUtf8AndTerminator(
        string scalar, int split)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(server).WithDimensions(20, 5).Build();
        var upstream = await ConnectAsync(server);
        await using var upstreamHandle = upstream.Handle;
        await using var upstreamClient = upstream.Client;
        await using var relay = new Hmp1PresentationAdapter(20, 5);
        await using var replica = Hex1bTerminal.CreateBuilder()
            .WithWorkload(upstreamClient).WithPresentation(relay).Build();
        var title = $"repo;{scalar};tail";
        var sequence = Encoding.UTF8.GetBytes($"\x1b]2;{title}\x1b\\");
        var prefix = Encoding.UTF8.GetBytes("\x1b]0;before\x07ready\r\n")
            .Concat(sequence.Take(split)).ToArray();
        workload.Write(prefix.AsMemory());
        await WaitForScreenAsync(producer, s => s.ContainsText("ready") && s.WindowTitle == "before");
        await WaitForScreenAsync(replica, s => s.ContainsText("ready") && s.WindowTitle == "before");

        var late = await ConnectAsync(relay);
        await using var lateHandle = late.Handle;
        await using var lateClient = late.Client;
        await using var lateView = new Hwt1PresentationAdapter();
        await using var lateMirror = Hex1bTerminal.CreateBuilder()
            .WithWorkload(lateClient).WithPresentation(lateView).Build();
        await AssertFirstConnectedTitleAsync(lateView, "before");
        Assert.AreEqual("before", lateMirror.IconName);

        var suffix = sequence.Skip(split).Concat(Encoding.UTF8.GetBytes("done")).ToArray();
        workload.Write(suffix.AsMemory());
        await WaitForScreenAsync(producer, s => s.ContainsText("done") && s.WindowTitle == title);
        await WaitForScreenAsync(replica, s => s.ContainsText("done") && s.WindowTitle == title);
        await WaitForScreenAsync(lateMirror, s => s.ContainsText("done") && s.WindowTitle == title);
        await ReadUntilAsync(lateView, frame => frame.GetProperty("title").GetString() == title);

        using var expected = producer.CreateSnapshot();
        using var actual = lateMirror.CreateSnapshot();
        for (var row = 0; row < expected.Height; row++)
            Assert.AreEqual(expected.GetLine(row), actual.GetLine(row), $"No OSC tail may leak into row {row}.");
        Assert.AreEqual("before", lateMirror.IconName);
    }

    private static async Task AssertFirstConnectedTitleAsync(Hwt1PresentationAdapter view, string expected)
    {
        var frame = await ReadUntilAsync(view, metadata =>
            metadata.GetProperty("peer").GetProperty("id").ValueKind == JsonValueKind.String);
        Assert.AreEqual(expected, frame.GetProperty("title").GetString(),
            "The first connected frame, not merely an eventual frame, must contain the current title.");
    }

    private static async Task<JsonElement> ReadUntilAsync(
        Hwt1PresentationAdapter view, Func<JsonElement, bool> predicate)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(Timeout);
        while (true)
        {
            var frame = ParseMetadata(await view.ReadFrameAsync(timeout.Token));
            await AcknowledgeAsync(view, frame);
            if (predicate(frame))
                return frame;
        }
    }

    private static JsonElement ParseMetadata(ReadOnlyMemory<byte> bytes)
    {
        Assert.IsTrue(bytes.Span[..4].SequenceEqual("HWT1"u8));
        var length = BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[4..]);
        using var document = JsonDocument.Parse(bytes.Slice(8, length));
        Assert.AreEqual(JsonValueKind.String, document.RootElement.GetProperty("title").ValueKind);
        return document.RootElement.Clone();
    }

    private static Task AcknowledgeAsync(Hwt1PresentationAdapter view, JsonElement metadata)
        => view.HandleMessageAsync(Encoding.UTF8.GetBytes(
            $$"""{"type":"ack","revision":{{metadata.GetProperty("revision").GetUInt32()}}}"""),
            TestContext.Current.CancellationToken);

    private static async Task WaitForScreenAsync(
        Hex1bTerminal terminal, Func<Hex1bTerminalSnapshot, bool> predicate)
    {
        using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(predicate, Timeout, "ordered title output applied")
            .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
    }

    private static async Task<(Hmp1ClientHandle Handle, Hmp1WorkloadAdapter Client)> ConnectAsync(
        Hmp1PresentationAdapter server)
    {
        var toClient = new Pipe();
        var toServer = new Pipe();
        var serverStream = new DuplexStream(toServer.Reader.AsStream(), toClient.Writer.AsStream());
        var clientStream = new DuplexStream(toClient.Reader.AsStream(), toServer.Writer.AsStream());
        var client = new Hmp1WorkloadAdapter(new Hmp1ClientOptions
        {
            StreamFactory = _ => Task.FromResult<Stream>(clientStream),
        });
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(Timeout);
        var accepted = server.AddClient(serverStream, timeout.Token);
        try
        {
            await client.ConnectAsync(timeout.Token);
            return (await accepted.WaitAsync(timeout.Token), client);
        }
        catch
        {
            timeout.Cancel();
            await client.DisposeAsync();
            await serverStream.DisposeAsync();
            await clientStream.DisposeAsync();
            try
            {
                await using var handle = await accepted.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            }
            catch (OperationCanceledException) { }
            throw;
        }
    }

    private sealed class ReplayGate : IHex1bTerminalWorkloadFilter
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask OnOutputAsync(
            IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(ct);
        }

        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnFrameCompleteAsync(TimeSpan elapsed, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default) => ValueTask.CompletedTask;
    }

    private sealed class DuplexStream(Stream input, Stream output) : Stream
    {
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => input.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => input.ReadAsync(buffer, cancellationToken);
        public override void Write(byte[] buffer, int offset, int count) => output.Write(buffer, offset, count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => output.WriteAsync(buffer, cancellationToken);
        public override void Flush() => output.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => output.FlushAsync(cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                input.Dispose();
                output.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
