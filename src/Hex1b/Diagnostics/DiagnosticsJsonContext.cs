using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.Diagnostics;

/// <summary>
/// Source-generated JSON serializer context for diagnostics protocol types.
/// Eliminates reflection-based serialization for native AOT compatibility.
/// </summary>
[JsonSerializable(typeof(DiagnosticsRequest))]
[JsonSerializable(typeof(DiagnosticsResponse))]
[JsonSerializable(typeof(DiagnosticNode))]
[JsonSerializable(typeof(DiagnosticRect))]
[JsonSerializable(typeof(DiagnosticPopupEntry))]
[JsonSerializable(typeof(DiagnosticAnchorInfo))]
[JsonSerializable(typeof(DiagnosticFocusInfo))]
[JsonSerializable(typeof(DiagnosticFocusableEntry))]
[JsonSerializable(typeof(DiagnosticTiming))]
[JsonSerializable(typeof(DiagnosticFrameInfo))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(bool))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class DiagnosticsJsonContext : JsonSerializerContext
{
}
