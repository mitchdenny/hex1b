using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Hex1b.LanguageServer.Protocol;

/// <summary>
/// JSON-RPC 2.0 transport over a pair of streams (stdin/stdout of a language server process).
/// Handles Content-Length framing per LSP specification.
/// All reads go through a single reader loop; requests register a pending TCS.
/// </summary>
internal sealed class JsonRpcTransport : IAsyncDisposable
{
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonRpcResponse>> _pendingRequests = new();
    private readonly SemaphoreSlim _readerLock = new(1, 1);
    private bool _readerLoopRunning;
    private readonly CancellationTokenSource _cts = new();
    private int _nextId;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Creates a transport over the given input (server stdout) and output (server stdin) streams.
    /// </summary>
    public JsonRpcTransport(Stream input, Stream output)
    {
        _input = input;
        _output = output;
    }

    /// <summary>Sends a request and returns the response.</summary>
    public async Task<JsonRpcResponse> SendRequestAsync(string method, object? @params, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[id] = tcs;

        using var reg = ct.Register(() => tcs.TrySetCanceled());

        var request = new JsonRpcRequest { Id = id, Method = method, Params = @params };
        await WriteMessageAsync(JsonSerializer.SerializeToUtf8Bytes(request, s_jsonOptions), ct).ConfigureAwait(false);

        // If the reader loop isn't running yet (pre-initialization), pump messages inline
        if (!_readerLoopRunning)
        {
            while (!tcs.Task.IsCompleted)
            {
                var msg = await ReadMessageAsync(ct).ConfigureAwait(false);
                if (msg == null) throw new InvalidOperationException("Transport closed while waiting for response");
                DispatchMessage(msg);
            }
        }

        return await tcs.Task.ConfigureAwait(false);
    }

    /// <summary>Sends a notification (no response expected).</summary>
    public async Task SendNotificationAsync(string method, object? @params, CancellationToken ct = default)
    {
        var notification = new JsonRpcNotification { Method = method, Params = @params };
        await WriteMessageAsync(JsonSerializer.SerializeToUtf8Bytes(notification, s_jsonOptions), ct).ConfigureAwait(false);
    }

    /// <summary>Reads the next message from the transport.</summary>
    public async Task<JsonRpcResponse?> ReadMessageAsync(CancellationToken ct = default)
    {
        var body = await ReadFrameAsync(ct).ConfigureAwait(false);
        if (body == null) return null;

        return JsonSerializer.Deserialize<JsonRpcResponse>(body, s_jsonOptions);
    }

    /// <summary>Raised when a server-initiated notification arrives during request processing.</summary>
    public event Action<JsonRpcResponse>? NotificationReceived;

    /// <summary>
    /// Starts a background loop that reads all incoming messages and dispatches them.
    /// Responses go to pending request TCS; notifications fire the event.
    /// Returns a task that completes when the stream closes.
    /// </summary>
    public async Task RunNotificationLoopAsync(CancellationToken ct = default)
    {
        _readerLoopRunning = true;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var msg = await ReadMessageAsync(ct).ConfigureAwait(false);
                if (msg == null) break;
                DispatchMessage(msg);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (Exception)
        {
            // Deserialization or other errors — don't crash the loop silently
        }
        finally
        {
            _readerLoopRunning = false;
            // Complete any pending requests with cancellation
            foreach (var kvp in _pendingRequests)
            {
                kvp.Value.TrySetCanceled();
                _pendingRequests.TryRemove(kvp.Key, out _);
            }
        }
    }

    /// <summary>
    /// Raised when the server sends a request (has both id and method).
    /// The handler should return the result to send back to the server.
    /// </summary>
    public event Func<string, JsonElement?, Task<object?>>? ServerRequestReceived;

    private void DispatchMessage(JsonRpcResponse msg)
    {
        if (msg.Id.HasValue && msg.Method != null)
        {
            // Server-to-client request — needs a response
            _ = Task.Run(async () =>
            {
                object? result = null;
                try
                {
                    if (ServerRequestReceived != null)
                        result = await ServerRequestReceived.Invoke(msg.Method, msg.Params);
                }
                catch { }

                var response = new { jsonrpc = "2.0", id = msg.Id.Value, result };
                await WriteMessageAsync(JsonSerializer.SerializeToUtf8Bytes(response, s_jsonOptions), CancellationToken.None)
                    .ConfigureAwait(false);
            });
        }
        else if (msg.Id.HasValue && _pendingRequests.TryRemove(msg.Id.Value, out var tcs))
        {
            tcs.TrySetResult(msg);
        }
        else if (msg.IsNotification)
        {
            NotificationReceived?.Invoke(msg);
        }
    }

    // ── Framing ──────────────────────────────────────────────

    private async Task WriteMessageAsync(byte[] body, CancellationToken ct)
    {
        var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _output.WriteAsync(header, ct).ConfigureAwait(false);
            await _output.WriteAsync(body, ct).ConfigureAwait(false);
            await _output.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<byte[]?> ReadFrameAsync(CancellationToken ct)
    {
        // Read headers until empty line
        var contentLength = -1;
        while (true)
        {
            var line = await ReadLineAsync(ct).ConfigureAwait(false);
            if (line == null) return null; // Stream closed

            if (line.Length == 0)
                break; // End of headers

            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                var value = line["Content-Length:".Length..].Trim();
                contentLength = int.Parse(value);
            }
        }

        if (contentLength < 0)
            throw new InvalidOperationException("Missing Content-Length header");

        // Read body — consume from internal buffer first, then from stream
        var body = new byte[contentLength];
        var totalRead = 0;

        // Drain any leftover bytes from the line buffer
        var buffered = _lineBufferLen - _lineBufferPos;
        if (buffered > 0)
        {
            var toCopy = Math.Min(buffered, contentLength);
            Buffer.BlockCopy(_lineBuffer, _lineBufferPos, body, 0, toCopy);
            _lineBufferPos += toCopy;
            totalRead = toCopy;
        }

        while (totalRead < contentLength)
        {
            var read = await _input.ReadAsync(body.AsMemory(totalRead, contentLength - totalRead), ct).ConfigureAwait(false);
            if (read == 0) return null; // Stream closed
            totalRead += read;
        }

        return body;
    }

    private readonly byte[] _lineBuffer = new byte[4096];
    private int _lineBufferPos;
    private int _lineBufferLen;

    private async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        var sb = new StringBuilder();
        while (true)
        {
            // Check buffer for newline
            while (_lineBufferPos < _lineBufferLen)
            {
                var b = _lineBuffer[_lineBufferPos++];
                if (b == '\n')
                {
                    // Strip trailing \r
                    var result = sb.ToString();
                    if (result.EndsWith('\r'))
                        result = result[..^1];
                    return result;
                }
                sb.Append((char)b);
            }

            // Need more data
            _lineBufferPos = 0;
            _lineBufferLen = await _input.ReadAsync(_lineBuffer, ct).ConfigureAwait(false);
            if (_lineBufferLen == 0) return sb.Length > 0 ? sb.ToString() : null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        _writeLock.Dispose();
        _cts.Dispose();
    }
}
