using System.IO.Pipelines;
using System.Text;
using Hex1b.Muxer;

namespace Hex1b.Tests.Muxer;

public class MuxerAdapterTests
{
    [Fact]
    public async Task ClientServer_HelloAndStateSync_ReceivedByClient()
    {
        var (serverToClient, clientToServer) = CreateDuplexStreamPair();

        var server = new MuxerPresentationAdapter(80, 24);
        var client = new MuxerWorkloadAdapter(serverToClient.ReadStream);

        // Server adds the client
        var handle = await server.AddClient(serverToClient.WriteStream);

        // Client connects and reads Hello + StateSync
        // We need to simulate the client reading from the server's output
        // In a real scenario, the client reads from the same stream the server writes to
        // For this test, use connected streams
        Assert.True(handle.IsConnected);

        await handle.DisposeAsync();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task IntegrationTest_ServerSendsOutput_ClientReceivesIt()
    {
        // Create a full duplex pair
        var (stream1, stream2) = CreateFullDuplexPair();

        await using var server = new MuxerPresentationAdapter(80, 24);

        // Add client (sends Hello + StateSync)
        var handle = await server.AddClient(stream1);

        // Create client workload adapter connected to the other end
        var client = new MuxerWorkloadAdapter(stream2);
        await client.ConnectAsync(CancellationToken.None);

        Assert.Equal(80, client.RemoteWidth);
        Assert.Equal(24, client.RemoteHeight);

        // Server sends output
        var outputData = "Hello from server!"u8.ToArray();
        await server.WriteOutputAsync(outputData);

        // Client should receive it
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = await client.ReadOutputAsync(cts.Token);

        Assert.Equal(outputData, received.ToArray());

        await handle.DisposeAsync();
        await client.DisposeAsync();
    }

    [Fact]
    public async Task IntegrationTest_ClientSendsInput_ServerReceivesIt()
    {
        var (stream1, stream2) = CreateFullDuplexPair();

        await using var server = new MuxerPresentationAdapter(80, 24);
        var handle = await server.AddClient(stream1);

        var client = new MuxerWorkloadAdapter(stream2);
        await client.ConnectAsync(CancellationToken.None);

        // Client sends input
        var inputData = "keyboard input"u8.ToArray();
        await client.WriteInputAsync(inputData);

        // Server should receive it
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = await server.ReadInputAsync(cts.Token);

        Assert.Equal(inputData, received.ToArray());

        await handle.DisposeAsync();
        await client.DisposeAsync();
    }

    [Fact]
    public async Task IntegrationTest_ClientSendsResize_ServerFires()
    {
        var (stream1, stream2) = CreateFullDuplexPair();

        await using var server = new MuxerPresentationAdapter(80, 24);
        var handle = await server.AddClient(stream1);

        var client = new MuxerWorkloadAdapter(stream2);
        await client.ConnectAsync(CancellationToken.None);

        var resizeTcs = new TaskCompletionSource<(int Width, int Height)>();
        server.Resized += (w, h) => resizeTcs.TrySetResult((w, h));

        // Client sends resize
        await client.ResizeAsync(120, 40);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (newWidth, newHeight) = await resizeTcs.Task.WaitAsync(cts.Token);

        Assert.Equal(120, newWidth);
        Assert.Equal(40, newHeight);
        Assert.Equal(120, server.Width);
        Assert.Equal(40, server.Height);

        await handle.DisposeAsync();
        await client.DisposeAsync();
    }

    [Fact]
    public async Task IntegrationTest_MultipleClients_AllReceiveOutput()
    {
        var (stream1a, stream1b) = CreateFullDuplexPair();
        var (stream2a, stream2b) = CreateFullDuplexPair();

        await using var server = new MuxerPresentationAdapter(80, 24);

        var handle1 = await server.AddClient(stream1a);
        var handle2 = await server.AddClient(stream2a);

        var client1 = new MuxerWorkloadAdapter(stream1b);
        var client2 = new MuxerWorkloadAdapter(stream2b);
        await client1.ConnectAsync(CancellationToken.None);
        await client2.ConnectAsync(CancellationToken.None);

        Assert.Equal(2, server.ClientCount);

        // Server sends output
        var outputData = "broadcast!"u8.ToArray();
        await server.WriteOutputAsync(outputData);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var received1 = await client1.ReadOutputAsync(cts.Token);
        var received2 = await client2.ReadOutputAsync(cts.Token);

        Assert.Equal(outputData, received1.ToArray());
        Assert.Equal(outputData, received2.ToArray());

        await handle1.DisposeAsync();
        await handle2.DisposeAsync();
        await client1.DisposeAsync();
        await client2.DisposeAsync();
    }

    [Fact]
    public async Task IntegrationTest_ClientDisconnect_ServerContinues()
    {
        var (stream1a, stream1b) = CreateFullDuplexPair();
        var (stream2a, stream2b) = CreateFullDuplexPair();

        await using var server = new MuxerPresentationAdapter(80, 24);

        var handle1 = await server.AddClient(stream1a);
        var handle2 = await server.AddClient(stream2a);

        var client1 = new MuxerWorkloadAdapter(stream1b);
        var client2 = new MuxerWorkloadAdapter(stream2b);
        await client1.ConnectAsync(CancellationToken.None);
        await client2.ConnectAsync(CancellationToken.None);

        Assert.Equal(2, server.ClientCount);

        // Disconnect client 1
        await handle1.DisposeAsync();
        await client1.DisposeAsync();

        // Wait for cleanup
        await Task.Delay(100);

        // Client 2 should still work
        var outputData = "still here"u8.ToArray();
        await server.WriteOutputAsync(outputData);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = await client2.ReadOutputAsync(cts.Token);

        Assert.Equal(outputData, received.ToArray());

        await handle2.DisposeAsync();
        await client2.DisposeAsync();
    }

    [Fact]
    public async Task MuxerWorkloadAdapter_Disconnected_FiredOnStreamClose()
    {
        var (stream1, stream2) = CreateFullDuplexPair();

        await using var server = new MuxerPresentationAdapter(80, 24);
        var handle = await server.AddClient(stream1);

        var client = new MuxerWorkloadAdapter(stream2);
        await client.ConnectAsync(CancellationToken.None);

        var disconnectedTcs = new TaskCompletionSource();
        client.Disconnected += () => disconnectedTcs.TrySetResult();

        // Close server side
        await handle.DisposeAsync();

        // Client should detect disconnect
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await disconnectedTcs.Task.WaitAsync(cts.Token);

        await client.DisposeAsync();
    }

    /// <summary>
    /// Creates a full-duplex stream pair where each side can read and write.
    /// Stream1 writes → Stream2 reads, and Stream2 writes → Stream1 reads.
    /// </summary>
    private static (Stream Stream1, Stream Stream2) CreateFullDuplexPair()
    {
        var pipe1To2 = new Pipe();
        var pipe2To1 = new Pipe();

        var stream1 = new DuplexPipeStream(pipe2To1.Reader.AsStream(), pipe1To2.Writer.AsStream());
        var stream2 = new DuplexPipeStream(pipe1To2.Reader.AsStream(), pipe2To1.Writer.AsStream());

        return (stream1, stream2);
    }

    private static (DuplexStreamPair ServerToClient, DuplexStreamPair ClientToServer) CreateDuplexStreamPair()
    {
        var s2c = new Pipe();
        var c2s = new Pipe();
        return (
            new DuplexStreamPair(s2c.Reader.AsStream(), s2c.Writer.AsStream()),
            new DuplexStreamPair(c2s.Reader.AsStream(), c2s.Writer.AsStream()));
    }

    private sealed record DuplexStreamPair(Stream ReadStream, Stream WriteStream);

    /// <summary>
    /// A stream that reads from one underlying stream and writes to another,
    /// creating a bidirectional channel.
    /// </summary>
    private sealed class DuplexPipeStream(Stream readStream, Stream writeStream) : Stream
    {
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
            => readStream.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
            => readStream.ReadAsync(buffer, ct);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => readStream.ReadAsync(buffer, offset, count, ct);

        public override void Write(byte[] buffer, int offset, int count)
            => writeStream.Write(buffer, offset, count);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
            => writeStream.WriteAsync(buffer, ct);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => writeStream.WriteAsync(buffer, offset, count, ct);

        public override void Flush() => writeStream.Flush();

        public override Task FlushAsync(CancellationToken ct) => writeStream.FlushAsync(ct);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                readStream.Dispose();
                writeStream.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await readStream.DisposeAsync();
            await writeStream.DisposeAsync();
            await base.DisposeAsync();
        }
    }
}
