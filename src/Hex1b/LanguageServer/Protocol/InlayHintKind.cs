using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal static class InlayHintKind
{
    public const int Type = 1;
    public const int Parameter = 2;
}
