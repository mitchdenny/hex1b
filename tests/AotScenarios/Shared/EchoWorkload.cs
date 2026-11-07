using System.Text;
using System.Threading.Channels;
using Hex1b;

namespace AotScenarios;

internal sealed class EchoWorkload : IHex1bTerminalWorkloadAdapter
{
    private readonly Channel<ReadOnlyMemory<byte>> _output = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
    private readonly StringBuilder _input = new();

    public bool IsDisposed { get; private set; }
    public string Input => _input.ToString();
    public event Action? Disconnected;

    public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
        => _output.Reader.ReadAsync(ct);

    public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        _input.Append(Encoding.UTF8.GetString(data.Span));
        return _output.Writer.WriteAsync(data.ToArray(), ct);
    }

    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
        => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        _output.Writer.TryComplete();
        Disconnected?.Invoke();
        return ValueTask.CompletedTask;
    }
}
