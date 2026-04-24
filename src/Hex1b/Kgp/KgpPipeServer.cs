using System.IO.Pipes;
using System.Text;
using Hex1b.Tokens;

namespace Hex1b.Kgp;

/// <summary>
/// Server-side of the KGP side-channel pipe. Created by the parent terminal
/// to receive KGP image data from child processes that can't send APC
/// sequences through ConPTY.
/// </summary>
public sealed class KgpPipeServer : IAsyncDisposable
{
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cts = new();
    private Task? _listenTask;
    private Action<KgpToken>? _tokenHandler;

    /// <summary>
    /// The pipe name to pass to child processes via HEX1B_KGP_PIPE env var.
    /// </summary>
    public string PipeName => _pipeName;

    public KgpPipeServer()
    {
        _pipeName = $"hex1b-kgp-{Environment.ProcessId}-{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Sets the handler called when KGP tokens arrive from child processes.
    /// </summary>
    public void SetTokenHandler(Action<KgpToken> handler)
    {
        _tokenHandler = handler;
    }

    /// <summary>
    /// Starts listening for KGP data from child processes.
    /// </summary>
    public void Start()
    {
        _listenTask = ListenAsync(_cts.Token);
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.In,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(ct);
                _ = HandleClientAsync(pipe, ct);
            }
            catch (OperationCanceledException)
            {
                await pipe.DisposeAsync();
                break;
            }
            catch
            {
                await pipe.DisposeAsync();
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        try
        {
            var buffer = new byte[64 * 1024];
            var accum = new StringBuilder();

            while (!ct.IsCancellationRequested)
            {
                int bytesRead = await pipe.ReadAsync(buffer, ct);
                if (bytesRead == 0)
                    break;

                accum.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                ProcessAccumulatedData(accum);
            }
        }
        catch (OperationCanceledException) { }
        catch { }
        finally
        {
            await pipe.DisposeAsync();
        }
    }

    private void ProcessAccumulatedData(StringBuilder accum)
    {
        var text = accum.ToString();
        var tokens = AnsiTokenizer.Tokenize(text);

        int lastConsumedEnd = 0;
        foreach (var token in tokens)
        {
            if (token is KgpToken kgpToken)
            {
                _tokenHandler?.Invoke(kgpToken);
            }
            var serialized = AnsiTokenSerializer.Serialize([token]);
            lastConsumedEnd += serialized.Length;
        }

        if (lastConsumedEnd > 0 && lastConsumedEnd <= accum.Length)
        {
            accum.Remove(0, lastConsumedEnd);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _cts.Dispose();

        if (_listenTask != null)
        {
            try { await _listenTask.WaitAsync(TimeSpan.FromSeconds(2)); }
            catch { }
        }
    }
}
