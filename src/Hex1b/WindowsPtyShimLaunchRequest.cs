using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Hex1b;

internal sealed record WindowsPtyShimLaunchRequest(
    string FileName,
    string[] Arguments,
    string? WorkingDirectory,
    Dictionary<string, string> Environment,
    int Width,
    int Height,
    string SessionToken);
