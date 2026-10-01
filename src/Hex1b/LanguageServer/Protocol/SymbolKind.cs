using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

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
