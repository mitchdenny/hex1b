using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Hex1b.Diagnostics;

namespace Hex1b.Tool.Infrastructure;

/// <summary>
/// Client for communicating with Hex1b terminals via Unix domain sockets.
/// Sends DiagnosticsRequest and receives DiagnosticsResponse over the existing protocol.
/// </summary>
internal sealed class TerminalClient
{
    /// <summary>
    /// Sends a request to a terminal at the specified socket path and returns the response.
    /// </summary>
    public async Task<DiagnosticsResponse> SendAsync(string socketPath, DiagnosticsRequest request, CancellationToken cancellationToken = default)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken);

        await using var stream = new NetworkStream(socket, ownsSocket: false);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        var requestJson = JsonSerializer.Serialize(request, DiagnosticsJsonOptions.Default);
        await writer.WriteLineAsync(requestJson.AsMemory(), cancellationToken);

        var responseLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrEmpty(responseLine))
        {
            return new DiagnosticsResponse { Success = false, Error = "Empty response from terminal" };
        }

        return JsonSerializer.Deserialize<DiagnosticsResponse>(responseLine, DiagnosticsJsonOptions.Default)
            ?? new DiagnosticsResponse { Success = false, Error = "Failed to deserialize response" };
    }

    /// <summary>
    /// Probes a terminal socket to check if it's alive. Times out after 3 seconds.
    /// </summary>
    public async Task<DiagnosticsResponse?> TryProbeAsync(string socketPath, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));
            return await SendAsync(socketPath, new DiagnosticsRequest { Method = "info" }, timeoutCts.Token);
        }
        catch
        {
            return null;
        }
    }
}
