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
    private LspFeatureController? _featureController;
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
        _featureController = new LspFeatureController(sharedClient, DefaultLanguageExtension.Instance);
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

    /// <summary>
    /// Syncs the document content to the language server and marks the version
    /// as up-to-date so the background sync in GetDecorations() won't re-send it.
    /// This prevents double-sending didChange when a feature request (e.g., completion)
    /// needs to sync before issuing its LSP request.
    /// </summary>
    internal async Task SyncDocumentAsync(IHex1bDocument document)
    {
        var client = ActiveClient;
        if (client == null) return;
        _lastDocVersion = document.Version;
        await client.ChangeDocumentAsync(_documentUri, document.GetText()).ConfigureAwait(false);
    }

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

        // Create feature controller for gated LSP requests
        _featureController = new LspFeatureController(_client, DefaultLanguageExtension.Instance);

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

        // Refresh structural features (folding, symbols, inlay hints) after initial load
        await RefreshStructuralFeaturesAsync(ct).ConfigureAwait(false);
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

        // Refresh structural features after content changes
        await RefreshStructuralFeaturesAsync(_cts.Token).ConfigureAwait(false);
    }

    private async Task RefreshSemanticTokensAsync(LanguageServerClient client, CancellationToken ct)
    {
        var controller = _featureController;
        SemanticTokensResult? result;
        if (controller != null)
            result = await controller.RequestSemanticTokensAsync(_documentUri, ct).ConfigureAwait(false);
        else
            result = await client.RequestSemanticTokensAsync(_documentUri, ct).ConfigureAwait(false);

        if (result?.Data != null)
        {
            _semanticSpans = SemanticTokenMapper.MapTokens(result.Data, _tokenLegend);
            _session?.Invalidate();
        }
    }

    /// <summary>
    /// Refreshes structural LSP features: folding ranges, document symbols, and inlay hints.
    /// Called after initial document open and after document changes.
    /// </summary>
    private async Task RefreshStructuralFeaturesAsync(CancellationToken ct)
    {
        if (_session == null) return;

        try
        {
            // Request folding, symbols, and inlay hints in parallel
            var tasks = new List<Task>();
            tasks.Add(RequestFoldingRangesAsync(ct));
            tasks.Add(RequestDocumentSymbolsAsync(ct));
            await Task.WhenAll(tasks).ConfigureAwait(false);
            _session.Invalidate();
        }
        catch { }
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

    // ── Static conversion helpers ────────────────────────────

    /// <summary>Converts LSP DocumentHighlight[] to foundation RangeHighlight[] (0-based → 1-based).</summary>
    internal static IReadOnlyList<RangeHighlight> DocumentHighlightsToRangeHighlights(DocumentHighlight[]? highlights)
    {
        if (highlights == null || highlights.Length == 0)
            return [];

        var result = new RangeHighlight[highlights.Length];
        for (var i = 0; i < highlights.Length; i++)
        {
            var h = highlights[i];
            var kind = h.Kind switch
            {
                2 => RangeHighlightKind.ReadAccess,
                3 => RangeHighlightKind.WriteAccess,
                _ => RangeHighlightKind.Default,
            };
            result[i] = new RangeHighlight(
                new DocumentPosition(h.Range.Start.Line + 1, h.Range.Start.Character + 1),
                new DocumentPosition(h.Range.End.Line + 1, h.Range.End.Character + 1),
                kind);
        }

        return result;
    }

    /// <summary>Converts LSP SignatureHelp to a SignaturePanel for the editor.</summary>
    internal static SignaturePanel? SignatureHelpToPanel(SignatureHelp? help)
    {
        if (help == null || help.Signatures.Length == 0)
            return null;

        var entries = new List<SignaturePanelEntry>(help.Signatures.Length);
        foreach (var sig in help.Signatures)
        {
            var parameters = new List<SignatureParameterInfo>();
            if (sig.Parameters != null)
            {
                foreach (var p in sig.Parameters)
                {
                    var label = p.Label.ValueKind == JsonValueKind.String
                        ? p.Label.GetString() ?? ""
                        : p.Label.ToString();
                    var doc = p.Documentation.HasValue
                        ? (p.Documentation.Value.ValueKind == JsonValueKind.String
                            ? p.Documentation.Value.GetString()
                            : null)
                        : null;
                    parameters.Add(new SignatureParameterInfo(label) { Documentation = doc });
                }
            }

            var sigDoc = sig.Documentation.HasValue
                ? (sig.Documentation.Value.ValueKind == JsonValueKind.String
                    ? sig.Documentation.Value.GetString()
                    : null)
                : null;
            entries.Add(new SignaturePanelEntry(sig.Label, parameters) { Documentation = sigDoc });
        }

        return new SignaturePanel(entries)
        {
            ActiveSignature = help.ActiveSignature ?? 0,
            ActiveParameter = help.ActiveParameter ?? 0,
        };
    }

    /// <summary>Converts LSP DocumentSymbol[] to BreadcrumbData (0-based → 1-based).</summary>
    internal static BreadcrumbData? DocumentSymbolsToBreadcrumbs(DocumentSymbol[]? symbols)
    {
        if (symbols == null || symbols.Length == 0)
            return null;

        return new BreadcrumbData(ConvertSymbols(symbols));

        static IReadOnlyList<BreadcrumbSymbol> ConvertSymbols(DocumentSymbol[] syms)
        {
            var result = new BreadcrumbSymbol[syms.Length];
            for (var i = 0; i < syms.Length; i++)
            {
                var s = syms[i];
                var kind = MapSymbolKind(s.Kind);
                var children = s.Children != null && s.Children.Length > 0
                    ? ConvertSymbols(s.Children)
                    : null;
                result[i] = new BreadcrumbSymbol(
                    s.Name,
                    kind,
                    new DocumentPosition(s.Range.Start.Line + 1, s.Range.Start.Character + 1),
                    new DocumentPosition(s.Range.End.Line + 1, s.Range.End.Character + 1),
                    children);
            }

            return result;
        }

        static BreadcrumbSymbolKind MapSymbolKind(int kind) => kind switch
        {
            SymbolKind.File => BreadcrumbSymbolKind.File,
            SymbolKind.Module => BreadcrumbSymbolKind.Module,
            SymbolKind.Namespace => BreadcrumbSymbolKind.Namespace,
            SymbolKind.Package => BreadcrumbSymbolKind.Package,
            SymbolKind.Class => BreadcrumbSymbolKind.Class,
            SymbolKind.Method => BreadcrumbSymbolKind.Method,
            SymbolKind.Property => BreadcrumbSymbolKind.Property,
            SymbolKind.Field => BreadcrumbSymbolKind.Field,
            SymbolKind.Constructor => BreadcrumbSymbolKind.Constructor,
            SymbolKind.Enum => BreadcrumbSymbolKind.Enum,
            SymbolKind.Interface => BreadcrumbSymbolKind.Interface,
            SymbolKind.Function => BreadcrumbSymbolKind.Function,
            SymbolKind.Variable => BreadcrumbSymbolKind.Variable,
            SymbolKind.Constant => BreadcrumbSymbolKind.Constant,
            SymbolKind.String => BreadcrumbSymbolKind.String,
            SymbolKind.Number => BreadcrumbSymbolKind.Number,
            SymbolKind.Boolean => BreadcrumbSymbolKind.Boolean,
            SymbolKind.Array => BreadcrumbSymbolKind.Array,
            SymbolKind.Object => BreadcrumbSymbolKind.Object,
            SymbolKind.Key => BreadcrumbSymbolKind.Key,
            SymbolKind.Null => BreadcrumbSymbolKind.Null,
            SymbolKind.EnumMember => BreadcrumbSymbolKind.EnumMember,
            SymbolKind.Struct => BreadcrumbSymbolKind.Struct,
            SymbolKind.Event => BreadcrumbSymbolKind.Event,
            SymbolKind.Operator => BreadcrumbSymbolKind.Operator,
            SymbolKind.TypeParameter => BreadcrumbSymbolKind.TypeParameter,
            _ => BreadcrumbSymbolKind.File,
        };
    }

    /// <summary>Converts LSP FoldingRange[] to foundation FoldingRegion[] (0-based → 1-based).
    /// Regions that would hide fewer than one line when collapsed are excluded.</summary>
    internal static IReadOnlyList<FoldingRegion> FoldingRangesToRegions(FoldingRange[]? ranges)
    {
        if (ranges == null || ranges.Length == 0)
            return [];

        var result = new List<FoldingRegion>(ranges.Length);
        for (var i = 0; i < ranges.Length; i++)
        {
            var r = ranges[i];
            int startLine = r.StartLine + 1;
            int endLine = r.EndLine + 1;

            // A fold keeps the start line visible and hides StartLine+1..EndLine.
            // If endLine <= startLine there are no lines to hide — skip it.
            if (endLine <= startLine)
                continue;

            var kind = r.Kind switch
            {
                "comment" => FoldingRegionKind.Comment,
                "imports" => FoldingRegionKind.Imports,
                _ => FoldingRegionKind.Region,
            };
            result.Add(new FoldingRegion(startLine, endLine, kind));
        }

        return result;
    }

    /// <summary>Converts LSP InlayHint[] to foundation InlineHint[] (0-based → 1-based).</summary>
    internal static IReadOnlyList<InlineHint> InlayHintsToInlineHints(InlayHint[]? hints)
    {
        if (hints == null || hints.Length == 0)
            return [];

        var result = new InlineHint[hints.Length];
        for (var i = 0; i < hints.Length; i++)
        {
            var h = hints[i];
            string text;
            if (h.Label.ValueKind == JsonValueKind.String)
            {
                text = h.Label.GetString() ?? "";
            }
            else if (h.Label.ValueKind == JsonValueKind.Array)
            {
                var parts = JsonSerializer.Deserialize<InlayHintLabelPart[]>(h.Label.GetRawText());
                text = parts != null ? string.Concat(parts.Select(p => p.Value)) : "";
            }
            else
            {
                text = h.Label.ToString();
            }

            result[i] = new InlineHint(
                new DocumentPosition(h.Position.Line + 1, h.Position.Character + 1),
                text);
        }

        return result;
    }

    /// <summary>Converts LSP CodeLens[] to foundation GutterDecoration[].</summary>
    internal static IReadOnlyList<GutterDecoration> CodeLensToGutterDecorations(CodeLens[]? lenses)
    {
        if (lenses == null || lenses.Length == 0)
            return [];

        var result = new List<GutterDecoration>(lenses.Length);
        foreach (var lens in lenses)
        {
            if (lens.Command == null) continue;
            var title = lens.Command.Title;
            var ch = title.Length > 0 ? title[0] : '·';
            result.Add(new GutterDecoration(
                lens.Range.Start.Line + 1,
                ch,
                GutterDecorationKind.Info));
        }

        return result;
    }

    // ── Feature integration methods ──────────────────────────

    /// <summary>Highlights all occurrences of the symbol at the given position.</summary>
    internal async Task RequestDocumentHighlightAsync(int line, int column, CancellationToken ct = default)
    {
        var client = ActiveClient;
        if (client == null || _session == null) return;

        DocumentHighlight[]? result;
        var controller = _featureController;
        if (controller != null)
            result = await controller.RequestDocumentHighlightAsync(_documentUri, line - 1, column - 1, ct).ConfigureAwait(false);
        else
            result = await client.RequestDocumentHighlightAsync(_documentUri, line - 1, column - 1, ct).ConfigureAwait(false);

        var highlights = DocumentHighlightsToRangeHighlights(result);
        _session.PushRangeHighlights(highlights);
    }

    /// <summary>Requests rename at the given position with the new name.</summary>
    internal async Task<WorkspaceEdit?> RequestRenameAsync(int line, int column, string newName, CancellationToken ct = default)
    {
        var client = ActiveClient;
        if (client == null || _session == null) return null;

        var controller = _featureController;
        if (controller != null)
            return await controller.RequestRenameAsync(_documentUri, line - 1, column - 1, newName, ct).ConfigureAwait(false);
        else
            return await client.RequestRenameAsync(_documentUri, line - 1, column - 1, newName, ct).ConfigureAwait(false);
    }

    /// <summary>Requests signature help at the given position and shows a signature panel.</summary>
    internal async Task RequestSignatureHelpAsync(int line, int column, CancellationToken ct = default)
    {
        var client = ActiveClient;
        if (client == null || _session == null) return;

        SignatureHelp? result;
        var controller = _featureController;
        if (controller != null)
            result = await controller.RequestSignatureHelpAsync(_documentUri, line - 1, column - 1, ct).ConfigureAwait(false);
        else
            result = await client.RequestSignatureHelpAsync(_documentUri, line - 1, column - 1, ct).ConfigureAwait(false);

        var panel = SignatureHelpToPanel(result);
        if (panel != null)
            _session.ShowSignaturePanel(panel);
        else
            _session.DismissSignaturePanel();
    }

    /// <summary>Requests code actions for the given range.</summary>
    internal async Task<IReadOnlyList<CodeAction>> RequestCodeActionsAsync(int startLine, int startCol, int endLine, int endCol, CancellationToken ct = default)
    {
        var client = ActiveClient;
        if (client == null || _session == null) return [];

        var range = new LspRange
        {
            Start = new LspPosition { Line = startLine - 1, Character = startCol - 1 },
            End = new LspPosition { Line = endLine - 1, Character = endCol - 1 },
        };

        IReadOnlyList<CodeAction>? result;
        var controller = _featureController;
        if (controller != null)
            result = await controller.RequestCodeActionsAsync(_documentUri, range, ct).ConfigureAwait(false);
        else
        {
            var actions = await client.RequestCodeActionsAsync(_documentUri, range, ct).ConfigureAwait(false);
            result = actions;
        }

        return result ?? [];
    }

    /// <summary>Requests formatting for the entire document and applies text edits.</summary>
    internal async Task<TextEdit[]> RequestFormattingAsync(CancellationToken ct = default)
    {
        var client = ActiveClient;
        if (client == null || _session == null) return [];

        TextEdit[]? result;
        var controller = _featureController;
        if (controller != null)
            result = await controller.RequestFormattingAsync(_documentUri, ct: ct).ConfigureAwait(false);
        else
            result = await client.RequestFormattingAsync(_documentUri, ct: ct).ConfigureAwait(false);

        return result ?? [];
    }

    /// <summary>Requests document symbols and sets breadcrumb data.</summary>
    internal async Task RequestDocumentSymbolsAsync(CancellationToken ct = default)
    {
        var client = ActiveClient;
        if (client == null || _session == null) return;

        DocumentSymbol[]? result;
        var controller = _featureController;
        if (controller != null)
            result = await controller.RequestDocumentSymbolsAsync(_documentUri, ct).ConfigureAwait(false);
        else
            result = await client.RequestDocumentSymbolsAsync(_documentUri, ct).ConfigureAwait(false);

        var breadcrumbs = DocumentSymbolsToBreadcrumbs(result);
        _session.SetBreadcrumbs(breadcrumbs);
    }

    /// <summary>Requests folding ranges and sets folding regions.</summary>
    internal async Task RequestFoldingRangesAsync(CancellationToken ct = default)
    {
        var client = ActiveClient;
        if (client == null || _session == null) return;

        FoldingRange[]? result;
        var controller = _featureController;
        if (controller != null)
            result = await controller.RequestFoldingRangesAsync(_documentUri, ct).ConfigureAwait(false);
        else
            result = await client.RequestFoldingRangesAsync(_documentUri, ct).ConfigureAwait(false);

        var regions = FoldingRangesToRegions(result);
        _session.SetFoldingRegions(regions);
    }

    /// <summary>Requests inlay hints for visible range and sets inline hints.</summary>
    internal async Task RequestInlayHintsAsync(int startLine, int endLine, CancellationToken ct = default)
    {
        var client = ActiveClient;
        if (client == null || _session == null) return;

        var range = new LspRange
        {
            Start = new LspPosition { Line = startLine - 1, Character = 0 },
            End = new LspPosition { Line = endLine - 1, Character = 0 },
        };

        InlayHint[]? result;
        var controller = _featureController;
        if (controller != null)
            result = await controller.RequestInlayHintsAsync(_documentUri, range, ct).ConfigureAwait(false);
        else
            result = await client.RequestInlayHintsAsync(_documentUri, range, ct).ConfigureAwait(false);

        var hints = InlayHintsToInlineHints(result);
        _session.PushInlineHints(hints);
    }

    /// <summary>Requests code lens for the document and sets gutter decorations.</summary>
    internal async Task RequestCodeLensAsync(CancellationToken ct = default)
    {
        var client = ActiveClient;
        if (client == null || _session == null) return;

        CodeLens[]? result;
        var controller = _featureController;
        if (controller != null)
            result = await controller.RequestCodeLensAsync(_documentUri, ct).ConfigureAwait(false);
        else
            result = await client.RequestCodeLensAsync(_documentUri, ct).ConfigureAwait(false);

        var decorations = CodeLensToGutterDecorations(result);
        _session.PushGutterDecorations(decorations);
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
        CompletionList? result;
        var controller = _featureController;
        if (controller != null)
            result = await controller.RequestCompletionAsync(_documentUri, line - 1, column - 1, ct: ct).ConfigureAwait(false);
        else
            result = await client.RequestCompletionAsync(_documentUri, line - 1, column - 1, ct: ct).ConfigureAwait(false);
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

    // ── Hover support ──────────────────────────────────────────

    /// <summary>
    /// Requests hover information at the specified position and shows it as an overlay.
    /// </summary>
    internal async Task RequestHoverAsync(int line, int column, CancellationToken ct = default)
    {
        var client = ActiveClient;
        if (client == null || _session == null) return;

        HoverResult? result;
        var controller = _featureController;
        if (controller != null)
            result = await controller.RequestHoverAsync(_documentUri, line - 1, column - 1, ct).ConfigureAwait(false);
        else
            result = await client.RequestHoverAsync(_documentUri, line - 1, column - 1, ct).ConfigureAwait(false);

        var overlay = BuildHoverOverlay(result, line, column);
        if (overlay == null)
        {
            _session.DismissOverlay("lsp-hover");
            return;
        }

        _session.PushOverlay(overlay);
    }

    /// <summary>
    /// Builds an editor overlay from a hover result. Returns null if no content.
    /// </summary>
    internal static EditorOverlay? BuildHoverOverlay(HoverResult? result, int line, int column)
    {
        if (result?.Contents == null || string.IsNullOrWhiteSpace(result.Contents.Value))
            return null;

        var hoverText = result.Contents.Value;
        var textLines = hoverText.Split('\n');
        var overlayLines = new List<OverlayLine>();
        foreach (var textLine in textLines)
        {
            overlayLines.Add(new OverlayLine(
                " " + textLine + " ",
                Hex1bColor.FromRgb(204, 204, 204),
                Hex1bColor.FromRgb(37, 37, 38)));
        }

        return new EditorOverlay(
            Id: "lsp-hover",
            AnchorPosition: new DocumentPosition(line, column),
            Placement: OverlayPlacement.Above,
            Content: overlayLines,
            DismissOnCursorMove: true);
    }

    // ── Definition support ────────────────────────────────────

    /// <summary>
    /// Requests go-to-definition at the specified position.
    /// Returns the target locations, or empty if none found.
    /// </summary>
    internal async Task<Location[]> RequestDefinitionAsync(int line, int column, CancellationToken ct = default)
    {
        var client = ActiveClient;
        if (client == null || _session == null) return [];

        Location[]? result;
        var controller = _featureController;
        if (controller != null)
            result = await controller.RequestDefinitionAsync(_documentUri, line - 1, column - 1, ct).ConfigureAwait(false);
        else
            result = await client.RequestDefinitionAsync(_documentUri, line - 1, column - 1, ct).ConfigureAwait(false);

        return result ?? [];
    }

    // ── References support ───────────────────────────────────

    /// <summary>
    /// Requests find-all-references at the specified position and highlights them.
    /// </summary>
    internal async Task RequestReferencesAsync(int line, int column, CancellationToken ct = default)
    {
        var client = ActiveClient;
        if (client == null || _session == null) return;

        Location[]? result;
        var controller = _featureController;
        if (controller != null)
            result = await controller.RequestReferencesAsync(_documentUri, line - 1, column - 1, true, ct).ConfigureAwait(false);
        else
            result = await client.RequestReferencesAsync(_documentUri, line - 1, column - 1, true, ct).ConfigureAwait(false);

        if (result == null || result.Length == 0)
        {
            _session.ClearRangeHighlights();
            return;
        }

        var highlights = LocationsToHighlights(result, _documentUri);
        _session.PushRangeHighlights(highlights);
    }

    // ── Selection range support ────────────────────────────────

    /// <summary>
    /// Requests smart selection range at the given position.
    /// Returns nested selection ranges for expanding/shrinking selection.
    /// </summary>
    internal async Task<SelectionRange?> RequestSelectionRangeAsync(int line, int column, CancellationToken ct = default)
    {
        var client = ActiveClient;
        if (client == null || _session == null) return null;

        var positions = new[] { new LspPosition { Line = line - 1, Character = column - 1 } };

        SelectionRange[]? result;
        var controller = _featureController;
        if (controller != null)
            result = await controller.RequestSelectionRangeAsync(_documentUri, positions, ct).ConfigureAwait(false);
        else
            result = await client.RequestSelectionRangeAsync(_documentUri, positions, ct).ConfigureAwait(false);

        return result is { Length: > 0 } ? result[0] : null;
    }

    // ── Call hierarchy support ───────────────────────────────

    /// <summary>
    /// Prepares call hierarchy at the given position.
    /// Returns the call hierarchy items at that position.
    /// </summary>
    internal async Task<CallHierarchyItem[]> RequestCallHierarchyPrepareAsync(int line, int column, CancellationToken ct = default)
    {
        var client = ActiveClient;
        if (client == null || _session == null) return [];

        CallHierarchyItem[]? result;
        var controller = _featureController;
        if (controller != null)
            result = await controller.RequestCallHierarchyPrepareAsync(_documentUri, line - 1, column - 1, ct).ConfigureAwait(false);
        else
            result = await client.RequestCallHierarchyPrepareAsync(_documentUri, line - 1, column - 1, ct).ConfigureAwait(false);

        return result ?? [];
    }

    /// <summary>
    /// Gets incoming calls for a call hierarchy item.
    /// </summary>
    internal async Task<CallHierarchyIncomingCall[]> RequestCallHierarchyIncomingAsync(CallHierarchyItem item, CancellationToken ct = default)
    {
        var client = ActiveClient;
        if (client == null || _session == null) return [];

        CallHierarchyIncomingCall[]? result;
        var controller = _featureController;
        if (controller != null)
            result = await controller.RequestCallHierarchyIncomingAsync(item, ct).ConfigureAwait(false);
        else
            result = await client.RequestCallHierarchyIncomingAsync(item, ct).ConfigureAwait(false);

        return result ?? [];
    }

    /// <summary>
    /// Gets outgoing calls for a call hierarchy item.
    /// </summary>
    internal async Task<CallHierarchyOutgoingCall[]> RequestCallHierarchyOutgoingAsync(CallHierarchyItem item, CancellationToken ct = default)
    {
        var client = ActiveClient;
        if (client == null || _session == null) return [];

        CallHierarchyOutgoingCall[]? result;
        var controller = _featureController;
        if (controller != null)
            result = await controller.RequestCallHierarchyOutgoingAsync(item, ct).ConfigureAwait(false);
        else
            result = await client.RequestCallHierarchyOutgoingAsync(item, ct).ConfigureAwait(false);

        return result ?? [];
    }

    // ── Type hierarchy support ───────────────────────────────

    /// <summary>
    /// Prepares type hierarchy at the given position.
    /// </summary>
    internal async Task<TypeHierarchyItem[]> RequestTypeHierarchyPrepareAsync(int line, int column, CancellationToken ct = default)
    {
        var client = ActiveClient;
        if (client == null || _session == null) return [];

        TypeHierarchyItem[]? result;
        var controller = _featureController;
        if (controller != null)
            result = await controller.RequestTypeHierarchyPrepareAsync(_documentUri, line - 1, column - 1, ct).ConfigureAwait(false);
        else
            result = await client.RequestTypeHierarchyPrepareAsync(_documentUri, line - 1, column - 1, ct).ConfigureAwait(false);

        return result ?? [];
    }

    /// <summary>
    /// Gets supertypes for a type hierarchy item.
    /// </summary>
    internal async Task<TypeHierarchyItem[]> RequestTypeHierarchySupertypesAsync(TypeHierarchyItem item, CancellationToken ct = default)
    {
        var client = ActiveClient;
        if (client == null || _session == null) return [];

        TypeHierarchyItem[]? result;
        var controller = _featureController;
        if (controller != null)
            result = await controller.RequestTypeHierarchySupertypesAsync(item, ct).ConfigureAwait(false);
        else
            result = await client.RequestTypeHierarchySuperAsync(item, ct).ConfigureAwait(false);

        return result ?? [];
    }

    /// <summary>
    /// Gets subtypes for a type hierarchy item.
    /// </summary>
    internal async Task<TypeHierarchyItem[]> RequestTypeHierarchySubtypesAsync(TypeHierarchyItem item, CancellationToken ct = default)
    {
        var client = ActiveClient;
        if (client == null || _session == null) return [];

        TypeHierarchyItem[]? result;
        var controller = _featureController;
        if (controller != null)
            result = await controller.RequestTypeHierarchySubtypesAsync(item, ct).ConfigureAwait(false);
        else
            result = await client.RequestTypeHierarchySubAsync(item, ct).ConfigureAwait(false);

        return result ?? [];
    }

    /// <summary>
    /// Converts LSP locations to range highlights for the given document URI.
    /// </summary>
    internal static IReadOnlyList<RangeHighlight> LocationsToHighlights(Location[]? locations, string documentUri)
    {
        if (locations == null || locations.Length == 0) return [];

        var highlights = new List<RangeHighlight>();
        foreach (var loc in locations)
        {
            if (loc.Uri == documentUri && loc.Range != null)
            {
                highlights.Add(new RangeHighlight(
                    Start: new DocumentPosition(loc.Range.Start.Line + 1, loc.Range.Start.Character + 1),
                    End: new DocumentPosition(loc.Range.End.Line + 1, loc.Range.End.Character + 1),
                    Kind: RangeHighlightKind.ReadAccess));
            }
        }

        return highlights;
    }

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
