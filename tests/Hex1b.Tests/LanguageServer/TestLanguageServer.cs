using System.IO.Pipelines;
using System.Text;
using System.Text.Json;

namespace Hex1b.Tests.LanguageServer;

/// <summary>
/// A minimal in-process LSP server for testing. Communicates via paired streams
/// (no sockets or processes needed). Supports:
/// - initialize/initialized handshake
/// - textDocument/didOpen, textDocument/didChange
/// - textDocument/semanticTokens/full (hardcoded C# keyword tokens)
/// - textDocument/publishDiagnostics (pushed after didOpen/didChange for "TODO" patterns)
/// - textDocument/completion (returns hardcoded items)
/// </summary>
internal sealed class TestLanguageServer : IAsyncDisposable
{
    private readonly Stream _serverInput;
    private readonly Stream _serverOutput;
    private readonly CancellationTokenSource _cts = new();
    private Task? _runLoop;
    private string _documentText = "";

    /// <summary>Client reads from this stream (server's stdout).</summary>
    public Stream ClientInput { get; }

    /// <summary>Client writes to this stream (server's stdin).</summary>
    public Stream ClientOutput { get; }

    public TestLanguageServer()
    {
        // Create two pipes: client→server and server→client
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        // Server reads from client→server pipe, writes to server→client pipe
        _serverInput = clientToServer.Reader.AsStream();
        _serverOutput = serverToClient.Writer.AsStream();

        // Client reads from server→client pipe, writes to client→server pipe
        ClientInput = serverToClient.Reader.AsStream();
        ClientOutput = clientToServer.Writer.AsStream();
    }

    /// <summary>Starts the server message processing loop.</summary>
    public void Start()
    {
        _runLoop = Task.Run(() => RunAsync(_cts.Token));
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var reader = new StreamReader(_serverInput, Encoding.UTF8);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var message = await ReadMessageAsync(ct).ConfigureAwait(false);
                if (message == null) break;

                await HandleMessageAsync(message, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    private async Task HandleMessageAsync(JsonDocument message, CancellationToken ct)
    {
        var root = message.RootElement;
        var method = root.TryGetProperty("method", out var m) ? m.GetString() : null;
        var hasId = root.TryGetProperty("id", out var idElement);

        switch (method)
        {
            case "initialize":
                await SendResponseAsync(idElement.GetInt32(), new
                {
                    capabilities = new
                    {
                        textDocumentSync = 1, // Full sync
                        semanticTokensProvider = new
                        {
                            full = true,
                            legend = new
                            {
                                tokenTypes = new[] { "keyword", "type", "string", "comment", "number", "variable", "function", "namespace" },
                                tokenModifiers = Array.Empty<string>(),
                            }
                        },
                        completionProvider = new
                        {
                            triggerCharacters = new[] { "." },
                        },
                    }
                }, ct).ConfigureAwait(false);
                break;

            case "initialized":
                // No response needed
                break;

            case "textDocument/didOpen":
                if (root.TryGetProperty("params", out var openParams))
                {
                    var text = openParams.GetProperty("textDocument").GetProperty("text").GetString() ?? "";
                    _documentText = text;
                    var uri = openParams.GetProperty("textDocument").GetProperty("uri").GetString() ?? "";
                    await PublishDiagnosticsAsync(uri, ct).ConfigureAwait(false);
                }
                break;

            case "textDocument/didChange":
                if (root.TryGetProperty("params", out var changeParams))
                {
                    var changes = changeParams.GetProperty("contentChanges");
                    if (changes.GetArrayLength() > 0)
                        _documentText = changes[0].GetProperty("text").GetString() ?? "";

                    var uri = changeParams.GetProperty("textDocument").GetProperty("uri").GetString() ?? "";
                    await PublishDiagnosticsAsync(uri, ct).ConfigureAwait(false);
                }
                break;

            case "textDocument/semanticTokens/full":
                var tokens = ComputeSemanticTokens();
                await SendResponseAsync(idElement.GetInt32(), new { data = tokens }, ct).ConfigureAwait(false);
                break;

            case "textDocument/completion":
                var items = GetCompletionItems();
                await SendResponseAsync(idElement.GetInt32(), new { isIncomplete = false, items }, ct).ConfigureAwait(false);
                break;

            case "textDocument/didClose":
                break;

            case "shutdown":
                await SendResponseAsync(idElement.GetInt32(), (object?)null, ct).ConfigureAwait(false);
                break;

            case "exit":
                break;

            default:
                if (hasId)
                {
                    // Unknown request — send error
                    await SendErrorAsync(idElement.GetInt32(), -32601, $"Method not found: {method}", ct).ConfigureAwait(false);
                }
                break;
        }
    }

    // ── Semantic tokens ──────────────────────────────────────

    private int[] ComputeSemanticTokens()
    {
        // Simple keyword scanner for testing
        var tokens = new List<int>();
        var lines = _documentText.Split('\n');
        var keywords = new HashSet<string>
        {
            "using", "namespace", "class", "public", "private", "protected", "internal",
            "sealed", "static", "void", "int", "string", "var", "return", "new", "if",
            "else", "for", "foreach", "while", "async", "await", "throw", "try", "catch",
            "readonly", "const", "override", "virtual", "abstract", "interface", "record",
            "struct", "enum", "true", "false", "null", "this", "base", "in", "out", "ref",
            "bool", "byte", "char", "double", "float", "long", "short", "object", "Task",
        };

        var prevLine = 0;
        var prevChar = 0;

        for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            var line = lines[lineIdx];
            var col = 0;

            while (col < line.Length)
            {
                // Skip whitespace
                if (char.IsWhiteSpace(line[col])) { col++; continue; }

                // String literal
                if (line[col] == '"')
                {
                    var start = col;
                    col++;
                    while (col < line.Length && line[col] != '"') col++;
                    if (col < line.Length) col++; // closing quote

                    var deltaLine = lineIdx - prevLine;
                    var deltaChar = deltaLine > 0 ? start : start - prevChar;
                    tokens.AddRange([deltaLine, deltaChar, col - start, 2, 0]); // tokenType 2 = string
                    prevLine = lineIdx;
                    prevChar = start;
                    continue;
                }

                // Comment
                if (col + 1 < line.Length && line[col] == '/' && line[col + 1] == '/')
                {
                    var start = col;
                    var length = line.Length - col;

                    var deltaLine = lineIdx - prevLine;
                    var deltaChar = deltaLine > 0 ? start : start - prevChar;
                    tokens.AddRange([deltaLine, deltaChar, length, 3, 0]); // tokenType 3 = comment
                    prevLine = lineIdx;
                    prevChar = start;
                    col = line.Length;
                    continue;
                }

                // Identifier or keyword
                if (char.IsLetter(line[col]) || line[col] == '_')
                {
                    var start = col;
                    while (col < line.Length && (char.IsLetterOrDigit(line[col]) || line[col] == '_')) col++;
                    var word = line[start..col];

                    if (keywords.Contains(word))
                    {
                        var deltaLine = lineIdx - prevLine;
                        var deltaChar = deltaLine > 0 ? start : start - prevChar;
                        tokens.AddRange([deltaLine, deltaChar, word.Length, 0, 0]); // tokenType 0 = keyword
                        prevLine = lineIdx;
                        prevChar = start;
                    }
                    continue;
                }

                // Number
                if (char.IsDigit(line[col]))
                {
                    var start = col;
                    while (col < line.Length && (char.IsDigit(line[col]) || line[col] == '.')) col++;

                    var deltaLine = lineIdx - prevLine;
                    var deltaChar = deltaLine > 0 ? start : start - prevChar;
                    tokens.AddRange([deltaLine, deltaChar, col - start, 4, 0]); // tokenType 4 = number
                    prevLine = lineIdx;
                    prevChar = start;
                    continue;
                }

                col++;
            }
        }

        return tokens.ToArray();
    }

    // ── Diagnostics ──────────────────────────────────────────

    private async Task PublishDiagnosticsAsync(string uri, CancellationToken ct)
    {
        var diagnostics = new List<object>();
        var lines = _documentText.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var todoIdx = line.IndexOf("TODO", StringComparison.Ordinal);
            if (todoIdx >= 0)
            {
                diagnostics.Add(new
                {
                    range = new
                    {
                        start = new { line = i, character = todoIdx },
                        end = new { line = i, character = todoIdx + 4 },
                    },
                    severity = 2, // Warning
                    message = "TODO comment found",
                    source = "test-server",
                });
            }

            var errorIdx = line.IndexOf("undefinedVar", StringComparison.Ordinal);
            if (errorIdx >= 0)
            {
                diagnostics.Add(new
                {
                    range = new
                    {
                        start = new { line = i, character = errorIdx },
                        end = new { line = i, character = errorIdx + 12 },
                    },
                    severity = 1, // Error
                    message = "Use of undeclared identifier 'undefinedVar'",
                    source = "test-server",
                });
            }
        }

        await SendNotificationAsync("textDocument/publishDiagnostics", new
        {
            uri,
            diagnostics,
        }, ct).ConfigureAwait(false);
    }

    // ── Completions ──────────────────────────────────────────

    private static object[] GetCompletionItems() =>
    [
        new { label = "ToString", kind = 2, detail = "string object.ToString()" },
        new { label = "GetHashCode", kind = 2, detail = "int object.GetHashCode()" },
        new { label = "Equals", kind = 2, detail = "bool object.Equals(object?)" },
        new { label = "GetType", kind = 2, detail = "Type object.GetType()" },
    ];

    // ── Message I/O ──────────────────────────────────────────

    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private async Task SendResponseAsync(int id, object? result, CancellationToken ct)
    {
        var response = new { jsonrpc = "2.0", id, result };
        await WriteMessageAsync(JsonSerializer.SerializeToUtf8Bytes(response, s_options), ct).ConfigureAwait(false);
    }

    private async Task SendErrorAsync(int id, int code, string message, CancellationToken ct)
    {
        var response = new { jsonrpc = "2.0", id, error = new { code, message } };
        await WriteMessageAsync(JsonSerializer.SerializeToUtf8Bytes(response, s_options), ct).ConfigureAwait(false);
    }

    private async Task SendNotificationAsync(string method, object @params, CancellationToken ct)
    {
        var notification = new { jsonrpc = "2.0", method, @params };
        await WriteMessageAsync(JsonSerializer.SerializeToUtf8Bytes(notification, s_options), ct).ConfigureAwait(false);
    }

    private async Task WriteMessageAsync(byte[] body, CancellationToken ct)
    {
        var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        await _serverOutput.WriteAsync(header, ct).ConfigureAwait(false);
        await _serverOutput.WriteAsync(body, ct).ConfigureAwait(false);
        await _serverOutput.FlushAsync(ct).ConfigureAwait(false);
    }

    private readonly byte[] _readBuffer = new byte[8192];
    private int _readPos;
    private int _readLen;

    private async Task<JsonDocument?> ReadMessageAsync(CancellationToken ct)
    {
        // Read headers
        var contentLength = -1;
        while (true)
        {
            var line = await ReadLineAsync(ct).ConfigureAwait(false);
            if (line == null) return null;
            if (line.Length == 0) break;

            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                contentLength = int.Parse(line["Content-Length:".Length..].Trim());
        }

        if (contentLength < 0) return null;

        // Read body
        var body = new byte[contentLength];
        var totalRead = 0;
        while (totalRead < contentLength)
        {
            // Use buffered data first
            if (_readPos < _readLen)
            {
                var available = Math.Min(_readLen - _readPos, contentLength - totalRead);
                Buffer.BlockCopy(_readBuffer, _readPos, body, totalRead, available);
                _readPos += available;
                totalRead += available;
            }
            else
            {
                var read = await _serverInput.ReadAsync(body.AsMemory(totalRead, contentLength - totalRead), ct).ConfigureAwait(false);
                if (read == 0) return null;
                totalRead += read;
            }
        }

        return JsonDocument.Parse(body);
    }

    private async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        var sb = new StringBuilder();
        while (true)
        {
            while (_readPos < _readLen)
            {
                var b = _readBuffer[_readPos++];
                if (b == '\n')
                {
                    var result = sb.ToString();
                    return result.EndsWith('\r') ? result[..^1] : result;
                }
                sb.Append((char)b);
            }

            _readPos = 0;
            _readLen = await _serverInput.ReadAsync(_readBuffer, ct).ConfigureAwait(false);
            if (_readLen == 0) return sb.Length > 0 ? sb.ToString() : null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_runLoop != null)
        {
            try { await _runLoop.ConfigureAwait(false); } catch { }
        }
        _cts.Dispose();
    }
}
