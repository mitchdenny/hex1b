using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;

namespace Hex1b.Analyzer;

/// <summary>
/// SignalR hub for real-time terminal streaming to web clients.
/// </summary>
public class TerminalHub : Hub
{
    private readonly FollowingPresentationAdapterHolder _adapterHolder;

    /// <summary>
    /// Creates a new TerminalHub instance.
    /// </summary>
    /// <param name="adapterHolder">The following adapter holder.</param>
    public TerminalHub(FollowingPresentationAdapterHolder adapterHolder)
    {
        _adapterHolder = adapterHolder;
    }

    /// <summary>
    /// Called when a client connects. Sends the current terminal state.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var adapter = _adapterHolder.Adapter;
        if (adapter != null)
        {
            // Register this client for resize events
            var client = adapter.RegisterClient(Context.ConnectionId);
            Context.Items["ClientConnection"] = client;

            // Send initial dimensions
            var (cols, rows) = adapter.GetDimensions();
            await Clients.Caller.SendAsync("SetDimensions", cols, rows);

            // Pause I/O and get buffered output for state sync
            var bufferedOutput = adapter.GetBufferedOutputWithPause();
            if (!string.IsNullOrEmpty(bufferedOutput))
            {
                await Clients.Caller.SendAsync("WriteOutput", bufferedOutput);
            }

            // Start streaming resize events to this client in the background
            // The task is tracked via the ConnectionAborted token which will cancel when client disconnects
            _ = Task.Run(() => StreamResizeEventsAsync(client, Context.ConnectionAborted));
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects.
    /// </summary>
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var adapter = _adapterHolder.Adapter;
        if (adapter != null && Context.Items.TryGetValue("ClientConnection", out var clientObj) 
            && clientObj is ClientConnection client)
        {
            adapter.UnregisterClient(client);
        }

        return base.OnDisconnectedAsync(exception);
    }

    private async Task StreamResizeEventsAsync(ClientConnection client, CancellationToken ct)
    {
        try
        {
            await foreach (var (width, height) in client.GetResizeEventsAsync(ct))
            {
                await Clients.Client(client.ConnectionId).SendAsync("SetDimensions", width, height, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
        }
    }

    /// <summary>
    /// Sends input from the client to the terminal.
    /// </summary>
    /// <param name="data">The input data.</param>
    public void SendInput(string data)
    {
        var adapter = _adapterHolder.Adapter;
        adapter?.SendInput(data);
    }

    /// <summary>
    /// Streams terminal output to the connected client.
    /// </summary>
    public async IAsyncEnumerable<string> StreamOutput([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var adapter = _adapterHolder.Adapter;
        if (adapter == null) yield break;

        await foreach (var message in adapter.GetOutputStreamAsync(cancellationToken))
        {
            if (message.Type == TerminalMessageType.Output)
            {
                yield return message.Data;
            }
        }
    }
}

/// <summary>
/// Holder for the FollowingPresentationAdapter, registered as a singleton.
/// </summary>
public class FollowingPresentationAdapterHolder
{
    private volatile FollowingPresentationAdapter? _adapter;

    /// <summary>
    /// The following presentation adapter instance.
    /// </summary>
    public FollowingPresentationAdapter? Adapter
    {
        get => _adapter;
        set => _adapter = value;
    }
}
