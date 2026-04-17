using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Hex1b.Muxer;

namespace Hex1b.Tests.Muxer;

public class MuxerProtocolTests
{
    [Fact]
    public async Task WriteFrameAsync_ReadFrameAsync_RoundTrip()
    {
        var (clientStream, serverStream) = CreateStreamPair();

        var payload = "Hello, World!"u8.ToArray();
        await MuxerProtocol.WriteFrameAsync(serverStream, MuxerFrameType.Output, payload);

        var frame = await MuxerProtocol.ReadFrameAsync(clientStream);

        Assert.NotNull(frame);
        Assert.Equal(MuxerFrameType.Output, frame.Value.Type);
        Assert.Equal(payload, frame.Value.Payload.ToArray());
    }

    [Fact]
    public async Task WriteFrameAsync_EmptyPayload_RoundTrips()
    {
        var (clientStream, serverStream) = CreateStreamPair();

        await MuxerProtocol.WriteFrameAsync(serverStream, MuxerFrameType.Exit, ReadOnlyMemory<byte>.Empty);

        var frame = await MuxerProtocol.ReadFrameAsync(clientStream);

        Assert.NotNull(frame);
        Assert.Equal(MuxerFrameType.Exit, frame.Value.Type);
        Assert.True(frame.Value.Payload.IsEmpty);
    }

    [Fact]
    public async Task ReadFrameAsync_ClosedStream_ReturnsNull()
    {
        var (clientStream, serverStream) = CreateStreamPair();

        await serverStream.DisposeAsync();

        var frame = await MuxerProtocol.ReadFrameAsync(clientStream);

        Assert.Null(frame);
    }

    [Fact]
    public async Task WriteHelloAsync_ParseHello_RoundTrip()
    {
        var (clientStream, serverStream) = CreateStreamPair();

        await MuxerProtocol.WriteHelloAsync(serverStream, 120, 40);

        var frame = await MuxerProtocol.ReadFrameAsync(clientStream);

        Assert.NotNull(frame);
        Assert.Equal(MuxerFrameType.Hello, frame.Value.Type);

        var hello = MuxerProtocol.ParseHello(frame.Value.Payload);
        Assert.Equal(MuxerProtocol.Version, hello.Version);
        Assert.Equal(120, hello.Width);
        Assert.Equal(40, hello.Height);
    }

    [Fact]
    public async Task WriteResizeAsync_ParseResize_RoundTrip()
    {
        var (clientStream, serverStream) = CreateStreamPair();

        await MuxerProtocol.WriteResizeAsync(serverStream, 132, 50);

        var frame = await MuxerProtocol.ReadFrameAsync(clientStream);

        Assert.NotNull(frame);
        Assert.Equal(MuxerFrameType.Resize, frame.Value.Type);

        var (width, height) = MuxerProtocol.ParseResize(frame.Value.Payload);
        Assert.Equal(132, width);
        Assert.Equal(50, height);
    }

    [Fact]
    public async Task WriteExitAsync_ParseExitCode_RoundTrip()
    {
        var (clientStream, serverStream) = CreateStreamPair();

        await MuxerProtocol.WriteExitAsync(serverStream, 42);

        var frame = await MuxerProtocol.ReadFrameAsync(clientStream);

        Assert.NotNull(frame);
        Assert.Equal(MuxerFrameType.Exit, frame.Value.Type);

        var exitCode = MuxerProtocol.ParseExitCode(frame.Value.Payload);
        Assert.Equal(42, exitCode);
    }

    [Fact]
    public async Task MultipleFrames_ReadInOrder()
    {
        var (clientStream, serverStream) = CreateStreamPair();

        await MuxerProtocol.WriteFrameAsync(serverStream, MuxerFrameType.Output, "first"u8.ToArray());
        await MuxerProtocol.WriteFrameAsync(serverStream, MuxerFrameType.Output, "second"u8.ToArray());
        await MuxerProtocol.WriteFrameAsync(serverStream, MuxerFrameType.Output, "third"u8.ToArray());

        var frame1 = await MuxerProtocol.ReadFrameAsync(clientStream);
        var frame2 = await MuxerProtocol.ReadFrameAsync(clientStream);
        var frame3 = await MuxerProtocol.ReadFrameAsync(clientStream);

        Assert.Equal("first", Encoding.UTF8.GetString(frame1!.Value.Payload.Span));
        Assert.Equal("second", Encoding.UTF8.GetString(frame2!.Value.Payload.Span));
        Assert.Equal("third", Encoding.UTF8.GetString(frame3!.Value.Payload.Span));
    }

    [Fact]
    public void ParseHello_WrongVersion_Throws()
    {
        var json = """{"version":99,"width":80,"height":24}"""u8.ToArray();

        Assert.Throws<InvalidOperationException>(() => MuxerProtocol.ParseHello(json));
    }

    [Fact]
    public void ParseResize_ShortPayload_Throws()
    {
        var shortPayload = new byte[4]; // Need 8

        Assert.Throws<InvalidOperationException>(() => MuxerProtocol.ParseResize(shortPayload));
    }

    [Fact]
    public async Task AllFrameTypes_RoundTrip()
    {
        var (clientStream, serverStream) = CreateStreamPair();

        // Write all frame types
        await MuxerProtocol.WriteHelloAsync(serverStream, 80, 24);
        await MuxerProtocol.WriteFrameAsync(serverStream, MuxerFrameType.StateSync, "screen data"u8.ToArray());
        await MuxerProtocol.WriteFrameAsync(serverStream, MuxerFrameType.Output, "output data"u8.ToArray());
        await MuxerProtocol.WriteFrameAsync(serverStream, MuxerFrameType.Input, "input data"u8.ToArray());
        await MuxerProtocol.WriteResizeAsync(serverStream, 100, 30);
        await MuxerProtocol.WriteExitAsync(serverStream, 0);

        var f1 = await MuxerProtocol.ReadFrameAsync(clientStream);
        var f2 = await MuxerProtocol.ReadFrameAsync(clientStream);
        var f3 = await MuxerProtocol.ReadFrameAsync(clientStream);
        var f4 = await MuxerProtocol.ReadFrameAsync(clientStream);
        var f5 = await MuxerProtocol.ReadFrameAsync(clientStream);
        var f6 = await MuxerProtocol.ReadFrameAsync(clientStream);

        Assert.Equal(MuxerFrameType.Hello, f1!.Value.Type);
        Assert.Equal(MuxerFrameType.StateSync, f2!.Value.Type);
        Assert.Equal(MuxerFrameType.Output, f3!.Value.Type);
        Assert.Equal(MuxerFrameType.Input, f4!.Value.Type);
        Assert.Equal(MuxerFrameType.Resize, f5!.Value.Type);
        Assert.Equal(MuxerFrameType.Exit, f6!.Value.Type);
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
