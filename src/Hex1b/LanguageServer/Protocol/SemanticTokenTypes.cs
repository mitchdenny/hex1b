using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

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
