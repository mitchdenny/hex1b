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
}

internal sealed class TextDocumentClientCapabilities
{
    [JsonPropertyName("semanticTokens")]
    public SemanticTokensClientCapabilities? SemanticTokens { get; set; } = new();

    [JsonPropertyName("completion")]
    public CompletionClientCapabilities? Completion { get; set; } = new();

    [JsonPropertyName("publishDiagnostics")]
    public PublishDiagnosticsClientCapabilities? PublishDiagnostics { get; set; } = new();
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

internal sealed class CompletionClientCapabilities;
internal sealed class PublishDiagnosticsClientCapabilities;

internal sealed class InitializeResult
{
    [JsonPropertyName("capabilities")]
    public ServerCapabilities Capabilities { get; set; } = new();
}

internal sealed class ServerCapabilities
{
    [JsonPropertyName("semanticTokensProvider")]
    public System.Text.Json.JsonElement? SemanticTokensProvider { get; set; }

    [JsonPropertyName("completionProvider")]
    public System.Text.Json.JsonElement? CompletionProvider { get; set; }

    [JsonPropertyName("textDocumentSync")]
    public System.Text.Json.JsonElement? TextDocumentSync { get; set; }
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
    public const int Keyword = 14;
    public const int Snippet = 15;
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
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

internal sealed class DidCloseTextDocumentParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();
}
