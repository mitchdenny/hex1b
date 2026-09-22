using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

/// <summary>Server capabilities returned from initialize response.</summary>
internal sealed class ServerCapabilities
{
    [JsonPropertyName("textDocumentSync")]
    public System.Text.Json.JsonElement? TextDocumentSync { get; set; }

    [JsonPropertyName("completionProvider")]
    public CompletionOptions? CompletionProvider { get; set; }

    [JsonPropertyName("hoverProvider")]
    public System.Text.Json.JsonElement? HoverProvider { get; set; }

    [JsonPropertyName("signatureHelpProvider")]
    public SignatureHelpOptions? SignatureHelpProvider { get; set; }

    [JsonPropertyName("definitionProvider")]
    public System.Text.Json.JsonElement? DefinitionProvider { get; set; }

    [JsonPropertyName("referencesProvider")]
    public System.Text.Json.JsonElement? ReferencesProvider { get; set; }

    [JsonPropertyName("documentHighlightProvider")]
    public System.Text.Json.JsonElement? DocumentHighlightProvider { get; set; }

    [JsonPropertyName("documentSymbolProvider")]
    public System.Text.Json.JsonElement? DocumentSymbolProvider { get; set; }

    [JsonPropertyName("codeActionProvider")]
    public System.Text.Json.JsonElement? CodeActionProvider { get; set; }

    [JsonPropertyName("codeLensProvider")]
    public CodeLensOptions? CodeLensProvider { get; set; }

    [JsonPropertyName("documentFormattingProvider")]
    public System.Text.Json.JsonElement? DocumentFormattingProvider { get; set; }

    [JsonPropertyName("documentRangeFormattingProvider")]
    public System.Text.Json.JsonElement? DocumentRangeFormattingProvider { get; set; }

    [JsonPropertyName("renameProvider")]
    public System.Text.Json.JsonElement? RenameProvider { get; set; }

    [JsonPropertyName("foldingRangeProvider")]
    public System.Text.Json.JsonElement? FoldingRangeProvider { get; set; }

    [JsonPropertyName("selectionRangeProvider")]
    public System.Text.Json.JsonElement? SelectionRangeProvider { get; set; }

    [JsonPropertyName("inlayHintProvider")]
    public System.Text.Json.JsonElement? InlayHintProvider { get; set; }

    [JsonPropertyName("callHierarchyProvider")]
    public System.Text.Json.JsonElement? CallHierarchyProvider { get; set; }

    [JsonPropertyName("typeHierarchyProvider")]
    public System.Text.Json.JsonElement? TypeHierarchyProvider { get; set; }

    [JsonPropertyName("documentLinkProvider")]
    public DocumentLinkOptions? DocumentLinkProvider { get; set; }

    [JsonPropertyName("semanticTokensProvider")]
    public System.Text.Json.JsonElement? SemanticTokensProvider { get; set; }

    /// <summary>Check if a capability is supported (handles bool or object forms).</summary>
    public bool HasCapability(System.Text.Json.JsonElement? element)
    {
        if (element == null) return false;
        if (element.Value.ValueKind == System.Text.Json.JsonValueKind.True) return true;
        if (element.Value.ValueKind == System.Text.Json.JsonValueKind.Object) return true;
        return false;
    }
}
