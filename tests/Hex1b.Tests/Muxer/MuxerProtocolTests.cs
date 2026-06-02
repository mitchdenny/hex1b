using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Hex1b;

namespace Hex1b.Tests.Muxer;

[TestClass]
public class Hmp1ProtocolTests
{
    [TestMethod]
    public async Task WriteFrameAsync_ReadFrameAsync_RoundTrip()
    {
        var (clientStream, serverStream) = CreateStreamPair();

        var payload = "Hello, World!"u8.ToArray();
        await Hmp1Protocol.WriteFrameAsync(serverStream, Hmp1FrameType.Output, payload);

        var frame = await Hmp1Protocol.ReadFrameAsync(clientStream);

        Assert.IsNotNull(frame);
        Assert.AreEqual(Hmp1FrameType.Output, frame.Value.Type);
        TestSeq.AreEqual(payload, frame.Value.Payload.ToArray());
    }

    [TestMethod]
    public async Task WriteFrameAsync_EmptyPayload_RoundTrips()
    {
        var (clientStream, serverStream) = CreateStreamPair();

        await Hmp1Protocol.WriteFrameAsync(serverStream, Hmp1FrameType.Exit, ReadOnlyMemory<byte>.Empty);

        var frame = await Hmp1Protocol.ReadFrameAsync(clientStream);

        Assert.IsNotNull(frame);
        Assert.AreEqual(Hmp1FrameType.Exit, frame.Value.Type);
        Assert.IsTrue(frame.Value.Payload.IsEmpty);
    }

    [TestMethod]
    public async Task ReadFrameAsync_ClosedStream_ReturnsNull()
    {
        var (clientStream, serverStream) = CreateStreamPair();

        await serverStream.DisposeAsync();

        var frame = await Hmp1Protocol.ReadFrameAsync(clientStream);

        Assert.IsNull(frame);
    }

    [TestMethod]
    public async Task WriteHelloAsync_ParseHello_RoundTrip()
    {
        var (clientStream, serverStream) = CreateStreamPair();

        await Hmp1Protocol.WriteHelloAsync(serverStream, 120, 40);

        var frame = await Hmp1Protocol.ReadFrameAsync(clientStream);

        Assert.IsNotNull(frame);
        Assert.AreEqual(Hmp1FrameType.Hello, frame.Value.Type);

        var hello = Hmp1Protocol.ParseHello(frame.Value.Payload);
        Assert.AreEqual(Hmp1Protocol.Version, hello.Version);
        Assert.AreEqual(120, hello.Width);
        Assert.AreEqual(40, hello.Height);
    }

    [TestMethod]
    public async Task WriteResizeAsync_ParseResize_RoundTrip()
    {
        var (clientStream, serverStream) = CreateStreamPair();

        await Hmp1Protocol.WriteResizeAsync(serverStream, 132, 50);

        var frame = await Hmp1Protocol.ReadFrameAsync(clientStream);

        Assert.IsNotNull(frame);
        Assert.AreEqual(Hmp1FrameType.Resize, frame.Value.Type);

        var (width, height) = Hmp1Protocol.ParseResize(frame.Value.Payload);
        Assert.AreEqual(132, width);
        Assert.AreEqual(50, height);
    }

    [TestMethod]
    public async Task WriteExitAsync_ParseExitCode_RoundTrip()
    {
        var (clientStream, serverStream) = CreateStreamPair();

        await Hmp1Protocol.WriteExitAsync(serverStream, 42);

        var frame = await Hmp1Protocol.ReadFrameAsync(clientStream);

        Assert.IsNotNull(frame);
        Assert.AreEqual(Hmp1FrameType.Exit, frame.Value.Type);

        var exitCode = Hmp1Protocol.ParseExitCode(frame.Value.Payload);
        Assert.AreEqual(42, exitCode);
    }

    [TestMethod]
    public async Task MultipleFrames_ReadInOrder()
    {
        var (clientStream, serverStream) = CreateStreamPair();

        await Hmp1Protocol.WriteFrameAsync(serverStream, Hmp1FrameType.Output, "first"u8.ToArray());
        await Hmp1Protocol.WriteFrameAsync(serverStream, Hmp1FrameType.Output, "second"u8.ToArray());
        await Hmp1Protocol.WriteFrameAsync(serverStream, Hmp1FrameType.Output, "third"u8.ToArray());

        var frame1 = await Hmp1Protocol.ReadFrameAsync(clientStream);
        var frame2 = await Hmp1Protocol.ReadFrameAsync(clientStream);
        var frame3 = await Hmp1Protocol.ReadFrameAsync(clientStream);

        Assert.AreEqual("first", Encoding.UTF8.GetString(frame1!.Value.Payload.Span));
        Assert.AreEqual("second", Encoding.UTF8.GetString(frame2!.Value.Payload.Span));
        Assert.AreEqual("third", Encoding.UTF8.GetString(frame3!.Value.Payload.Span));
    }

    [TestMethod]
    public void ParseHello_WrongVersion_Throws()
    {
        var json = """{"version":99,"width":80,"height":24}"""u8.ToArray();

        Assert.ThrowsExactly<InvalidOperationException>(() => Hmp1Protocol.ParseHello(json));
    }

    [TestMethod]
    public void ParseResize_ShortPayload_Throws()
    {
        var shortPayload = new byte[4]; // Need 8

        Assert.ThrowsExactly<InvalidOperationException>(() => Hmp1Protocol.ParseResize(shortPayload));
    }

    [TestMethod]
    public async Task AllFrameTypes_RoundTrip()
    {
        var (clientStream, serverStream) = CreateStreamPair();

        // Write all frame types
        await Hmp1Protocol.WriteHelloAsync(serverStream, 80, 24);
        await Hmp1Protocol.WriteFrameAsync(serverStream, Hmp1FrameType.StateSync, "screen data"u8.ToArray());
        await Hmp1Protocol.WriteFrameAsync(serverStream, Hmp1FrameType.Output, "output data"u8.ToArray());
        await Hmp1Protocol.WriteFrameAsync(serverStream, Hmp1FrameType.Input, "input data"u8.ToArray());
        await Hmp1Protocol.WriteResizeAsync(serverStream, 100, 30);
        await Hmp1Protocol.WriteExitAsync(serverStream, 0);

        var f1 = await Hmp1Protocol.ReadFrameAsync(clientStream);
        var f2 = await Hmp1Protocol.ReadFrameAsync(clientStream);
        var f3 = await Hmp1Protocol.ReadFrameAsync(clientStream);
        var f4 = await Hmp1Protocol.ReadFrameAsync(clientStream);
        var f5 = await Hmp1Protocol.ReadFrameAsync(clientStream);
        var f6 = await Hmp1Protocol.ReadFrameAsync(clientStream);

        Assert.AreEqual(Hmp1FrameType.Hello, f1!.Value.Type);
        Assert.AreEqual(Hmp1FrameType.StateSync, f2!.Value.Type);
        Assert.AreEqual(Hmp1FrameType.Output, f3!.Value.Type);
        Assert.AreEqual(Hmp1FrameType.Input, f4!.Value.Type);
        Assert.AreEqual(Hmp1FrameType.Resize, f5!.Value.Type);
        Assert.AreEqual(Hmp1FrameType.Exit, f6!.Value.Type);
    }

    /// <summary>
    /// Creates a pair of connected in-memory streams using Pipe.
    /// Data written to stream A can be read from stream B and vice versa.
    /// </summary>
    private static (Stream ClientRead, Stream ServerWrite) CreateStreamPair()
    {
        var pipe = new Pipe();
        return (pipe.Reader.AsStream(), pipe.Writer.AsStream());
    }
}
