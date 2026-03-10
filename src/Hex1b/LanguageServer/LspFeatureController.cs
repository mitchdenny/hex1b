using Hex1b.Documents;
using Hex1b.LanguageServer.Protocol;

namespace Hex1b.LanguageServer;

/// <summary>
/// Central dispatch for LSP features. Reads enabled features from the language extension,
/// only sends requests for enabled features, and routes results through extension hooks.
/// </summary>
internal sealed class LspFeatureController
{
    private readonly LanguageServerClient _client;
    private readonly ILanguageExtension _extension;

    public LspFeatureController(LanguageServerClient client, ILanguageExtension extension)
    {
        _client = client;
        _extension = extension;
    }

    /// <summary>The language extension driving this controller.</summary>
    public ILanguageExtension Extension => _extension;

    /// <summary>Whether a specific feature is enabled.</summary>
    public bool IsEnabled(LspFeatureSet feature) =>
        (_extension.EnabledFeatures & feature) != 0;

    /// <summary>Whether a feature is both enabled by the extension and supported by the server.</summary>
    public bool IsAvailable(LspFeatureSet feature)
    {
        if (!IsEnabled(feature)) return false;
        var caps = _client.ServerCapabilities;
        if (caps == null) return true;

        return feature switch
        {
            LspFeatureSet.Hover => caps.HasCapability(caps.HoverProvider),
            LspFeatureSet.Definition => caps.HasCapability(caps.DefinitionProvider),
            LspFeatureSet.References => caps.HasCapability(caps.ReferencesProvider),
            LspFeatureSet.Rename => caps.HasCapability(caps.RenameProvider),
            LspFeatureSet.SignatureHelp => caps.SignatureHelpProvider != null,
            LspFeatureSet.CodeActions => caps.HasCapability(caps.CodeActionProvider),
            LspFeatureSet.Formatting => caps.HasCapability(caps.DocumentFormattingProvider),
            LspFeatureSet.DocumentSymbol => caps.HasCapability(caps.DocumentSymbolProvider),
            LspFeatureSet.DocumentHighlight => caps.HasCapability(caps.DocumentHighlightProvider),
            LspFeatureSet.FoldingRange => caps.HasCapability(caps.FoldingRangeProvider),
            LspFeatureSet.SelectionRange => caps.HasCapability(caps.SelectionRangeProvider),
            LspFeatureSet.InlayHints => caps.HasCapability(caps.InlayHintProvider),
            LspFeatureSet.CodeLens => caps.CodeLensProvider != null,
            LspFeatureSet.CallHierarchy => caps.HasCapability(caps.CallHierarchyProvider),
            LspFeatureSet.TypeHierarchy => caps.HasCapability(caps.TypeHierarchyProvider),
            LspFeatureSet.Completion => caps.CompletionProvider != null,
            LspFeatureSet.SemanticTokens => caps.HasCapability(caps.SemanticTokensProvider),
            _ => true,
        };
    }

    /// <summary>Requests hover if enabled.</summary>
    public async Task<HoverResult?> RequestHoverAsync(string uri, int line, int character, CancellationToken ct = default)
    {
        if (!IsEnabled(LspFeatureSet.Hover)) return null;
        return await _client.RequestHoverAsync(uri, line, character, ct).ConfigureAwait(false);
    }

    /// <summary>Requests definition if enabled.</summary>
    public async Task<Location[]?> RequestDefinitionAsync(string uri, int line, int character, CancellationToken ct = default)
    {
        if (!IsEnabled(LspFeatureSet.Definition)) return null;
        return await _client.RequestDefinitionAsync(uri, line, character, ct).ConfigureAwait(false);
    }

    /// <summary>Requests references if enabled.</summary>
    public async Task<Location[]?> RequestReferencesAsync(string uri, int line, int character, bool includeDeclaration = true, CancellationToken ct = default)
    {
        if (!IsEnabled(LspFeatureSet.References)) return null;
        return await _client.RequestReferencesAsync(uri, line, character, includeDeclaration, ct).ConfigureAwait(false);
    }

    /// <summary>Requests rename if enabled.</summary>
    public async Task<WorkspaceEdit?> RequestRenameAsync(string uri, int line, int character, string newName, CancellationToken ct = default)
    {
        if (!IsEnabled(LspFeatureSet.Rename)) return null;
        return await _client.RequestRenameAsync(uri, line, character, newName, ct).ConfigureAwait(false);
    }

    /// <summary>Requests signature help if enabled.</summary>
    public async Task<SignatureHelp?> RequestSignatureHelpAsync(string uri, int line, int character, CancellationToken ct = default)
    {
        if (!IsEnabled(LspFeatureSet.SignatureHelp)) return null;
        return await _client.RequestSignatureHelpAsync(uri, line, character, ct).ConfigureAwait(false);
    }

    /// <summary>Requests code actions if enabled, filtered through the extension.</summary>
    public async Task<IReadOnlyList<CodeAction>?> RequestCodeActionsAsync(string uri, LspRange range, CancellationToken ct = default)
    {
        if (!IsEnabled(LspFeatureSet.CodeActions)) return null;
        var actions = await _client.RequestCodeActionsAsync(uri, range, ct).ConfigureAwait(false);
        if (actions == null) return null;
        return _extension.FilterCodeActions(actions);
    }

    /// <summary>Requests formatting if enabled.</summary>
    public async Task<TextEdit[]?> RequestFormattingAsync(string uri, FormattingOptions? options = null, CancellationToken ct = default)
    {
        if (!IsEnabled(LspFeatureSet.Formatting)) return null;
        return await _client.RequestFormattingAsync(uri, options, ct).ConfigureAwait(false);
    }

    /// <summary>Requests document symbols if enabled.</summary>
    public async Task<DocumentSymbol[]?> RequestDocumentSymbolsAsync(string uri, CancellationToken ct = default)
    {
        if (!IsEnabled(LspFeatureSet.DocumentSymbol)) return null;
        return await _client.RequestDocumentSymbolsAsync(uri, ct).ConfigureAwait(false);
    }

    /// <summary>Requests document highlight if enabled.</summary>
    public async Task<DocumentHighlight[]?> RequestDocumentHighlightAsync(string uri, int line, int character, CancellationToken ct = default)
    {
        if (!IsEnabled(LspFeatureSet.DocumentHighlight)) return null;
        return await _client.RequestDocumentHighlightAsync(uri, line, character, ct).ConfigureAwait(false);
    }

    /// <summary>Requests folding ranges if enabled.</summary>
    public async Task<FoldingRange[]?> RequestFoldingRangesAsync(string uri, CancellationToken ct = default)
    {
        if (!IsEnabled(LspFeatureSet.FoldingRange)) return null;
        return await _client.RequestFoldingRangesAsync(uri, ct).ConfigureAwait(false);
    }

    /// <summary>Requests inlay hints if enabled.</summary>
    public async Task<InlayHint[]?> RequestInlayHintsAsync(string uri, LspRange range, CancellationToken ct = default)
    {
        if (!IsEnabled(LspFeatureSet.InlayHints)) return null;
        return await _client.RequestInlayHintsAsync(uri, range, ct).ConfigureAwait(false);
    }

    /// <summary>Requests code lens if enabled.</summary>
    public async Task<CodeLens[]?> RequestCodeLensAsync(string uri, CancellationToken ct = default)
    {
        if (!IsEnabled(LspFeatureSet.CodeLens)) return null;
        return await _client.RequestCodeLensAsync(uri, ct).ConfigureAwait(false);
    }

    /// <summary>Requests completion if enabled, filtered through the extension.</summary>
    public async Task<CompletionList?> RequestCompletionAsync(string uri, int line, int character, CompletionContext? context = null, CancellationToken ct = default)
    {
        if (!IsEnabled(LspFeatureSet.Completion)) return null;
        var result = await _client.RequestCompletionAsync(uri, line, character, context, ct).ConfigureAwait(false);
        if (result?.Items != null)
        {
            var filtered = _extension.FilterCompletions(result.Items);
            if (!ReferenceEquals(filtered, result.Items))
                result = new CompletionList { IsIncomplete = result.IsIncomplete, Items = filtered.ToArray() };
        }
        return result;
    }

    /// <summary>Requests semantic tokens if enabled.</summary>
    public async Task<SemanticTokensResult?> RequestSemanticTokensAsync(string uri, CancellationToken ct = default)
    {
        if (!IsEnabled(LspFeatureSet.SemanticTokens)) return null;
        return await _client.RequestSemanticTokensAsync(uri, ct).ConfigureAwait(false);
    }

    /// <summary>Requests selection range if enabled.</summary>
    public async Task<SelectionRange[]?> RequestSelectionRangeAsync(string uri, LspPosition[] positions, CancellationToken ct = default)
    {
        if (!IsEnabled(LspFeatureSet.SelectionRange)) return null;
        return await _client.RequestSelectionRangeAsync(uri, positions, ct).ConfigureAwait(false);
    }

    /// <summary>Requests call hierarchy preparation if enabled.</summary>
    public async Task<CallHierarchyItem[]?> RequestCallHierarchyPrepareAsync(string uri, int line, int character, CancellationToken ct = default)
    {
        if (!IsEnabled(LspFeatureSet.CallHierarchy)) return null;
        return await _client.RequestCallHierarchyPrepareAsync(uri, line, character, ct).ConfigureAwait(false);
    }

    /// <summary>Requests incoming calls if call hierarchy is enabled.</summary>
    public async Task<CallHierarchyIncomingCall[]?> RequestCallHierarchyIncomingAsync(CallHierarchyItem item, CancellationToken ct = default)
    {
        if (!IsEnabled(LspFeatureSet.CallHierarchy)) return null;
        return await _client.RequestCallHierarchyIncomingAsync(item, ct).ConfigureAwait(false);
    }

    /// <summary>Requests outgoing calls if call hierarchy is enabled.</summary>
    public async Task<CallHierarchyOutgoingCall[]?> RequestCallHierarchyOutgoingAsync(CallHierarchyItem item, CancellationToken ct = default)
    {
        if (!IsEnabled(LspFeatureSet.CallHierarchy)) return null;
        return await _client.RequestCallHierarchyOutgoingAsync(item, ct).ConfigureAwait(false);
    }

    /// <summary>Requests type hierarchy preparation if enabled.</summary>
    public async Task<TypeHierarchyItem[]?> RequestTypeHierarchyPrepareAsync(string uri, int line, int character, CancellationToken ct = default)
    {
        if (!IsEnabled(LspFeatureSet.TypeHierarchy)) return null;
        return await _client.RequestTypeHierarchyPrepareAsync(uri, line, character, ct).ConfigureAwait(false);
    }

    /// <summary>Requests supertypes if type hierarchy is enabled.</summary>
    public async Task<TypeHierarchyItem[]?> RequestTypeHierarchySupertypesAsync(TypeHierarchyItem item, CancellationToken ct = default)
    {
        if (!IsEnabled(LspFeatureSet.TypeHierarchy)) return null;
        return await _client.RequestTypeHierarchySuperAsync(item, ct).ConfigureAwait(false);
    }

    /// <summary>Requests subtypes if type hierarchy is enabled.</summary>
    public async Task<TypeHierarchyItem[]?> RequestTypeHierarchySubtypesAsync(TypeHierarchyItem item, CancellationToken ct = default)
    {
        if (!IsEnabled(LspFeatureSet.TypeHierarchy)) return null;
        return await _client.RequestTypeHierarchySubAsync(item, ct).ConfigureAwait(false);
    }
}
