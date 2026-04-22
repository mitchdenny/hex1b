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

    /// <summary>Whether the server supports incremental document sync (mode 2).</summary>
    public bool SupportsIncrementalSync { get; private set; }

    /// <summary>Raised when the server sends a notification (e.g., diagnostics).</summary>
    public event Action<JsonRpcResponse>? NotificationReceived;

    /// <summary>Event raised when the server sends a work done progress notification.</summary>
    internal event Action<WorkDoneProgress>? ProgressReceived;

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
        _transport.NotificationReceived += msg =>
        {
            NotificationReceived?.Invoke(msg);
            if (msg.Method == "$/progress" && msg.Params.HasValue)
            {
                try
                {
                    var progress = JsonSerializer.Deserialize(msg.Params.Value.GetRawText(), LspJsonContext.Default.WorkDoneProgress);
                    if (progress != null)
                        ProgressReceived?.Invoke(progress);
                }
                catch { }
            }
        };

        // Initialize handshake
        var initParams = new InitializeParams
        {
            ProcessId = Environment.ProcessId,
            RootUri = _config.RootUri,
            Capabilities = new ClientCapabilities
            {
                TextDocument = new TextDocumentClientCapabilities
                {
                    Synchronization = new SynchronizationClientCapabilities(),
                    Completion = new CompletionClientCapabilities(),
                    Hover = new HoverClientCapabilities(),
                    SignatureHelp = new SignatureHelpClientCapabilities(),
                    Definition = new DynamicRegistrationCapability(),
                    References = new DynamicRegistrationCapability(),
                    DocumentHighlight = new DynamicRegistrationCapability(),
                    DocumentSymbol = new DocumentSymbolClientCapabilities(),
                    CodeAction = new CodeActionClientCapabilities(),
                    Formatting = new DynamicRegistrationCapability(),
                    RangeFormatting = new DynamicRegistrationCapability(),
                    Rename = new RenameClientCapabilities(),
                    FoldingRange = new FoldingRangeClientCapabilities(),
                    SelectionRange = new DynamicRegistrationCapability(),
                    SemanticTokens = new SemanticTokensClientCapabilities(),
                    InlayHint = new DynamicRegistrationCapability(),
                    CodeLens = new DynamicRegistrationCapability(),
                    CallHierarchy = new DynamicRegistrationCapability(),
                    TypeHierarchy = new DynamicRegistrationCapability(),
                    DocumentLink = new DynamicRegistrationCapability(),
                    PublishDiagnostics = new PublishDiagnosticsClientCapabilities(),
                },
                Workspace = new WorkspaceClientCapabilities
                {
                    ApplyEdit = true,
                    WorkspaceFolders = true,
                },
            }
        };

        // Include workspace folders if we have a root URI
        if (_config.RootUri != null)
        {
            var folderName = Path.GetFileName(_config.WorkingDirectory ?? "workspace") ?? "workspace";
            initParams.WorkspaceFolders = [new WorkspaceFolder { Uri = _config.RootUri, Name = folderName }];
        }

        var response = await _transport.SendRequestAsync("initialize", JsonSerializer.SerializeToElement(initParams, LspJsonContext.Default.InitializeParams), ct).ConfigureAwait(false);
        if (response.Error != null)
            throw new InvalidOperationException($"LSP initialize failed: {response.Error.Message}");

        if (response.Result.HasValue)
        {
            var result = JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.InitializeResult);
            _serverCapabilities = result?.Capabilities;

            if (_serverCapabilities?.TextDocumentSync != null)
            {
                var syncElement = _serverCapabilities.TextDocumentSync.Value;
                int syncKind = 0;
                if (syncElement.ValueKind == JsonValueKind.Number)
                    syncKind = syncElement.GetInt32();
                else if (syncElement.ValueKind == JsonValueKind.Object &&
                         syncElement.TryGetProperty("change", out var changeElement))
                    syncKind = changeElement.GetInt32();

                SupportsIncrementalSync = syncKind == 2;
            }
        }

        await _transport.SendNotificationAsync("initialized", JsonDocument.Parse("{}").RootElement, ct).ConfigureAwait(false);

        // Start notification listener and wait for it to begin reading so that
        // subsequent SendRequestAsync calls don't race into the inline pump path.
        _notificationCts = new CancellationTokenSource();
        var loopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _notificationLoop = Task.Run(async () =>
        {
            loopStarted.TrySetResult();
            await _transport.RunNotificationLoopAsync(_notificationCts.Token).ConfigureAwait(false);
        });
        await loopStarted.Task.ConfigureAwait(false);

        _ready.TrySetResult();
    }

    /// <summary>Sends textDocument/didOpen notification.</summary>
    public async Task OpenDocumentAsync(string documentUri, string languageId, string text, CancellationToken ct = default)
    {
        if (_transport == null) throw new InvalidOperationException("Client not started");

        _documentVersions[documentUri] = 1;
        await _transport.SendNotificationAsync("textDocument/didOpen", JsonSerializer.SerializeToElement(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = documentUri,
                LanguageId = languageId,
                Version = 1,
                Text = text,
            }
        }, LspJsonContext.Default.DidOpenTextDocumentParams), ct).ConfigureAwait(false);
    }

    /// <summary>Sends textDocument/didChange notification (full document sync).</summary>
    public async Task ChangeDocumentAsync(string documentUri, string text, CancellationToken ct = default)
    {
        if (_transport == null) throw new InvalidOperationException("Client not started");

        var version = _documentVersions.GetValueOrDefault(documentUri) + 1;
        _documentVersions[documentUri] = version;
        await _transport.SendNotificationAsync("textDocument/didChange", JsonSerializer.SerializeToElement(new DidChangeTextDocumentParams
        {
            TextDocument = new VersionedTextDocumentIdentifier
            {
                Uri = documentUri,
                Version = version,
            },
            ContentChanges = [new TextDocumentContentChangeEvent { Text = text }]
        }, LspJsonContext.Default.DidChangeTextDocumentParams), ct).ConfigureAwait(false);
    }

    /// <summary>Sends an incremental textDocument/didChange notification with specific content changes.</summary>
    public async Task ChangeDocumentIncrementalAsync(
        string documentUri,
        IReadOnlyList<TextDocumentContentChangeEvent> changes,
        CancellationToken ct = default)
    {
        if (_transport == null) throw new InvalidOperationException("Client not started");

        var version = _documentVersions.GetValueOrDefault(documentUri) + 1;
        _documentVersions[documentUri] = version;
        await _transport.SendNotificationAsync("textDocument/didChange", JsonSerializer.SerializeToElement(new DidChangeTextDocumentParams
        {
            TextDocument = new VersionedTextDocumentIdentifier
            {
                Uri = documentUri,
                Version = version,
            },
            ContentChanges = changes.ToArray()
        }, LspJsonContext.Default.DidChangeTextDocumentParams), ct).ConfigureAwait(false);
    }

    /// <summary>Requests textDocument/semanticTokens/full.</summary>
    public async Task<SemanticTokensResult?> RequestSemanticTokensAsync(string documentUri, CancellationToken ct = default)
    {
        if (_transport == null) return null;

        var response = await _transport.SendRequestAsync("textDocument/semanticTokens/full",
            JsonSerializer.SerializeToElement(new SemanticTokensParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri }
            }, LspJsonContext.Default.SemanticTokensParams), ct).ConfigureAwait(false);

        if (response.Error != null || !response.Result.HasValue) return null;

        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.SemanticTokensResult);
    }

    /// <summary>Requests textDocument/completion.</summary>
    public async Task<CompletionList?> RequestCompletionAsync(
        string documentUri, int line, int character,
        CompletionContext? context = null,
        CancellationToken ct = default)
    {
        if (_transport == null) return null;

        var response = await _transport.SendRequestAsync("textDocument/completion",
            JsonSerializer.SerializeToElement(new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri },
                Position = new LspPosition { Line = line, Character = character },
                Context = context,
            }, LspJsonContext.Default.CompletionParams), ct).ConfigureAwait(false);

        if (response.Error != null || !response.Result.HasValue) return null;

        var raw = response.Result.Value;
        // Response can be CompletionList or CompletionItem[]
        if (raw.ValueKind == JsonValueKind.Array)
        {
            var items = JsonSerializer.Deserialize(raw.GetRawText(), LspJsonContext.Default.CompletionItemArray);
            return new CompletionList { Items = items ?? [] };
        }

        return JsonSerializer.Deserialize(raw.GetRawText(), LspJsonContext.Default.CompletionList);
    }

    /// <summary>Requests textDocument/hover.</summary>
    public async Task<HoverResult?> RequestHoverAsync(string documentUri, int line, int character, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("textDocument/hover",
            JsonSerializer.SerializeToElement(new HoverParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri },
                Position = new LspPosition { Line = line, Character = character }
            }, LspJsonContext.Default.HoverParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.HoverResult);
    }

    /// <summary>Requests textDocument/definition.</summary>
    public async Task<Location[]?> RequestDefinitionAsync(string documentUri, int line, int character, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("textDocument/definition",
            JsonSerializer.SerializeToElement(new DefinitionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri },
                Position = new LspPosition { Line = line, Character = character }
            }, LspJsonContext.Default.DefinitionParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        var raw = response.Result.Value;
        if (raw.ValueKind == JsonValueKind.Array)
            return JsonSerializer.Deserialize(raw.GetRawText(), LspJsonContext.Default.LocationArray);
        var single = JsonSerializer.Deserialize(raw.GetRawText(), LspJsonContext.Default.Location);
        return single != null ? [single] : null;
    }

    /// <summary>Requests textDocument/references.</summary>
    public async Task<Location[]?> RequestReferencesAsync(string documentUri, int line, int character, bool includeDeclaration = true, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("textDocument/references",
            JsonSerializer.SerializeToElement(new ReferenceParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri },
                Position = new LspPosition { Line = line, Character = character },
                Context = new ReferenceContext { IncludeDeclaration = includeDeclaration }
            }, LspJsonContext.Default.ReferenceParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.LocationArray);
    }

    /// <summary>Requests textDocument/rename.</summary>
    public async Task<WorkspaceEdit?> RequestRenameAsync(string documentUri, int line, int character, string newName, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("textDocument/rename",
            JsonSerializer.SerializeToElement(new RenameParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri },
                Position = new LspPosition { Line = line, Character = character },
                NewName = newName
            }, LspJsonContext.Default.RenameParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.WorkspaceEdit);
    }

    /// <summary>Requests textDocument/prepareRename.</summary>
    public async Task<PrepareRenameResult?> RequestPrepareRenameAsync(string documentUri, int line, int character, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("textDocument/prepareRename",
            JsonSerializer.SerializeToElement(new PrepareRenameParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri },
                Position = new LspPosition { Line = line, Character = character }
            }, LspJsonContext.Default.PrepareRenameParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.PrepareRenameResult);
    }

    /// <summary>Requests textDocument/signatureHelp.</summary>
    public async Task<SignatureHelp?> RequestSignatureHelpAsync(string documentUri, int line, int character, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("textDocument/signatureHelp",
            JsonSerializer.SerializeToElement(new SignatureHelpParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri },
                Position = new LspPosition { Line = line, Character = character }
            }, LspJsonContext.Default.SignatureHelpParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.SignatureHelp);
    }

    /// <summary>Requests textDocument/codeAction.</summary>
    public async Task<CodeAction[]?> RequestCodeActionsAsync(string documentUri, LspRange range, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("textDocument/codeAction",
            JsonSerializer.SerializeToElement(new CodeActionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri },
                Range = range,
                Context = new CodeActionContext()
            }, LspJsonContext.Default.CodeActionParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.CodeActionArray);
    }

    /// <summary>Requests textDocument/formatting.</summary>
    public async Task<TextEdit[]?> RequestFormattingAsync(string documentUri, FormattingOptions? options = null, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("textDocument/formatting",
            JsonSerializer.SerializeToElement(new DocumentFormattingParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri },
                Options = options ?? new FormattingOptions()
            }, LspJsonContext.Default.DocumentFormattingParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.TextEditArray);
    }

    /// <summary>Requests textDocument/rangeFormatting.</summary>
    public async Task<TextEdit[]?> RequestRangeFormattingAsync(string documentUri, LspRange range, FormattingOptions? options = null, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("textDocument/rangeFormatting",
            JsonSerializer.SerializeToElement(new DocumentRangeFormattingParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri },
                Range = range,
                Options = options ?? new FormattingOptions()
            }, LspJsonContext.Default.DocumentRangeFormattingParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.TextEditArray);
    }

    /// <summary>Requests textDocument/documentSymbol.</summary>
    public async Task<DocumentSymbol[]?> RequestDocumentSymbolsAsync(string documentUri, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("textDocument/documentSymbol",
            JsonSerializer.SerializeToElement(new DocumentSymbolParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri }
            }, LspJsonContext.Default.DocumentSymbolParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.DocumentSymbolArray);
    }

    /// <summary>Requests textDocument/documentHighlight.</summary>
    public async Task<DocumentHighlight[]?> RequestDocumentHighlightAsync(string documentUri, int line, int character, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("textDocument/documentHighlight",
            JsonSerializer.SerializeToElement(new DocumentHighlightParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri },
                Position = new LspPosition { Line = line, Character = character }
            }, LspJsonContext.Default.DocumentHighlightParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.DocumentHighlightArray);
    }

    /// <summary>Requests textDocument/foldingRange.</summary>
    public async Task<FoldingRange[]?> RequestFoldingRangesAsync(string documentUri, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("textDocument/foldingRange",
            JsonSerializer.SerializeToElement(new FoldingRangeParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri }
            }, LspJsonContext.Default.FoldingRangeParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.FoldingRangeArray);
    }

    /// <summary>Requests textDocument/selectionRange.</summary>
    public async Task<SelectionRange[]?> RequestSelectionRangeAsync(string documentUri, LspPosition[] positions, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("textDocument/selectionRange",
            JsonSerializer.SerializeToElement(new SelectionRangeParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri },
                Positions = positions
            }, LspJsonContext.Default.SelectionRangeParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.SelectionRangeArray);
    }

    /// <summary>Requests textDocument/inlayHint.</summary>
    public async Task<InlayHint[]?> RequestInlayHintsAsync(string documentUri, LspRange range, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("textDocument/inlayHint",
            JsonSerializer.SerializeToElement(new InlayHintParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri },
                Range = range
            }, LspJsonContext.Default.InlayHintParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.InlayHintArray);
    }

    /// <summary>Requests textDocument/codeLens.</summary>
    public async Task<CodeLens[]?> RequestCodeLensAsync(string documentUri, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("textDocument/codeLens",
            JsonSerializer.SerializeToElement(new CodeLensParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri }
            }, LspJsonContext.Default.CodeLensParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.CodeLensArray);
    }

    /// <summary>Requests textDocument/prepareCallHierarchy.</summary>
    public async Task<CallHierarchyItem[]?> RequestCallHierarchyPrepareAsync(string documentUri, int line, int character, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("textDocument/prepareCallHierarchy",
            JsonSerializer.SerializeToElement(new CallHierarchyPrepareParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri },
                Position = new LspPosition { Line = line, Character = character }
            }, LspJsonContext.Default.CallHierarchyPrepareParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.CallHierarchyItemArray);
    }

    /// <summary>Requests callHierarchy/incomingCalls.</summary>
    public async Task<CallHierarchyIncomingCall[]?> RequestCallHierarchyIncomingAsync(CallHierarchyItem item, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("callHierarchy/incomingCalls",
            JsonSerializer.SerializeToElement(new CallHierarchyIncomingCallsParams { Item = item }, LspJsonContext.Default.CallHierarchyIncomingCallsParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.CallHierarchyIncomingCallArray);
    }

    /// <summary>Requests callHierarchy/outgoingCalls.</summary>
    public async Task<CallHierarchyOutgoingCall[]?> RequestCallHierarchyOutgoingAsync(CallHierarchyItem item, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("callHierarchy/outgoingCalls",
            JsonSerializer.SerializeToElement(new CallHierarchyOutgoingCallsParams { Item = item }, LspJsonContext.Default.CallHierarchyOutgoingCallsParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.CallHierarchyOutgoingCallArray);
    }

    /// <summary>Requests textDocument/prepareTypeHierarchy.</summary>
    public async Task<TypeHierarchyItem[]?> RequestTypeHierarchyPrepareAsync(string documentUri, int line, int character, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("textDocument/prepareTypeHierarchy",
            JsonSerializer.SerializeToElement(new TypeHierarchyPrepareParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri },
                Position = new LspPosition { Line = line, Character = character }
            }, LspJsonContext.Default.TypeHierarchyPrepareParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.TypeHierarchyItemArray);
    }

    /// <summary>Requests typeHierarchy/supertypes.</summary>
    public async Task<TypeHierarchyItem[]?> RequestTypeHierarchySuperAsync(TypeHierarchyItem item, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("typeHierarchy/supertypes",
            JsonSerializer.SerializeToElement(new TypeHierarchySupertypesParams { Item = item }, LspJsonContext.Default.TypeHierarchySupertypesParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.TypeHierarchyItemArray);
    }

    /// <summary>Requests typeHierarchy/subtypes.</summary>
    public async Task<TypeHierarchyItem[]?> RequestTypeHierarchySubAsync(TypeHierarchyItem item, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("typeHierarchy/subtypes",
            JsonSerializer.SerializeToElement(new TypeHierarchySubtypesParams { Item = item }, LspJsonContext.Default.TypeHierarchySubtypesParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.TypeHierarchyItemArray);
    }

    /// <summary>Requests textDocument/documentLink.</summary>
    public async Task<DocumentLink[]?> RequestDocumentLinkAsync(string documentUri, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("textDocument/documentLink",
            JsonSerializer.SerializeToElement(new DocumentLinkParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = documentUri }
            }, LspJsonContext.Default.DocumentLinkParams), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.DocumentLinkArray);
    }

    /// <summary>Requests completionItem/resolve.</summary>
    public async Task<CompletionItem?> ResolveCompletionItemAsync(CompletionItem item, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("completionItem/resolve", JsonSerializer.SerializeToElement(item, LspJsonContext.Default.CompletionItem), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.CompletionItem);
    }

    /// <summary>Requests codeLens/resolve.</summary>
    public async Task<CodeLens?> ResolveCodeLensAsync(CodeLens item, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("codeLens/resolve", JsonSerializer.SerializeToElement(item, LspJsonContext.Default.CodeLens), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.CodeLens);
    }

    /// <summary>Requests inlayHint/resolve.</summary>
    public async Task<InlayHint?> ResolveInlayHintAsync(InlayHint item, CancellationToken ct = default)
    {
        if (_transport == null) return null;
        var response = await _transport.SendRequestAsync("inlayHint/resolve", JsonSerializer.SerializeToElement(item, LspJsonContext.Default.InlayHint), ct).ConfigureAwait(false);
        if (response.Error != null || !response.Result.HasValue) return null;
        return JsonSerializer.Deserialize(response.Result.Value.GetRawText(), LspJsonContext.Default.InlayHint);
    }

    /// <summary>Sends textDocument/didSave notification.</summary>
    public async Task SendDidSaveAsync(string documentUri, string? text = null, CancellationToken ct = default)
    {
        if (_transport == null) return;
        await _transport.SendNotificationAsync("textDocument/didSave", JsonSerializer.SerializeToElement(new DidSaveTextDocumentParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = documentUri },
            Text = text
        }, LspJsonContext.Default.DidSaveTextDocumentParams), ct).ConfigureAwait(false);
    }

    /// <summary>Sends textDocument/didClose notification.</summary>
    public async Task SendDidCloseAsync(string documentUri, CancellationToken ct = default)
    {
        if (_transport == null) return;
        _documentVersions.Remove(documentUri);
        await _transport.SendNotificationAsync("textDocument/didClose", JsonSerializer.SerializeToElement(new DidCloseTextDocumentParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = documentUri }
        }, LspJsonContext.Default.DidCloseTextDocumentParams), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a $/cancelRequest notification to cancel a pending request.
    /// </summary>
    internal async Task SendCancelRequestAsync(int requestId, CancellationToken ct = default)
    {
        if (_transport == null) return;
        await _transport.SendNotificationAsync("$/cancelRequest", JsonSerializer.SerializeToElement(new CancelParams { Id = requestId }, LspJsonContext.Default.CancelParams), ct).ConfigureAwait(false);
    }

    /// <summary>Sends textDocument/didClose for all open documents and shutdown/exit.</summary>
    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_transport == null) return;

        // Send the shutdown handshake WHILE the notification loop is still
        // running.  The loop dispatches the response to the pending TCS, so
        // SendRequestAsync never enters its inline message-pump path and we
        // avoid the PipeReaderStream "concurrent reads" crash that occurs when
        // the loop is cancelled first (the cancelled ReadAsync leaves the
        // PipeReader in a dirty state).
        try
        {
            foreach (var uri in _documentVersions.Keys.ToArray())
            {
                await SendDidCloseAsync(uri, ct).ConfigureAwait(false);
            }

            await _transport.SendRequestAsync("shutdown", null, ct).ConfigureAwait(false);
            await _transport.SendNotificationAsync("exit", null, ct).ConfigureAwait(false);
        }
        catch (IOException) { }
        catch (InvalidOperationException) { }
        catch (OperationCanceledException) { }

        // Now stop the notification loop — the server has already acknowledged
        // the shutdown, so there's nothing left to read.
        if (_notificationCts != null)
        {
            await _notificationCts.CancelAsync().ConfigureAwait(false);
        }

        if (_notificationLoop != null)
        {
            var completed = await Task.WhenAny(
                _notificationLoop,
                Task.Delay(TimeSpan.FromSeconds(2), ct)).ConfigureAwait(false);
            if (completed == _notificationLoop)
            {
                try { await _notificationLoop.ConfigureAwait(false); } catch { }
            }
            _notificationLoop = null;
        }
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
            // Use a timeout — the loop may be stuck in a non-cancellable stream
            // read that won't unblock until the transport/process is disposed.
            var completed = await Task.WhenAny(
                _notificationLoop,
                Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
            if (completed == _notificationLoop)
            {
                try { await _notificationLoop.ConfigureAwait(false); } catch { }
            }
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
