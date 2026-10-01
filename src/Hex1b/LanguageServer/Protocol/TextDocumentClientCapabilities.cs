using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class TextDocumentClientCapabilities
{
    [JsonPropertyName("synchronization")]
    public SynchronizationClientCapabilities? Synchronization { get; set; }

    [JsonPropertyName("completion")]
    public CompletionClientCapabilities? Completion { get; set; }

    [JsonPropertyName("hover")]
    public HoverClientCapabilities? Hover { get; set; }

    [JsonPropertyName("signatureHelp")]
    public SignatureHelpClientCapabilities? SignatureHelp { get; set; }

    [JsonPropertyName("definition")]
    public DynamicRegistrationCapability? Definition { get; set; }

    [JsonPropertyName("references")]
    public DynamicRegistrationCapability? References { get; set; }

    [JsonPropertyName("documentHighlight")]
    public DynamicRegistrationCapability? DocumentHighlight { get; set; }

    [JsonPropertyName("documentSymbol")]
    public DocumentSymbolClientCapabilities? DocumentSymbol { get; set; }

    [JsonPropertyName("codeAction")]
    public CodeActionClientCapabilities? CodeAction { get; set; }

    [JsonPropertyName("formatting")]
    public DynamicRegistrationCapability? Formatting { get; set; }

    [JsonPropertyName("rangeFormatting")]
    public DynamicRegistrationCapability? RangeFormatting { get; set; }

    [JsonPropertyName("rename")]
    public RenameClientCapabilities? Rename { get; set; }

    [JsonPropertyName("foldingRange")]
    public FoldingRangeClientCapabilities? FoldingRange { get; set; }

    [JsonPropertyName("selectionRange")]
    public DynamicRegistrationCapability? SelectionRange { get; set; }

    [JsonPropertyName("semanticTokens")]
    public SemanticTokensClientCapabilities? SemanticTokens { get; set; }

    [JsonPropertyName("inlayHint")]
    public DynamicRegistrationCapability? InlayHint { get; set; }

    [JsonPropertyName("codeLens")]
    public DynamicRegistrationCapability? CodeLens { get; set; }

    [JsonPropertyName("callHierarchy")]
    public DynamicRegistrationCapability? CallHierarchy { get; set; }

    [JsonPropertyName("typeHierarchy")]
    public DynamicRegistrationCapability? TypeHierarchy { get; set; }

    [JsonPropertyName("documentLink")]
    public DynamicRegistrationCapability? DocumentLink { get; set; }

    [JsonPropertyName("publishDiagnostics")]
    public PublishDiagnosticsClientCapabilities? PublishDiagnostics { get; set; }
}
