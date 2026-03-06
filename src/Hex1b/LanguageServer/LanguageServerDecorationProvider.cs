using System.Text.Json;
using Hex1b.Documents;
using Hex1b.LanguageServer.Protocol;
using Hex1b.Theming;

namespace Hex1b.LanguageServer;

/// <summary>
/// An <see cref="ITextDecorationProvider"/> that connects to a language server
/// and bridges semantic tokens, diagnostics, and completions to the editor's
/// decoration and overlay APIs.
/// </summary>
public sealed class LanguageServerDecorationProvider : ITextDecorationProvider, IAsyncDisposable
{
    private readonly LanguageServerConfiguration _config;
    private readonly string _documentUri;
    private readonly string _languageId;
    private LanguageServerClient? _client;
    private LanguageServerClient? _sharedClient; // Set when using a workspace-managed client
    private IEditorSession? _session;
    private CancellationTokenSource? _cts;
    private string[] _tokenLegend = SemanticTokenTypes.All;

    // Cached decoration state — swapped atomically from background tasks
    private volatile IReadOnlyList<TextDecorationSpan> _semanticSpans = [];
    private volatile IReadOnlyList<TextDecorationSpan> _diagnosticSpans = [];
    private volatile IReadOnlyList<DiagnosticInfo> _currentDiagnostics = [];
    private long _lastDocVersion = -1;
    private volatile bool _documentOpened;

    public LanguageServerDecorationProvider(LanguageServerConfiguration config)
    {
        _config = config;
        _documentUri = config.DocumentUri ?? "file:///untitled";
        _languageId = config.LanguageId ?? "plaintext";
    }

    /// <summary>
    /// Creates a provider that uses a shared client managed by a workspace.
    /// </summary>
    internal LanguageServerDecorationProvider(LanguageServerClient sharedClient, string documentUri, string languageId, string[] tokenLegend)
    {
        _config = new LanguageServerConfiguration();
        _sharedClient = sharedClient;
        _documentUri = documentUri;
        _languageId = languageId;
        _tokenLegend = tokenLegend;
    }

    /// <summary>Whether the language server is connected and initialized.</summary>
    public bool IsConnected => _client != null || _sharedClient != null;

    /// <summary>The current diagnostics for this document, updated from publishDiagnostics notifications.</summary>
    public IReadOnlyList<DiagnosticInfo> CurrentDiagnostics => _currentDiagnostics;

    /// <summary>The active client (owned or shared).</summary>
    private LanguageServerClient? ActiveClient => _sharedClient ?? _client;

    /// <summary>The active client, exposed for completion requests from EditorNode.</summary>
    internal LanguageServerClient? ActiveClientForCompletion => ActiveClient;

    /// <summary>The document URI, exposed for completion requests from EditorNode.</summary>
    internal string DocumentUriForCompletion => _documentUri;

    // ── ITextDecorationProvider ──────────────────────────────

    public void Activate(IEditorSession session)
    {
        _session = session;
        _cts = new CancellationTokenSource();

        if (_sharedClient != null)
        {
            // Shared client is already connected — just open our document
            _ = Task.Run(async () =>
            {
                try
                {
                    await OpenAndRefreshAsync(_sharedClient, _cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"LSP document open failed: {ex.Message}");
                }
            });
        }
        else
        {
            // Start connection in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await ConnectAsync(_cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"LSP connection failed: {ex.Message}");
                }
            });
        }
    }

    public IReadOnlyList<TextDecorationSpan> GetDecorations(int startLine, int endLine, IHex1bDocument document)
    {
        // Don't send change notifications until the document has been opened via didOpen
        if (_documentOpened && document.Version != _lastDocVersion && ActiveClient != null)
        {
            _lastDocVersion = document.Version;
            var client = ActiveClient;
            _ = Task.Run(async () =>
            {
                try
                {
                    await OnDocumentChangedAsync(client, document).ConfigureAwait(false);
                }
                catch { }
            });
        }

        // Combine semantic and diagnostic spans, filtered to viewport
        var result = new List<TextDecorationSpan>();

        foreach (var span in _semanticSpans)
        {
            if (span.End.Line >= startLine && span.Start.Line <= endLine)
                result.Add(span);
        }

        foreach (var span in _diagnosticSpans)
        {
            if (span.End.Line >= startLine && span.Start.Line <= endLine)
                result.Add(span);
        }

        return result;
    }

    public void Deactivate()
    {
        _ = DisposeAsync();
        _session = null;
    }

    // ── Connection lifecycle ─────────────────────────────────

    private async Task ConnectAsync(CancellationToken ct)
    {
        _client = new LanguageServerClient(_config);

        // Handle server notifications (diagnostics)
        _client.NotificationReceived += OnServerNotification;

        await _client.StartAsync(ct).ConfigureAwait(false);

        // Extract token legend from server capabilities
        ExtractTokenLegend(_client);

        await OpenAndRefreshAsync(_client, ct).ConfigureAwait(false);
    }

    private async Task OpenAndRefreshAsync(LanguageServerClient client, CancellationToken ct)
    {
        if (_session == null) return;

        // Wait for client to finish its initialize handshake (critical for shared clients
        // that are started in a fire-and-forget Task.Run by workspaces)
        await client.WaitUntilReadyAsync(ct).ConfigureAwait(false);

        // Extract token legend now that client is initialized (may not have been available
        // when the provider was created if the workspace started the client asynchronously)
        ExtractTokenLegend(client);

        var text = _session.State.Document.GetText();
        await client.OpenDocumentAsync(_documentUri, _languageId, text, ct).ConfigureAwait(false);
        _documentOpened = true;

        // Also wire up notification handling for shared clients
        if (_sharedClient != null)
            _sharedClient.NotificationReceived += OnServerNotification;

        // Initial token request — real language servers (csharp-ls, tsserver) may
        // need time to load projects/analyze code before returning tokens.
        // Retry a few times with increasing delays.
        for (var attempt = 0; attempt < 5 && !ct.IsCancellationRequested; attempt++)
        {
            await RefreshSemanticTokensAsync(client, ct).ConfigureAwait(false);
            if (_semanticSpans.Count > 0)
                break;
            await Task.Delay(TimeSpan.FromSeconds(1 + attempt), ct).ConfigureAwait(false);
        }
    }

    private void ExtractTokenLegend(LanguageServerClient client)
    {
        if (client.ServerCapabilities?.SemanticTokensProvider == null) return;
        try
        {
            var provider = client.ServerCapabilities.SemanticTokensProvider.Value;
            if (provider.TryGetProperty("legend", out var legend) &&
                legend.TryGetProperty("tokenTypes", out var types))
            {
                _tokenLegend = JsonSerializer.Deserialize<string[]>(types.GetRawText()) ?? SemanticTokenTypes.All;
            }
        }
        catch { }
    }

    private async Task OnDocumentChangedAsync(LanguageServerClient client, IHex1bDocument document)
    {
        if (_cts == null) return;

        // Ensure client is ready before sending requests
        await client.WaitUntilReadyAsync(_cts.Token).ConfigureAwait(false);

        var text = document.GetText();
        await client.ChangeDocumentAsync(_documentUri, text, _cts.Token).ConfigureAwait(false);

        await RefreshSemanticTokensAsync(client, _cts.Token).ConfigureAwait(false);
    }

    private async Task RefreshSemanticTokensAsync(LanguageServerClient client, CancellationToken ct)
    {
        var result = await client.RequestSemanticTokensAsync(_documentUri, ct).ConfigureAwait(false);
        if (result?.Data != null)
        {
            _semanticSpans = SemanticTokenMapper.MapTokens(result.Data, _tokenLegend);
            _session?.Invalidate();
        }
    }

    private void OnServerNotification(JsonRpcResponse notification)
    {
        if (notification.Method == "textDocument/publishDiagnostics" && notification.Params.HasValue)
        {
            try
            {
                var diagParams = JsonSerializer.Deserialize<PublishDiagnosticsParams>(
                    notification.Params.Value.GetRawText());

                // Only accept diagnostics for our document
                if (diagParams != null && diagParams.Uri == _documentUri)
                {
                    _diagnosticSpans = DiagnosticMapper.MapDiagnostics(diagParams.Diagnostics);
                    _currentDiagnostics = DiagnosticMapper.MapToDiagnosticInfo(diagParams.Diagnostics, _documentUri);
                    _session?.Invalidate();
                }
            }
            catch { }
        }
    }

    // ── Completion support ───────────────────────────────────

    /// <summary>
    /// Requests completion items at the current cursor position and pushes an overlay.
    /// Called by the editor when a trigger character is typed.
    /// </summary>
    internal async Task RequestCompletionAsync(int line, int column, CancellationToken ct = default)
    {
        var client = ActiveClient;
        if (client == null || _session == null) return;

        // LSP positions are 0-based
        var result = await client.RequestCompletionAsync(_documentUri, line - 1, column - 1, ct: ct).ConfigureAwait(false);
        if (result?.Items == null || result.Items.Length == 0)
        {
            _session.DismissOverlay("lsp-completion");
            return;
        }

        // Build overlay content from completion items
        var maxItems = Math.Min(result.Items.Length, 10);
        var lines = new List<OverlayLine>(maxItems);
        for (var i = 0; i < maxItems; i++)
        {
            var item = result.Items[i];
            var kindIcon = GetCompletionKindIcon(item.Kind);
            var fg = i == 0 ? Hex1bColor.FromRgb(255, 255, 255) : Hex1bColor.FromRgb(180, 180, 180);
            var bg = i == 0 ? Hex1bColor.FromRgb(50, 50, 120) : (Hex1bColor?)null;
            lines.Add(new OverlayLine($" {kindIcon} {item.Label} ", fg, bg));
        }

        _session.PushOverlay(new EditorOverlay(
            Id: "lsp-completion",
            AnchorPosition: new DocumentPosition(line, column),
            Placement: OverlayPlacement.Below,
            Content: lines,
            DismissOnCursorMove: true));
    }

    private static string GetCompletionKindIcon(int? kind) => kind switch
    {
        CompletionItemKind.Method or CompletionItemKind.Function => "ƒ",
        CompletionItemKind.Variable => "𝑥",
        CompletionItemKind.Class => "C",
        CompletionItemKind.Interface => "I",
        CompletionItemKind.Property => "P",
        CompletionItemKind.Field => "F",
        CompletionItemKind.Keyword => "K",
        CompletionItemKind.Snippet => "S",
        CompletionItemKind.Module => "M",
        _ => "·",
    };

    public async ValueTask DisposeAsync()
    {
        if (_cts != null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            _cts.Dispose();
            _cts = null;
        }

        if (_client != null)
        {
            try { await _client.StopAsync().ConfigureAwait(false); } catch { }
            await _client.DisposeAsync().ConfigureAwait(false);
            _client = null;
        }
    }
}
