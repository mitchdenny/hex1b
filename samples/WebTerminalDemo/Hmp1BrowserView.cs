using Hex1b;

namespace WebTerminalDemo;

internal sealed class Hmp1BrowserView : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop;
    private readonly Stream _serverStream;
    private readonly Stream _clientStream;
    private readonly Hmp1WorkloadAdapter _client;
    private Hmp1ClientHandle? _peer;
    private Hex1bTerminal? _replica;
    private int _disposed;

    private Hmp1BrowserView(string? name, CancellationToken ct)
    {
        _stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        (_serverStream, _clientStream) = DuplexPipeStream.CreatePair();
        _client = new Hmp1WorkloadAdapter(new Hmp1ClientOptions
        {
            StreamFactory = _ => Task.FromResult(_clientStream),
            DisplayName = name,
            DefaultRole = Hmp1Role.Secondary
        });
    }

    internal Hwt1PresentationAdapter Presentation { get; } = new();

    internal static async Task<Hmp1BrowserView> CreateAsync(
        Hmp1PresentationAdapter producer, string? name, CancellationToken ct)
    {
        var view = new Hmp1BrowserView(name, ct);
        if (producer.ReflowEnabled)
            view.Presentation.WithReflow(producer);
        var connected = false;
        try
        {
            // Both halves use real HMP1 frames and bounded pipes. Cancel the other
            // handshake if either fails, and observe both before releasing resources.
            await Task.WhenAll(
                view.HandshakeAsync(async () => view._peer = await producer.AddClient(view._serverStream, view._stop.Token)),
                view.HandshakeAsync(() => view._client.ConnectAsync(view._stop.Token)));
            view._replica = Hex1bTerminal.CreateBuilder()
                .WithWorkload(view._client)
                .WithPresentation(view.Presentation)
                .WithScrollback(1000)
                .Build();
            connected = true;
            return view;
        }
        finally
        {
            if (!connected)
                await view.DisposeAsync();
        }
    }

    private async Task HandshakeAsync(Func<Task> handshake)
    {
        var completed = false;
        try
        {
            await handshake();
            completed = true;
        }
        finally
        {
            if (!completed)
                await _stop.CancelAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        using var stop = _stop;
        await using var serverStream = _serverStream;
        await using var clientStream = _clientStream;
        await using var presentation = Presentation;
        await using var client = _client;
        await using var peer = _peer;
        await using var replica = _replica;
        await stop.CancelAsync();
    }
}
