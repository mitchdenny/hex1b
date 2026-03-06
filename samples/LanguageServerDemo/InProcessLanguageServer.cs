using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Hex1b.LanguageServer;

namespace LanguageServerDemo;

/// <summary>
/// A minimal in-process LSP server for demo purposes. Runs over Pipe streams
/// so no external processes or sockets are needed. Provides:
/// - Semantic tokens for C# keywords, strings, comments, and numbers
/// - Diagnostics for TODO comments and "undefinedVar" usage
/// - Completion items for common methods
/// </summary>
internal sealed class InProcessLanguageServer : IAsyncDisposable
{
    private readonly Stream _serverInput;
    private readonly Stream _serverOutput;
    private readonly CancellationTokenSource _cts = new();
    private Task? _runLoop;
    private string _documentText = "";

    public Stream ClientInput { get; }
    public Stream ClientOutput { get; }

    public InProcessLanguageServer()
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        _serverInput = clientToServer.Reader.AsStream();
        _serverOutput = serverToClient.Writer.AsStream();
        ClientInput = serverToClient.Reader.AsStream();
        ClientOutput = clientToServer.Writer.AsStream();
    }

    public void Start() => _runLoop = Task.Run(() => RunAsync(_cts.Token));

    /// <summary>Creates a LanguageServerConfiguration wired to this server.</summary>
    public LanguageServerConfiguration CreateConfiguration(string languageId = "csharp")
    {
        var config = new LanguageServerConfiguration();
        config.WithLanguageId(languageId);
        config.DocumentUri = "file:///demo.cs";
        config.WithTransport(ClientInput, ClientOutput);
        return config;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var message = await ReadMessageAsync(ct).ConfigureAwait(false);
                if (message == null) break;
                await HandleAsync(message, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    private async Task HandleAsync(JsonDocument message, CancellationToken ct)
    {
        var root = message.RootElement;
        var method = root.TryGetProperty("method", out var m) ? m.GetString() : null;
        var hasId = root.TryGetProperty("id", out var idEl);

        switch (method)
        {
            case "initialize":
                await RespondAsync(idEl.GetInt32(), new
                {
                    capabilities = new
                    {
                        textDocumentSync = 1,
                        semanticTokensProvider = new
                        {
                            full = true,
                            legend = new
                            {
                                tokenTypes = new[] { "keyword", "type", "string", "comment", "number" },
                                tokenModifiers = Array.Empty<string>(),
                            }
                        },
                        completionProvider = new { triggerCharacters = new[] { "." } },
                    }
                }, ct).ConfigureAwait(false);
                break;

            case "initialized":
                break;

            case "textDocument/didOpen":
                if (root.TryGetProperty("params", out var op))
                {
                    _documentText = op.GetProperty("textDocument").GetProperty("text").GetString() ?? "";
                    var uri = op.GetProperty("textDocument").GetProperty("uri").GetString() ?? "";
                    await PublishDiagnosticsAsync(uri, ct).ConfigureAwait(false);
                }
                break;

            case "textDocument/didChange":
                if (root.TryGetProperty("params", out var cp))
                {
                    var changes = cp.GetProperty("contentChanges");
                    if (changes.GetArrayLength() > 0)
                        _documentText = changes[0].GetProperty("text").GetString() ?? "";
                    var uri = cp.GetProperty("textDocument").GetProperty("uri").GetString() ?? "";
                    await PublishDiagnosticsAsync(uri, ct).ConfigureAwait(false);
                }
                break;

            case "textDocument/semanticTokens/full":
                await RespondAsync(idEl.GetInt32(), new { data = ComputeTokens() }, ct).ConfigureAwait(false);
                break;

            case "textDocument/completion":
                await RespondAsync(idEl.GetInt32(), new
                {
                    isIncomplete = false,
                    items = new object[]
                    {
                        new { label = "ToString", kind = 2, detail = "string object.ToString()" },
                        new { label = "GetHashCode", kind = 2, detail = "int object.GetHashCode()" },
                        new { label = "Equals", kind = 2, detail = "bool object.Equals(object?)" },
                        new { label = "GetType", kind = 2, detail = "Type object.GetType()" },
                        new { label = "Dispose", kind = 2, detail = "void IDisposable.Dispose()" },
                    }
                }, ct).ConfigureAwait(false);
                break;

            case "shutdown":
                if (hasId) await RespondAsync(idEl.GetInt32(), (object?)null, ct).ConfigureAwait(false);
                break;

            case "textDocument/didClose":
            case "exit":
                break;

            default:
                if (hasId)
                    await SendErrorAsync(idEl.GetInt32(), -32601, $"Method not found: {method}", ct).ConfigureAwait(false);
                break;
        }
    }

    private int[] ComputeTokens()
    {
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

        int prevLine = 0, prevChar = 0;

        for (var li = 0; li < lines.Length; li++)
        {
            var line = lines[li];
            var col = 0;
            while (col < line.Length)
            {
                if (char.IsWhiteSpace(line[col])) { col++; continue; }

                if (line[col] == '"')
                {
                    var start = col++;
                    while (col < line.Length && line[col] != '"') col++;
                    if (col < line.Length) col++;
                    AddToken(tokens, li, start, col - start, 2, ref prevLine, ref prevChar);
                    continue;
                }

                if (col + 1 < line.Length && line[col] == '/' && line[col + 1] == '/')
                {
                    var start = col;
                    AddToken(tokens, li, start, line.Length - col, 3, ref prevLine, ref prevChar);
                    col = line.Length;
                    continue;
                }

                if (char.IsLetter(line[col]) || line[col] == '_')
                {
                    var start = col;
                    while (col < line.Length && (char.IsLetterOrDigit(line[col]) || line[col] == '_')) col++;
                    if (keywords.Contains(line[start..col]))
                        AddToken(tokens, li, start, col - start, 0, ref prevLine, ref prevChar);
                    continue;
                }

                if (char.IsDigit(line[col]))
                {
                    var start = col;
                    while (col < line.Length && (char.IsDigit(line[col]) || line[col] == '.')) col++;
                    AddToken(tokens, li, start, col - start, 4, ref prevLine, ref prevChar);
                    continue;
                }

                col++;
            }
        }

        return tokens.ToArray();
    }

    private static void AddToken(List<int> tokens, int line, int col, int len, int type,
        ref int prevLine, ref int prevChar)
    {
        var dLine = line - prevLine;
        var dChar = dLine > 0 ? col : col - prevChar;
        tokens.AddRange([dLine, dChar, len, type, 0]);
        prevLine = line;
        prevChar = col;
    }

    private async Task PublishDiagnosticsAsync(string uri, CancellationToken ct)
    {
        var diags = new List<object>();
        var lines = _documentText.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var todoIdx = lines[i].IndexOf("TODO", StringComparison.Ordinal);
            if (todoIdx >= 0)
                diags.Add(new { range = MakeRange(i, todoIdx, i, todoIdx + 4), severity = 2, message = "TODO comment", source = "demo-server" });

            var errIdx = lines[i].IndexOf("undefinedVar", StringComparison.Ordinal);
            if (errIdx >= 0)
                diags.Add(new { range = MakeRange(i, errIdx, i, errIdx + 12), severity = 1, message = "Undeclared identifier 'undefinedVar'", source = "demo-server" });
        }

        await NotifyAsync("textDocument/publishDiagnostics", new { uri, diagnostics = diags }, ct).ConfigureAwait(false);
    }

    private static object MakeRange(int sl, int sc, int el, int ec) =>
        new { start = new { line = sl, character = sc }, end = new { line = el, character = ec } };

    // ── Message I/O ──────────────────────────────────────────

    private static readonly JsonSerializerOptions s_opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private Task RespondAsync(int id, object? result, CancellationToken ct) =>
        WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new { jsonrpc = "2.0", id, result }, s_opts), ct);

    private Task SendErrorAsync(int id, int code, string message, CancellationToken ct) =>
        WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new { jsonrpc = "2.0", id, error = new { code, message } }, s_opts), ct);

    private Task NotifyAsync(string method, object @params, CancellationToken ct) =>
        WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new { jsonrpc = "2.0", method, @params }, s_opts), ct);

    private async Task WriteAsync(byte[] body, CancellationToken ct)
    {
        await _serverOutput.WriteAsync(Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n"), ct).ConfigureAwait(false);
        await _serverOutput.WriteAsync(body, ct).ConfigureAwait(false);
        await _serverOutput.FlushAsync(ct).ConfigureAwait(false);
    }

    private readonly byte[] _buf = new byte[8192];
    private int _pos, _len;

    private async Task<JsonDocument?> ReadMessageAsync(CancellationToken ct)
    {
        int contentLength = -1;
        while (true)
        {
            var line = await ReadLineAsync(ct).ConfigureAwait(false);
            if (line == null) return null;
            if (line.Length == 0) break;
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                contentLength = int.Parse(line["Content-Length:".Length..].Trim());
        }
        if (contentLength < 0) return null;

        var body = new byte[contentLength];
        var total = 0;
        var buffered = _len - _pos;
        if (buffered > 0)
        {
            var n = Math.Min(buffered, contentLength);
            Buffer.BlockCopy(_buf, _pos, body, 0, n);
            _pos += n;
            total = n;
        }
        while (total < contentLength)
        {
            var r = await _serverInput.ReadAsync(body.AsMemory(total, contentLength - total), ct).ConfigureAwait(false);
            if (r == 0) return null;
            total += r;
        }
        return JsonDocument.Parse(body);
    }

    private async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        var sb = new StringBuilder();
        while (true)
        {
            while (_pos < _len)
            {
                var b = _buf[_pos++];
                if (b == '\n') { var s = sb.ToString(); return s.EndsWith('\r') ? s[..^1] : s; }
                sb.Append((char)b);
            }
            _pos = 0;
            _len = await _serverInput.ReadAsync(_buf, ct).ConfigureAwait(false);
            if (_len == 0) return sb.Length > 0 ? sb.ToString() : null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_runLoop != null) try { await _runLoop.ConfigureAwait(false); } catch { }
        _cts.Dispose();
    }
}
