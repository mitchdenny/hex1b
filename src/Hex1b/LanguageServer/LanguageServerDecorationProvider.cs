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
    private LanguageServerClient? _client;
    private IEditorSession? _session;
    private CancellationTokenSource? _cts;
    private string[] _tokenLegend = SemanticTokenTypes.All;

    // Cached decoration state — swapped atomically from background tasks
    private volatile IReadOnlyList<TextDecorationSpan> _semanticSpans = [];
    private volatile IReadOnlyList<TextDecorationSpan> _diagnosticSpans = [];
    private long _lastDocVersion = -1;

    public LanguageServerDecorationProvider(LanguageServerConfiguration config)
    {
        _config = config;
    }

    /// <summary>Whether the language server is connected and initialized.</summary>
    public bool IsConnected => _client != null;

    // ── ITextDecorationProvider ──────────────────────────────

    public void Activate(IEditorSession session)
    {
        _session = session;
        _cts = new CancellationTokenSource();

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

    public IReadOnlyList<TextDecorationSpan> GetDecorations(int startLine, int endLine, IHex1bDocument document)
    {
        // Check if document changed — request fresh tokens
        if (document.Version != _lastDocVersion && _client != null)
        {
            _lastDocVersion = document.Version;
            _ = Task.Run(async () =>
            {
                try
                {
                    await OnDocumentChangedAsync(document).ConfigureAwait(false);
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
        if (_client.ServerCapabilities?.SemanticTokensProvider != null)
        {
            try
            {
                var provider = _client.ServerCapabilities.SemanticTokensProvider.Value;
                if (provider.TryGetProperty("legend", out var legend) &&
                    legend.TryGetProperty("tokenTypes", out var types))
                {
                    _tokenLegend = JsonSerializer.Deserialize<string[]>(types.GetRawText()) ?? SemanticTokenTypes.All;
                }
            }
            catch { }
        }

        // Open the document
        if (_session != null)
        {
            var text = _session.State.Document.GetText();
            await _client.OpenDocumentAsync(text, ct).ConfigureAwait(false);

            // Request initial semantic tokens
            if (_config.EnableSemanticHighlightingValue)
                await RefreshSemanticTokensAsync(ct).ConfigureAwait(false);
        }
    }

    private async Task OnDocumentChangedAsync(IHex1bDocument document)
    {
        if (_client == null || _cts == null) return;

        var text = document.GetText();
        await _client.ChangeDocumentAsync(text, _cts.Token).ConfigureAwait(false);

        if (_config.EnableSemanticHighlightingValue)
            await RefreshSemanticTokensAsync(_cts.Token).ConfigureAwait(false);
    }

    private async Task RefreshSemanticTokensAsync(CancellationToken ct)
    {
        if (_client == null) return;

        var result = await _client.RequestSemanticTokensAsync(ct).ConfigureAwait(false);
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

                if (diagParams != null && _config.EnableDiagnosticsValue)
                {
                    _diagnosticSpans = DiagnosticMapper.MapDiagnostics(diagParams.Diagnostics);
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
        if (_client == null || _session == null || !_config.EnableCompletionValue) return;

        // LSP positions are 0-based
        var result = await _client.RequestCompletionAsync(line - 1, column - 1, ct).ConfigureAwait(false);
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
