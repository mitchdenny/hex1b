using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b.Automation;

namespace Hex1b;

// A baseline, not a history of shell events. Kept separate from the ANSI screen replay.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record Hmp1ActivityState
{
    internal const int MaxPayloadSize = 1024;

    public required Hmp1ProgressState Progress { get; init; }
    public required Hmp1ShellIntegrationState ShellIntegration { get; init; }

    internal static Hmp1ActivityState Default { get; } = new()
    {
        Progress = new() { State = 0, Percentage = null },
        ShellIntegration = new() { Phase = 0, LastExitCode = null }
    };

    internal static Hmp1ActivityState Capture(Hex1bTerminalSnapshot snapshot) => new()
    {
        Progress = new() { State = (int)snapshot.Progress.State, Percentage = snapshot.Progress.Percentage },
        ShellIntegration = new()
        {
            Phase = (int)snapshot.ShellIntegration.Phase,
            LastExitCode = snapshot.ShellIntegration.LastExitCode
        }
    };

    internal static Hmp1ActivityState Parse(ReadOnlyMemory<byte> payload)
    {
        if (payload.IsEmpty || payload.Length > MaxPayloadSize)
            throw new InvalidDataException("Invalid HMP activity checkpoint size.");
        try
        {
            using var document = JsonDocument.Parse(payload);
            RejectDuplicateProperties(document.RootElement);
            var state = JsonSerializer.Deserialize(payload.Span, Hmp1JsonContext.Default.Hmp1ActivityState)
                ?? throw new InvalidDataException("Missing HMP activity checkpoint.");
            if (state.Progress is not { State: >= 0 and <= 4 } progress ||
                state.ShellIntegration is not { Phase: >= 0 and <= 4 } shell ||
                (progress.State is 0 or 3 ? progress.Percentage is not null : progress.Percentage is not (>= 0 and <= 100)) ||
                (shell.Phase == 0 && shell.LastExitCode is not null))
                throw new InvalidDataException("Invalid HMP activity checkpoint state.");
            return state;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Malformed HMP activity checkpoint.", exception);
        }
    }

    internal string BuildProgressReplay() => Progress.Percentage is { } percentage
        ? string.Create(CultureInfo.InvariantCulture, $"\x1b]9;4;{Progress.State};{percentage}\x1b\\")
        : string.Create(CultureInfo.InvariantCulture, $"\x1b]9;4;{Progress.State}\x1b\\");

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Add(property.Name))
                throw new InvalidDataException("Duplicate HMP activity checkpoint property.");
            RejectDuplicateProperties(property.Value);
        }
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record Hmp1ProgressState
{
    public required int State { get; init; }
    public required int? Percentage { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record Hmp1ShellIntegrationState
{
    public required int Phase { get; init; }
    public required int? LastExitCode { get; init; }
}
