using System.Text;
using WebTerminalDemo;

namespace Hex1b.Tests.Hmp1;

[TestClass]
public class Hmp1OutputCompletionTests
{
    [TestMethod]
    public async Task RunAsync_ReplicaCreatedAfterExit_ConsumesFinalBufferedOutput()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = timeout.Token;
        var (serverStream, clientStream) = DuplexPipeStream.CreatePair();
        await using var serverPipe = serverStream;
        await using var clientPipe = clientStream;
        await using var presentation = new Hmp1PresentationAdapter();
        await using var client = Hmp1TestHelpers.NewClient(clientStream);
        var attaching = presentation.AddClient(serverStream, ct);
        await client.ConnectAsync(ct);
        await using var handle = await attaching.WaitAsync(ct);
        await presentation.WriteOutputAsync("FIRST\r\n"u8.ToArray(), ct);
        var final = "FINAL-e\u0301-\u754c-\U0001f31c-WITHOUT-NEWLINE";
        await presentation.WriteOutputAsync(Encoding.UTF8.GetBytes(final), ct);
        presentation.TerminalCompleted(7);
        await client.DisconnectedTask.WaitAsync(ct);

        await using var replica = Hex1bTerminal.CreateBuilder()
            .WithWorkload(client).WithHeadless().WithDimensions(80, 10).Build();
        await replica.RunAsync(ct);
        using var snapshot = replica.CreateSnapshot();
        Assert.AreEqual("FIRST", snapshot.GetLineTrimmed(0));
        Assert.AreEqual(final, snapshot.GetLineTrimmed(1));
    }

    [TestMethod]
    public async Task TerminalCompleted_BlockedOutputWrite_PreservesEveryQueuedByteBeforeExit()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = timeout.Token;
        var (serverPipe, clientPipe) = DuplexPipeStream.CreatePair();
        await using var serverStream = new GatedHmp1Stream(serverPipe);
        await using var clientStream = clientPipe;
        await using var presentation = new Hmp1PresentationAdapter();
        await using var client = Hmp1TestHelpers.NewClient(clientStream);
        var attaching = presentation.AddClient(serverStream, ct);
        await client.ConnectAsync(ct);
        await using var handle = await attaching.WaitAsync(ct);
        var first = Encoding.UTF8.GetBytes(new string('A', 128 * 1024));
        var final = Encoding.UTF8.GetBytes("FINAL-e\u0301-\u754c-\U0001f31c-WITHOUT-NEWLINE");
        serverStream.Block = true;
        await presentation.WriteOutputAsync(first, ct);
        await serverStream.WriteStarted.Task.WaitAsync(ct);
        await presentation.WriteOutputAsync(final, ct);

        try
        {
            presentation.TerminalCompleted(7);
            Assert.AreEqual(1, serverStream.ActiveWrites, "Exit must not bypass or overlap the blocked output write.");
            Assert.IsFalse(client.DisconnectedTask.IsCompleted);
            serverStream.ReleaseWrite.TrySetResult();
            await client.DisconnectedTask.WaitAsync(ct);
            using var received = new MemoryStream();
            while (true)
            {
                var bytes = await client.ReadOutputAsync(ct);
                if (bytes.IsEmpty)
                    break;
                received.Write(bytes.Span);
            }
            var expected = first.Concat(final).ToArray();
            Assert.IsTrue(received.Length >= expected.Length);
            TestSeq.AreEqual(expected, received.ToArray().TakeLast(expected.Length).ToArray(),
                "Every queued byte, including the final UTF-8 sequence, must precede HMP1 Exit.");
        }
        finally
        {
            serverStream.ReleaseWrite.TrySetResult();
        }
    }
}
