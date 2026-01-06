using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;

namespace Hex1b.Analyzer;

/// <summary>
/// SignalR hub for real-time terminal streaming to Blazor clients.
/// </summary>
public class TerminalHub : Hub
{
    private readonly BlazorPresentationAdapterHolder _adapterHolder;

    /// <summary>
    /// Creates a new TerminalHub instance.
    /// </summary>
    /// <param name="adapterHolder">The Blazor adapter holder.</param>
    public TerminalHub(BlazorPresentationAdapterHolder adapterHolder)
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
            // Send initial dimensions
            var (cols, rows) = adapter.GetDimensions();
            await Clients.Caller.SendAsync("SetDimensions", cols, rows);

            // Send buffered output for state sync
            var bufferedOutput = adapter.GetBufferedOutput();
            if (!string.IsNullOrEmpty(bufferedOutput))
            {
                await Clients.Caller.SendAsync("WriteOutput", bufferedOutput);
            }
        }

        await base.OnConnectedAsync();
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
    /// Handles resize from the client.
    /// </summary>
    /// <param name="cols">New column count.</param>
    /// <param name="rows">New row count.</param>
    public void Resize(int cols, int rows)
    {
        var adapter = _adapterHolder.Adapter;
        adapter?.HandleResize(cols, rows);
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
            else if (message.Type == TerminalMessageType.Dimensions)
            {
                // Send dimensions update through regular method
                var (cols, rows) = adapter.GetDimensions();
                await Clients.Caller.SendAsync("SetDimensions", cols, rows, cancellationToken);
            }
        }
    }
}

/// <summary>
/// Holder for the Blazor presentation adapter, registered as a singleton.
/// </summary>
public class BlazorPresentationAdapterHolder
{
    private volatile BlazorPresentationAdapter? _adapter;

    /// <summary>
    /// The Blazor presentation adapter instance.
    /// </summary>
    public BlazorPresentationAdapter? Adapter
    {
        get => _adapter;
        set => _adapter = value;
    }
}
