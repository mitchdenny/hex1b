using System.IO.Pipelines;

namespace WebTerminalDemo;

internal sealed class DuplexPipeStream(Stream input, Stream output) : Stream
{
    internal static (Stream Server, Stream Client) CreatePair()
    {
        var toClient = new Pipe(new PipeOptions(useSynchronizationContext: false));
        var toServer = new Pipe(new PipeOptions(useSynchronizationContext: false));
        return (new DuplexPipeStream(toServer.Reader.AsStream(), toClient.Writer.AsStream()),
            new DuplexPipeStream(toClient.Reader.AsStream(), toServer.Writer.AsStream()));
    }

    public override bool CanRead => input.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => output.CanWrite;
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
        try
        {
            if (disposing)
            {
                try { input.Dispose(); }
                finally { output.Dispose(); }
            }
        }
        finally { base.Dispose(disposing); }
    }
}
