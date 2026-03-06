using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using Hex1b.LanguageServer.Protocol;

namespace Hex1b.LanguageServer;

/// <summary>
/// Manages the lifecycle of a language server connection: process spawning (stdio)
/// or socket connection, initialize handshake, document sync, and shutdown.
/// </summary>
internal sealed class LanguageServerClient : IAsyncDisposable
{
    private readonly LanguageServerConfiguration _config;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private JsonRpcTransport? _transport;
    private Process? _process;
    private Socket? _socket;
    private NetworkStream? _networkStream;
    private CancellationTokenSource? _notificationCts;
    private Task? _notificationLoop;
    private ServerCapabilities? _serverCapabilities;
    private readonly Dictionary<string, int> _documentVersions = new();

    public LanguageServerClient(LanguageServerConfiguration config)
    {
        _config = config;
    }

    /// <summary>The server capabilities received during initialization.</summary>
    public ServerCapabilities? ServerCapabilities => _serverCapabilities;

    /// <summary>Raised when the server sends a notification (e.g., diagnostics).</summary>
    public event Action<JsonRpcResponse>? NotificationReceived;

    /// <summary>
    /// Waits until the client has completed the initialize/initialized handshake.
    /// Safe to call concurrently — returns immediately if already ready.
    /// </summary>
    public Task WaitUntilReadyAsync(CancellationToken ct = default) =>
        _ready.Task.WaitAsync(ct);

    /// <summary>
    /// Starts the language server and performs the initialize/initialized handshake.
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        // Establish transport
        if (_config.SocketPath != null)
        {
            _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await _socket.ConnectAsync(new UnixDomainSocketEndPoint(_config.SocketPath), ct).ConfigureAwait(false);
            _networkStream = new NetworkStream(_socket, ownsSocket: false);
            _transport = new JsonRpcTransport(_networkStream, _networkStream);
        }
        else if (_config.ServerCommand != null)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _config.ServerCommand,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            if (_config.WorkingDirectory != null)
                psi.WorkingDirectory = _config.WorkingDirectory;

            if (_config.ServerArguments != null)
            {
                foreach (var arg in _config.ServerArguments)
                    psi.ArgumentList.Add(arg);
            }

            _process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start language server process");
            _transport = new JsonRpcTransport(_process.StandardOutput.BaseStream, _process.StandardInput.BaseStream);

            // Drain stderr to prevent pipe buffer deadlocks — language servers
            // like csharp-ls write verbose logs to stderr that can fill the OS
            // pipe buffer and block the process.
            _ = Task.Run(async () =>
            {
                try
                {
                    var buf = new byte[4096];
                    while (await _process.StandardError.BaseStream.ReadAsync(buf).ConfigureAwait(false) > 0) { }
                }
                catch { }
            });
        }
        else if (_config.Transport != null)
        {
            _transport = _config.Transport;
        }
        else
        {
            throw new InvalidOperationException("No server command, socket path, or transport configured");
        }

        // Wire up notification forwarding
        _transport.NotificationReceived += msg => NotificationReceived?.Invoke(msg);

        // Initialize handshake
        var initParams = new InitializeParams
        {
            ProcessId = Environment.ProcessId,
            RootUri = _config.RootUri,
            Capabilities = new ClientCapabilities
            {
                TextDocument = new TextDocumentClientCapabilities
                {
                    SemanticTokens = new SemanticTokensClientCapabilities(),
                    Completion = new CompletionClientCapabilities(),
                    PublishDiagnostics = new PublishDiagnosticsClientCapabilities(),
                }
            }
        };

        // Include workspace folders if we have a root URI
        if (_config.RootUri != null)
        {
            var folderName = Path.GetFileName(_config.WorkingDirectory ?? "workspace") ?? "workspace";
            initParams.WorkspaceFolders = [new WorkspaceFolder { Uri = _config.RootUri, Name = folderName }];
        }

        var response = await _transport.SendRequestAsync("initialize", initParams, ct).ConfigureAwait(false);
        if (response.Error != null)
            throw new InvalidOperationException($"LSP initialize failed: {response.Error.Message}");

        if (response.Result.HasValue)
        {
            var result = JsonSerializer.Deserialize<InitializeResult>(response.Result.Value.GetRawText());
            _serverCapabilities = result?.Capabilities;
        }

        await _transport.SendNotificationAsync("initialized", new { }, ct).ConfigureAwait(false);

        // Start notification listener
        _notificationCts = new CancellationTokenSource();
        _notificationLoop = Task.Run(() => _transport.RunNotificationLoopAsync(_notificationCts.Token));

        _ready.TrySetResult();
    }

    /// <summary>Sends textDocument/didOpen notification.</summary>
    public async Task OpenDocumentAsync(string documentUri, string languageId, string text, CancellationToken ct = default)
    {
        if (_transport == null) throw new InvalidOperationException("Client not started");

        _documentVersions[documentUri] = 1;
        await _transport.SendNotificationAsync("textDocument/didOpen", new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = documentUri,
                LanguageId = languageId,
                Version = 1,
                Text = text,
            }
        }, ct).ConfigureAwait(false);
    }

    /// <summary>Sends textDocument/didChange notification (full document sync).</summary>
    public async Task ChangeDocumentAsync(string documentUri, string text, CancellationToken ct = default)
    {
        if (_transport == null) throw new InvalidOperationException("Client not started");

        var version = _documentVersions.GetValueOrDefault(documentUri) + 1;
        _documentVersions[documentUri] = version;
        await _transport.SendNotificationAsync("textDocument/didChange", new DidChangeTextDocumentParams
        {
            TextDocument = new VersionedTextDocumentIdentifier
            {
                Uri = documentUri,
                Version = version,
            },
            ContentChanges = [new TextDocumentContentChangeEvent { Text = text }]
        }, ct).ConfigureAwait(false);
    }

    /// <summary>Requests textDocument/semanticTokens/full.</summary>
    public async Task<SemanticTokensResult?> RequestSemanticTokensAsync(string documentUri, CancellationToken ct = default)
    {
        if (_transport == null) return null;

        var response = await _transport.SendRequestAsync("textDocument/semanticTokens/full",
            new SemanticTokensParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri }
            }, ct).ConfigureAwait(false);

        if (response.Error != null || !response.Result.HasValue) return null;

        return JsonSerializer.Deserialize<SemanticTokensResult>(response.Result.Value.GetRawText());
    }

    /// <summary>Requests textDocument/completion.</summary>
    public async Task<CompletionList?> RequestCompletionAsync(string documentUri, int line, int character, CancellationToken ct = default)
    {
        if (_transport == null) return null;

        var response = await _transport.SendRequestAsync("textDocument/completion",
            new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri },
                Position = new LspPosition { Line = line, Character = character },
            }, ct).ConfigureAwait(false);

        if (response.Error != null || !response.Result.HasValue) return null;

        var raw = response.Result.Value;
        // Response can be CompletionList or CompletionItem[]
        if (raw.ValueKind == JsonValueKind.Array)
        {
            var items = JsonSerializer.Deserialize<CompletionItem[]>(raw.GetRawText());
            return new CompletionList { Items = items ?? [] };
        }

        return JsonSerializer.Deserialize<CompletionList>(raw.GetRawText());
    }

    /// <summary>Sends textDocument/didClose for all open documents and shutdown/exit.</summary>
    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_transport == null) return;

        try
        {
            foreach (var uri in _documentVersions.Keys)
            {
                await _transport.SendNotificationAsync("textDocument/didClose", new DidCloseTextDocumentParams
                {
                    TextDocument = new TextDocumentIdentifier { Uri = uri }
                }, ct).ConfigureAwait(false);
            }

            await _transport.SendRequestAsync("shutdown", null, ct).ConfigureAwait(false);
            await _transport.SendNotificationAsync("exit", null, ct).ConfigureAwait(false);
        }
        catch (IOException) { }
        catch (InvalidOperationException) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_notificationCts != null)
        {
            await _notificationCts.CancelAsync().ConfigureAwait(false);
            _notificationCts.Dispose();
        }

        if (_notificationLoop != null)
        {
            try { await _notificationLoop.ConfigureAwait(false); } catch { }
        }

        if (_transport != null)
            await _transport.DisposeAsync().ConfigureAwait(false);

        if (_process != null)
        {
            try
            {
                if (!_process.HasExited)
                    _process.Kill(entireProcessTree: true);
            }
            catch { }
            _process.Dispose();
        }

        _networkStream?.Dispose();
        _socket?.Dispose();
    }
}
