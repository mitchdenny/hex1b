using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.Diagnostics;

/// <summary>
/// JSON serialization options for diagnostics protocol.
/// </summary>
internal static class DiagnosticsJsonOptions
{
    public static readonly JsonSerializerOptions Default = DiagnosticsJsonContext.Default.Options;
}
