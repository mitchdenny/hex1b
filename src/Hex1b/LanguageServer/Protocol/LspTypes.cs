using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

// ── LSP basic types ──────────────────────────────────────────

internal sealed class LspPosition
{
    [JsonPropertyName("line")]
    public int Line { get; set; } // 0-based

    [JsonPropertyName("character")]
    public int Character { get; set; } // 0-based
}

internal sealed class LspRange
{
    [JsonPropertyName("start")]
    public LspPosition Start { get; set; } = new();

    [JsonPropertyName("end")]
    public LspPosition End { get; set; } = new();
}

internal sealed class TextDocumentIdentifier
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";
}

internal sealed class VersionedTextDocumentIdentifier
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    [JsonPropertyName("version")]
    public int Version { get; set; }
}

internal sealed class TextDocumentItem
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    [JsonPropertyName("languageId")]
    public string LanguageId { get; set; } = "";

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

// ── Initialize ───────────────────────────────────────────────

internal sealed class InitializeParams
{
    [JsonPropertyName("processId")]
    public int? ProcessId { get; set; }

    [JsonPropertyName("rootUri")]
    public string? RootUri { get; set; }

    [JsonPropertyName("workspaceFolders")]
    public WorkspaceFolder[]? WorkspaceFolders { get; set; }

    [JsonPropertyName("capabilities")]
    public ClientCapabilities Capabilities { get; set; } = new();
}

internal sealed class WorkspaceFolder
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

internal sealed class ClientCapabilities
{
    [JsonPropertyName("textDocument")]
    public TextDocumentClientCapabilities? TextDocument { get; set; } = new();

    [JsonPropertyName("workspace")]
    public WorkspaceClientCapabilities? Workspace { get; set; }
}

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

internal sealed class SemanticTokensClientCapabilities
{
    [JsonPropertyName("dynamicRegistration")]
    public bool DynamicRegistration { get; set; }

    [JsonPropertyName("requests")]
    public SemanticTokensRequests Requests { get; set; } = new();

    [JsonPropertyName("tokenTypes")]
    public string[] TokenTypes { get; set; } = SemanticTokenTypes.All;

    [JsonPropertyName("tokenModifiers")]
    public string[] TokenModifiers { get; set; } = [];

    [JsonPropertyName("formats")]
    public string[] Formats { get; set; } = ["relative"];
}

internal sealed class SemanticTokensRequests
{
    [JsonPropertyName("full")]
    public bool Full { get; set; } = true;
}

internal sealed class CompletionClientCapabilities
{
    [JsonPropertyName("dynamicRegistration")]
    public bool DynamicRegistration { get; set; }

    [JsonPropertyName("completionItem")]
    public CompletionItemClientCapabilities? CompletionItem { get; set; } = new();

    [JsonPropertyName("completionItemKind")]
    public CompletionItemKindClientCapabilities? CompletionItemKind { get; set; } = new();
}

internal sealed class PublishDiagnosticsClientCapabilities;

internal sealed class InitializeResult
{
    [JsonPropertyName("capabilities")]
    public ServerCapabilities Capabilities { get; set; } = new();
}

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

// ── Server Option Types ─────────────────────────────────────────

internal sealed class CompletionOptions
{
    [JsonPropertyName("triggerCharacters")]
    public string[]? TriggerCharacters { get; set; }

    [JsonPropertyName("resolveProvider")]
    public bool ResolveProvider { get; set; }
}

internal sealed class SignatureHelpOptions
{
    [JsonPropertyName("triggerCharacters")]
    public string[]? TriggerCharacters { get; set; }

    [JsonPropertyName("retriggerCharacters")]
    public string[]? RetriggerCharacters { get; set; }
}

internal sealed class CodeLensOptions
{
    [JsonPropertyName("resolveProvider")]
    public bool ResolveProvider { get; set; }
}

internal sealed class DocumentLinkOptions
{
    [JsonPropertyName("resolveProvider")]
    public bool ResolveProvider { get; set; }
}

// ── Client Capability Types ─────────────────────────────────────

internal sealed class WorkspaceClientCapabilities
{
    [JsonPropertyName("applyEdit")]
    public bool ApplyEdit { get; set; }

    [JsonPropertyName("workspaceFolders")]
    public bool WorkspaceFolders { get; set; }
}

internal sealed class DynamicRegistrationCapability
{
    [JsonPropertyName("dynamicRegistration")]
    public bool DynamicRegistration { get; set; }
}

internal sealed class SynchronizationClientCapabilities
{
    [JsonPropertyName("dynamicRegistration")]
    public bool DynamicRegistration { get; set; }

    [JsonPropertyName("willSave")]
    public bool WillSave { get; set; }

    [JsonPropertyName("didSave")]
    public bool DidSave { get; set; } = true;
}

internal sealed class HoverClientCapabilities
{
    [JsonPropertyName("dynamicRegistration")]
    public bool DynamicRegistration { get; set; }

    [JsonPropertyName("contentFormat")]
    public string[] ContentFormat { get; set; } = ["plaintext"];
}

internal sealed class SignatureHelpClientCapabilities
{
    [JsonPropertyName("dynamicRegistration")]
    public bool DynamicRegistration { get; set; }

    [JsonPropertyName("signatureInformation")]
    public SignatureInformationClientCapabilities? SignatureInformation { get; set; } = new();
}

internal sealed class SignatureInformationClientCapabilities
{
    [JsonPropertyName("documentationFormat")]
    public string[] DocumentationFormat { get; set; } = ["plaintext"];

    [JsonPropertyName("parameterInformation")]
    public ParameterInformationClientCapabilities? ParameterInformation { get; set; } = new();
}

internal sealed class ParameterInformationClientCapabilities
{
    [JsonPropertyName("labelOffsetSupport")]
    public bool LabelOffsetSupport { get; set; } = true;
}

internal sealed class DocumentSymbolClientCapabilities
{
    [JsonPropertyName("dynamicRegistration")]
    public bool DynamicRegistration { get; set; }

    [JsonPropertyName("hierarchicalDocumentSymbolSupport")]
    public bool HierarchicalDocumentSymbolSupport { get; set; } = true;
}

internal sealed class CodeActionClientCapabilities
{
    [JsonPropertyName("dynamicRegistration")]
    public bool DynamicRegistration { get; set; }

    [JsonPropertyName("codeActionLiteralSupport")]
    public CodeActionLiteralSupportCapabilities? CodeActionLiteralSupport { get; set; } = new();
}

internal sealed class CodeActionLiteralSupportCapabilities
{
    [JsonPropertyName("codeActionKind")]
    public CodeActionKindCapabilities? CodeActionKind { get; set; } = new();
}

internal sealed class CodeActionKindCapabilities
{
    [JsonPropertyName("valueSet")]
    public string[] ValueSet { get; set; } =
    [
        "quickfix",
        "refactor",
        "refactor.extract",
        "refactor.inline",
        "refactor.rewrite",
        "source",
        "source.organizeImports",
    ];
}

internal sealed class RenameClientCapabilities
{
    [JsonPropertyName("dynamicRegistration")]
    public bool DynamicRegistration { get; set; }

    [JsonPropertyName("prepareSupport")]
    public bool PrepareSupport { get; set; } = true;
}

internal sealed class FoldingRangeClientCapabilities
{
    [JsonPropertyName("dynamicRegistration")]
    public bool DynamicRegistration { get; set; }

    [JsonPropertyName("lineFoldingOnly")]
    public bool LineFoldingOnly { get; set; } = true;
}

internal sealed class CompletionItemClientCapabilities
{
    [JsonPropertyName("snippetSupport")]
    public bool SnippetSupport { get; set; }

    [JsonPropertyName("commitCharactersSupport")]
    public bool CommitCharactersSupport { get; set; } = true;

    [JsonPropertyName("documentationFormat")]
    public string[] DocumentationFormat { get; set; } = ["plaintext"];

    [JsonPropertyName("deprecatedSupport")]
    public bool DeprecatedSupport { get; set; } = true;

    [JsonPropertyName("preselectSupport")]
    public bool PreselectSupport { get; set; } = true;

    [JsonPropertyName("insertReplaceSupport")]
    public bool InsertReplaceSupport { get; set; }

    [JsonPropertyName("resolveSupport")]
    public CompletionItemResolveSupportCapabilities? ResolveSupport { get; set; } = new();
}

internal sealed class CompletionItemResolveSupportCapabilities
{
    [JsonPropertyName("properties")]
    public string[] Properties { get; set; } = ["documentation", "detail"];
}

internal sealed class CompletionItemKindClientCapabilities
{
    [JsonPropertyName("valueSet")]
    public int[] ValueSet { get; set; } = Enumerable.Range(1, 25).ToArray();
}

// ── Semantic Tokens ──────────────────────────────────────────

internal static class SemanticTokenTypes
{
    public const string Namespace = "namespace";
    public const string Type = "type";
    public const string Class = "class";
    public const string Enum = "enum";
    public const string Interface = "interface";
    public const string Struct = "struct";
    public const string TypeParameter = "typeParameter";
    public const string Parameter = "parameter";
    public const string Variable = "variable";
    public const string Property = "property";
    public const string EnumMember = "enumMember";
    public const string Function = "function";
    public const string Method = "method";
    public const string Keyword = "keyword";
    public const string Comment = "comment";
    public const string String = "string";
    public const string Number = "number";
    public const string Operator = "operator";

    public static string[] All =>
    [
        Namespace, Type, Class, Enum, Interface, Struct, TypeParameter,
        Parameter, Variable, Property, EnumMember, Function, Method,
        Keyword, Comment, String, Number, Operator
    ];
}

internal sealed class SemanticTokensParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();
}

internal sealed class SemanticTokensResult
{
    [JsonPropertyName("data")]
    public int[] Data { get; set; } = [];
}

// ── Diagnostics ──────────────────────────────────────────────

internal sealed class PublishDiagnosticsParams
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    [JsonPropertyName("diagnostics")]
    public LspDiagnostic[] Diagnostics { get; set; } = [];
}

internal sealed class LspDiagnostic
{
    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("severity")]
    public int? Severity { get; set; } // 1=Error, 2=Warning, 3=Info, 4=Hint

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("code")]
    public System.Text.Json.JsonElement? Code { get; set; }
}

// ── Completion ───────────────────────────────────────────────

internal sealed class CompletionParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("position")]
    public LspPosition Position { get; set; } = new();

    [JsonPropertyName("context")]
    public CompletionContext? Context { get; set; }
}

internal sealed class CompletionContext
{
    /// <summary>1 = Invoked (Ctrl+Space), 2 = TriggerCharacter, 3 = TriggerForIncompleteCompletions.</summary>
    [JsonPropertyName("triggerKind")]
    public int TriggerKind { get; set; } = 1;

    [JsonPropertyName("triggerCharacter")]
    public string? TriggerCharacter { get; set; }
}

internal sealed class CompletionList
{
    [JsonPropertyName("isIncomplete")]
    public bool IsIncomplete { get; set; }

    [JsonPropertyName("items")]
    public CompletionItem[] Items { get; set; } = [];
}

internal sealed class CompletionItem
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("kind")]
    public int? Kind { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("insertText")]
    public string? InsertText { get; set; }

    [JsonPropertyName("filterText")]
    public string? FilterText { get; set; }

    [JsonPropertyName("sortText")]
    public string? SortText { get; set; }
}

internal static class CompletionItemKind
{
    public const int Text = 1;
    public const int Method = 2;
    public const int Function = 3;
    public const int Constructor = 4;
    public const int Field = 5;
    public const int Variable = 6;
    public const int Class = 7;
    public const int Interface = 8;
    public const int Module = 9;
    public const int Property = 10;
    public const int Unit = 11;
    public const int Value = 12;
    public const int Enum = 13;
    public const int Keyword = 14;
    public const int Snippet = 15;
    public const int Color = 16;
    public const int File = 17;
    public const int Reference = 18;
    public const int Folder = 19;
    public const int EnumMember = 20;
    public const int Constant = 21;
    public const int Struct = 22;
    public const int Event = 23;
    public const int Operator = 24;
    public const int TypeParameter = 25;
}

// ── Document Sync ────────────────────────────────────────────

internal sealed class DidOpenTextDocumentParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentItem TextDocument { get; set; } = new();
}

internal sealed class DidChangeTextDocumentParams
{
    [JsonPropertyName("textDocument")]
    public VersionedTextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("contentChanges")]
    public TextDocumentContentChangeEvent[] ContentChanges { get; set; } = [];
}

internal sealed class TextDocumentContentChangeEvent
{
    /// <summary>Range of the document that changed. Null for full document replacement.</summary>
    [JsonPropertyName("range")]
    public LspRange? Range { get; set; }

    /// <summary>Length of the range that got replaced (deprecated but some servers use it).</summary>
    [JsonPropertyName("rangeLength")]
    public int? RangeLength { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

internal sealed class DidCloseTextDocumentParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();
}

// ── Navigation ──────────────────────────────────────────────

internal sealed class HoverParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("position")]
    public LspPosition Position { get; set; } = new();
}

internal sealed class HoverResult
{
    [JsonPropertyName("contents")]
    public MarkupContent Contents { get; set; } = new();

    [JsonPropertyName("range")]
    public LspRange? Range { get; set; }
}

internal sealed class MarkupContent
{
    /// <summary>"plaintext" or "markdown".</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "plaintext";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}

internal sealed class DefinitionParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("position")]
    public LspPosition Position { get; set; } = new();
}

internal sealed class Location
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();
}

internal sealed class LocationLink
{
    [JsonPropertyName("originSelectionRange")]
    public LspRange? OriginSelectionRange { get; set; }

    [JsonPropertyName("targetUri")]
    public string TargetUri { get; set; } = "";

    [JsonPropertyName("targetRange")]
    public LspRange TargetRange { get; set; } = new();

    [JsonPropertyName("targetSelectionRange")]
    public LspRange TargetSelectionRange { get; set; } = new();
}

internal sealed class ReferenceParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("position")]
    public LspPosition Position { get; set; } = new();

    [JsonPropertyName("context")]
    public ReferenceContext Context { get; set; } = new();
}

internal sealed class ReferenceContext
{
    [JsonPropertyName("includeDeclaration")]
    public bool IncludeDeclaration { get; set; }
}

internal sealed class DocumentHighlightParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("position")]
    public LspPosition Position { get; set; } = new();
}

internal sealed class DocumentHighlight
{
    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    /// <summary>1=Text, 2=Read, 3=Write.</summary>
    [JsonPropertyName("kind")]
    public int? Kind { get; set; }
}

// ── Editing ─────────────────────────────────────────────────

internal sealed class TextEdit
{
    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("newText")]
    public string NewText { get; set; } = "";
}

internal sealed class RenameParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("position")]
    public LspPosition Position { get; set; } = new();

    [JsonPropertyName("newName")]
    public string NewName { get; set; } = "";
}

internal sealed class PrepareRenameParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("position")]
    public LspPosition Position { get; set; } = new();
}

internal sealed class PrepareRenameResult
{
    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("placeholder")]
    public string Placeholder { get; set; } = "";
}

internal sealed class WorkspaceEdit
{
    [JsonPropertyName("changes")]
    public Dictionary<string, TextEdit[]>? Changes { get; set; }

    [JsonPropertyName("documentChanges")]
    public JsonElement? DocumentChanges { get; set; }
}

internal sealed class TextDocumentEdit
{
    [JsonPropertyName("textDocument")]
    public VersionedTextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("edits")]
    public TextEdit[] Edits { get; set; } = [];
}

internal sealed class Command
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("command")]
    public string CommandIdentifier { get; set; } = "";

    [JsonPropertyName("arguments")]
    public JsonElement[]? Arguments { get; set; }
}

internal sealed class CodeActionParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("context")]
    public CodeActionContext Context { get; set; } = new();
}

internal sealed class CodeActionContext
{
    [JsonPropertyName("diagnostics")]
    public LspDiagnostic[] Diagnostics { get; set; } = [];

    [JsonPropertyName("only")]
    public string[]? Only { get; set; }
}

internal sealed class CodeAction
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("diagnostics")]
    public LspDiagnostic[]? Diagnostics { get; set; }

    [JsonPropertyName("isPreferred")]
    public bool? IsPreferred { get; set; }

    [JsonPropertyName("edit")]
    public WorkspaceEdit? Edit { get; set; }

    [JsonPropertyName("command")]
    public Command? Command { get; set; }
}

internal sealed class FormattingOptions
{
    [JsonPropertyName("tabSize")]
    public int TabSize { get; set; } = 4;

    [JsonPropertyName("insertSpaces")]
    public bool InsertSpaces { get; set; } = true;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

internal sealed class DocumentFormattingParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("options")]
    public FormattingOptions Options { get; set; } = new();
}

internal sealed class DocumentRangeFormattingParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("options")]
    public FormattingOptions Options { get; set; } = new();
}

// ── Signature Help / Info / Structure ───────────────────────

internal sealed class SignatureHelpParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("position")]
    public LspPosition Position { get; set; } = new();

    [JsonPropertyName("context")]
    public SignatureHelpContext? Context { get; set; }
}

internal sealed class SignatureHelpContext
{
    /// <summary>1=Invoked, 2=TriggerCharacter, 3=ContentChange.</summary>
    [JsonPropertyName("triggerKind")]
    public int TriggerKind { get; set; } = 1;

    [JsonPropertyName("triggerCharacter")]
    public string? TriggerCharacter { get; set; }

    [JsonPropertyName("isRetrigger")]
    public bool IsRetrigger { get; set; }

    [JsonPropertyName("activeSignatureHelp")]
    public SignatureHelp? ActiveSignatureHelp { get; set; }
}

internal sealed class SignatureHelp
{
    [JsonPropertyName("signatures")]
    public SignatureInformation[] Signatures { get; set; } = [];

    [JsonPropertyName("activeSignature")]
    public int? ActiveSignature { get; set; }

    [JsonPropertyName("activeParameter")]
    public int? ActiveParameter { get; set; }
}

internal sealed class SignatureInformation
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("documentation")]
    public JsonElement? Documentation { get; set; }

    [JsonPropertyName("parameters")]
    public ParameterInformation[]? Parameters { get; set; }

    [JsonPropertyName("activeParameter")]
    public int? ActiveParameter { get; set; }
}

internal sealed class ParameterInformation
{
    /// <summary>String or [uint, uint] offset pair.</summary>
    [JsonPropertyName("label")]
    public JsonElement Label { get; set; }

    [JsonPropertyName("documentation")]
    public JsonElement? Documentation { get; set; }
}

internal sealed class DocumentSymbolParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();
}

internal sealed class DocumentSymbol
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("kind")]
    public int Kind { get; set; }

    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("selectionRange")]
    public LspRange SelectionRange { get; set; } = new();

    [JsonPropertyName("children")]
    public DocumentSymbol[]? Children { get; set; }
}

internal static class SymbolKind
{
    public const int File = 1;
    public const int Module = 2;
    public const int Namespace = 3;
    public const int Package = 4;
    public const int Class = 5;
    public const int Method = 6;
    public const int Property = 7;
    public const int Field = 8;
    public const int Constructor = 9;
    public const int Enum = 10;
    public const int Interface = 11;
    public const int Function = 12;
    public const int Variable = 13;
    public const int Constant = 14;
    public const int String = 15;
    public const int Number = 16;
    public const int Boolean = 17;
    public const int Array = 18;
    public const int Object = 19;
    public const int Key = 20;
    public const int Null = 21;
    public const int EnumMember = 22;
    public const int Struct = 23;
    public const int Event = 24;
    public const int Operator = 25;
    public const int TypeParameter = 26;
}

internal sealed class FoldingRangeParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();
}

internal sealed class FoldingRange
{
    [JsonPropertyName("startLine")]
    public int StartLine { get; set; }

    [JsonPropertyName("startCharacter")]
    public int? StartCharacter { get; set; }

    [JsonPropertyName("endLine")]
    public int EndLine { get; set; }

    [JsonPropertyName("endCharacter")]
    public int? EndCharacter { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }
}

internal sealed class SelectionRangeParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("positions")]
    public LspPosition[] Positions { get; set; } = [];
}

internal sealed class SelectionRange
{
    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("parent")]
    public SelectionRange? Parent { get; set; }
}

// ── Advanced ────────────────────────────────────────────────

internal sealed class InlayHintParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();
}

internal sealed class InlayHint
{
    [JsonPropertyName("position")]
    public LspPosition Position { get; set; } = new();

    /// <summary>String or InlayHintLabelPart[].</summary>
    [JsonPropertyName("label")]
    public JsonElement Label { get; set; }

    [JsonPropertyName("kind")]
    public int? Kind { get; set; }

    [JsonPropertyName("paddingLeft")]
    public bool? PaddingLeft { get; set; }

    [JsonPropertyName("paddingRight")]
    public bool? PaddingRight { get; set; }

    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }
}

internal sealed class InlayHintLabelPart
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("tooltip")]
    public JsonElement? Tooltip { get; set; }

    [JsonPropertyName("location")]
    public Location? Location { get; set; }

    [JsonPropertyName("command")]
    public Command? Command { get; set; }
}

internal static class InlayHintKind
{
    public const int Type = 1;
    public const int Parameter = 2;
}

internal sealed class CodeLensParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();
}

internal sealed class CodeLens
{
    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("command")]
    public Command? Command { get; set; }

    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }
}

internal sealed class CallHierarchyPrepareParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("position")]
    public LspPosition Position { get; set; } = new();
}

internal sealed class CallHierarchyItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("kind")]
    public int Kind { get; set; }

    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("selectionRange")]
    public LspRange SelectionRange { get; set; } = new();

    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }
}

internal sealed class CallHierarchyIncomingCallsParams
{
    [JsonPropertyName("item")]
    public CallHierarchyItem Item { get; set; } = new();
}

internal sealed class CallHierarchyIncomingCall
{
    [JsonPropertyName("from")]
    public CallHierarchyItem From { get; set; } = new();

    [JsonPropertyName("fromRanges")]
    public LspRange[] FromRanges { get; set; } = [];
}

internal sealed class CallHierarchyOutgoingCallsParams
{
    [JsonPropertyName("item")]
    public CallHierarchyItem Item { get; set; } = new();
}

internal sealed class CallHierarchyOutgoingCall
{
    [JsonPropertyName("to")]
    public CallHierarchyItem To { get; set; } = new();

    [JsonPropertyName("fromRanges")]
    public LspRange[] FromRanges { get; set; } = [];
}

internal sealed class TypeHierarchyPrepareParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("position")]
    public LspPosition Position { get; set; } = new();
}

internal sealed class TypeHierarchyItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("kind")]
    public int Kind { get; set; }

    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("selectionRange")]
    public LspRange SelectionRange { get; set; } = new();

    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }
}

internal sealed class TypeHierarchySupertypesParams
{
    [JsonPropertyName("item")]
    public TypeHierarchyItem Item { get; set; } = new();
}

internal sealed class TypeHierarchySubtypesParams
{
    [JsonPropertyName("item")]
    public TypeHierarchyItem Item { get; set; } = new();
}

internal sealed class DocumentLinkParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();
}

internal sealed class DocumentLink
{
    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("target")]
    public string? Target { get; set; }

    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }
}

// ── Document Save ────────────────────────────────────────────

internal sealed class DidSaveTextDocumentParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

// ── Progress ─────────────────────────────────────────────────

internal sealed class WorkDoneProgress
{
    [JsonPropertyName("token")]
    public JsonElement Token { get; set; }

    [JsonPropertyName("value")]
    public WorkDoneProgressValue? Value { get; set; }
}

internal sealed class WorkDoneProgressValue
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = ""; // "begin", "report", "end"

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("percentage")]
    public int? Percentage { get; set; }

    [JsonPropertyName("cancellable")]
    public bool? Cancellable { get; set; }
}
