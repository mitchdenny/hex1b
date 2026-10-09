using System.Threading.Channels;
using Hex1b;

namespace Hex1b.LegacyWorkloadAdapter;

public sealed class LegacyAdapter : IHex1bTerminalWorkloadAdapter
{
    private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>();

    public ChannelReader<byte[]> Input => _input.Reader;
    public event Action? Disconnected { add { } remove { } }

    public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
        => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);

    public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        => _input.Writer.WriteAsync(data.ToArray(), ct);

    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
        => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        _input.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
