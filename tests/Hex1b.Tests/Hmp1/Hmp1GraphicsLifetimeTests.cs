using System.Reflection;
using System.Threading.Channels;

namespace Hex1b.Tests.Hmp1;

[TestClass]
public class Hmp1GraphicsLifetimeTests
{
    [TestMethod]
    public async Task DisposeAsync_RetainedPresentation_DropsProducerAndInputQueue()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var adapter = new Hmp1PresentationAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(adapter).Build();
        var channel = (Channel<ReadOnlyMemory<byte>>)typeof(Hmp1PresentationAdapter)
            .GetField("_inputChannel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter)!;
        Assert.IsTrue(channel.Writer.TryWrite(new byte[1024 * 1024]));

        await adapter.DisposeAsync();

        Assert.AreEqual(0, channel.Reader.Count);
        foreach (var name in new[] { "_terminal", "Resized", "Disconnected" })
            Assert.IsNull(typeof(Hmp1PresentationAdapter)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter), name);
        GC.KeepAlive(adapter);
    }

    [TestMethod]
    public async Task RemoveSessionAsync_BeforeReplayPumpStarts_DropsQueuedSnapshotsAndPayloads()
    {
        await using var adapter = new Hmp1PresentationAdapter();
        var session = new Hmp1PresentationAdapter.Hmp1ClientSession(
            new MemoryStream(), new CancellationTokenSource(), "pending", null, null);
        var image = new KgpImageData(1, 0, new byte[1024 * 1024], 512, 512, KgpFormat.Rgba32);
        var images = new Dictionary<uint, KgpImageData> { [1] = image };
        Assert.IsTrue(session.OutputChannel.Writer.TryWrite(new(default, stream =>
            Hmp1KgpStateReplay.WriteAsync(stream, [], images, 0, 0, session.Cts.Token))));
        Assert.IsTrue(session.OutputChannel.Writer.TryWrite(new(new byte[1024 * 1024], null)));
        Assert.AreEqual(2, session.OutputChannel.Reader.Count);

        await adapter.RemoveSessionAsync(session);

        Assert.AreEqual(0, session.OutputChannel.Reader.Count,
            "Completing a channel alone leaves its graphics snapshots and buffers retained.");
        Assert.IsFalse(session.OutputChannel.Writer.TryWrite(new(new byte[1], null)));
        Assert.AreEqual(1, session.Disposed);
        GC.KeepAlive(session);
    }

    [TestMethod]
    public async Task DisposeAsync_UnreadWorkloadFrames_DropsBuffersWhileAdapterRemainsReferenced()
    {
        await using var adapter = new Hmp1WorkloadAdapter(new Hmp1ClientOptions
        {
            StreamFactory = _ => Task.FromResult(Stream.Null)
        });
        var channel = (Channel<Hmp1WorkloadOutput>)typeof(Hmp1WorkloadAdapter)
            .GetField("_outputChannel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter)!;
        Assert.IsTrue(channel.Writer.TryWrite(new(new byte[1024 * 1024])));
        Assert.IsTrue(channel.Writer.TryWrite(new(new byte[1024 * 1024])));
        Assert.AreEqual(2, channel.Reader.Count);

        await adapter.DisposeAsync();

        Assert.AreEqual(0, channel.Reader.Count);
        Assert.IsFalse(channel.Writer.TryWrite(new(new byte[1])));
        GC.KeepAlive(adapter);
    }
}
