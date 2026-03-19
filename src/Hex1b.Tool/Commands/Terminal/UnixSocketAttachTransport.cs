using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Hex1b.Diagnostics;

namespace Hex1b.Tool.Commands.Terminal;

/// <summary>
/// Attach transport over Unix domain sockets using the line-based diagnostics protocol.
/// </summary>
internal sealed class UnixSocketAttachTransport : IAttachTransport
{
    private readonly string _socketPath;
    private Socket? _socket;
    private NetworkStream? _networkStream;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public UnixSocketAttachTransport(string socketPath)
    {
        _socketPath = socketPath;
    }

    public async Task<AttachResult> ConnectAsync(CancellationToken ct)
    {
        _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await _socket.ConnectAsync(new UnixDomainSocketEndPoint(_socketPath), ct);
        }
        catch (Exception ex)
        {
            _socket.Dispose();
            _socket = null;
            return new AttachResult(false, 0, 0, false, null, $"Cannot connect: {ex.Message}");
        }

        _networkStream = new NetworkStream(_socket, ownsSocket: true);
        _reader = new StreamReader(_networkStream, Encoding.UTF8);
        _writer = new StreamWriter(_networkStream, Encoding.UTF8) { AutoFlush = true };

        var request = new DiagnosticsRequest { Method = "attach" };
        var requestJson = JsonSerializer.Serialize(request, DiagnosticsJsonOptions.Default);
        await _writer.WriteLineAsync(requestJson.AsMemory(), ct);

        var responseLine = await _reader.ReadLineAsync(ct);
        if (string.IsNullOrEmpty(responseLine))
            return new AttachResult(false, 0, 0, false, null, "Empty response from terminal");

        var response = JsonSerializer.Deserialize<DiagnosticsResponse>(responseLine, DiagnosticsJsonOptions.Default);
        if (response is not { Success: true })
            return new AttachResult(false, 0, 0, false, null, response?.Error ?? "Attach failed");

        return new AttachResult(
            true,
            response.Width ?? 80,
            response.Height ?? 24,
            response.Leader == true,
            response.Data,
            null);
    }

    public async Task SendInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var b64 = Convert.ToBase64String(data.Span);
        await _writer!.WriteLineAsync($"i:{b64}".AsMemory(), ct);
    }

    public async Task SendResizeAsync(int width, int height, CancellationToken ct)
        => await _writer!.WriteLineAsync($"r:{width},{height}".AsMemory(), ct);

    public async Task ClaimLeadAsync(CancellationToken ct)
        => await _writer!.WriteLineAsync("lead".AsMemory(), ct);

    public async Task DetachAsync(CancellationToken ct)
        => await _writer!.WriteLineAsync("detach".AsMemory(), ct);

    public async Task ShutdownAsync(CancellationToken ct)
        => await _writer!.WriteLineAsync("shutdown".AsMemory(), ct);

    public async IAsyncEnumerable<AttachFrame> ReadFramesAsync([EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var line = await _reader!.ReadLineAsync(ct);
            if (line == null)
            {
                yield return AttachFrame.Exit();
                yield break;
            }

            if (line.StartsWith("o:"))
            {
                var bytes = Convert.FromBase64String(line[2..]);
                yield return AttachFrame.Output(bytes);
            }
            else if (line.StartsWith("r:"))
            {
                var parts = line[2..].Split(',');
                if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
                    yield return AttachFrame.Resize(w, h);
            }
            else if (line == "leader:true")
            {
                yield return AttachFrame.LeaderChanged(true);
            }
            else if (line == "leader:false")
            {
                yield return AttachFrame.LeaderChanged(false);
            }
            else if (line == "exit")
            {
                yield return AttachFrame.Exit();
                yield break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _reader?.Dispose();
        if (_writer != null) await _writer.DisposeAsync();
        if (_networkStream != null) await _networkStream.DisposeAsync();
    }
}
