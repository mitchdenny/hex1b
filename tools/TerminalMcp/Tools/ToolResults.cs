using System.Text.Json.Serialization;

namespace TerminalMcp.Tools;

public class StartTerminalResult
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("sessionId")]
    public required string? SessionId { get; init; }

    [JsonPropertyName("processId")]
    public required int? ProcessId { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("command")]
    public required string Command { get; init; }

    [JsonPropertyName("arguments")]
    public required string[] Arguments { get; init; }

    [JsonPropertyName("workingDirectory")]
    public required string? WorkingDirectory { get; init; }

    [JsonPropertyName("width")]
    public required int Width { get; init; }

    [JsonPropertyName("height")]
    public required int Height { get; init; }
}

public class StopTerminalResult
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("hadAlreadyExited")]
    public bool HadAlreadyExited { get; init; }

    [JsonPropertyName("exitCode")]
    public int? ExitCode { get; init; }
}

public class RemoveSessionResult
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("wasRunning")]
    public bool WasRunning { get; init; }
}

public class SendInputResult
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("charactersSent")]
    public int CharactersSent { get; init; }
}

public class ResizeTerminalResult
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("oldWidth")]
    public int OldWidth { get; init; }

    [JsonPropertyName("oldHeight")]
    public int OldHeight { get; init; }

    [JsonPropertyName("newWidth")]
    public int NewWidth { get; init; }

    [JsonPropertyName("newHeight")]
    public int NewHeight { get; init; }
}

public class CaptureTextResult
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; init; }

    [JsonPropertyName("width")]
    public int Width { get; init; }

    [JsonPropertyName("height")]
    public int Height { get; init; }

    [JsonPropertyName("hasExited")]
    public bool HasExited { get; init; }

    [JsonPropertyName("exitCode")]
    public int? ExitCode { get; init; }
}

public class CaptureScreenshotResult
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("savedPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SavedPath { get; init; }

    [JsonPropertyName("pngPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PngPath { get; init; }

    [JsonPropertyName("width")]
    public int Width { get; init; }

    [JsonPropertyName("height")]
    public int Height { get; init; }

    [JsonPropertyName("hasExited")]
    public bool HasExited { get; init; }

    [JsonPropertyName("exitCode")]
    public int? ExitCode { get; init; }
}

public class WaitForTextResult
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("found")]
    public required bool Found { get; init; }

    [JsonPropertyName("currentText")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CurrentText { get; init; }
}

public class ListTerminalsResult
{
    [JsonPropertyName("sessionCount")]
    public required int SessionCount { get; init; }

    [JsonPropertyName("sessions")]
    public required TerminalSessionInfo[] Sessions { get; init; }
}

public class TerminalSessionInfo
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("processId")]
    public required int ProcessId { get; init; }

    [JsonPropertyName("command")]
    public required string Command { get; init; }

    [JsonPropertyName("arguments")]
    public required string[] Arguments { get; init; }

    [JsonPropertyName("workingDirectory")]
    public required string? WorkingDirectory { get; init; }

    [JsonPropertyName("width")]
    public required int Width { get; init; }

    [JsonPropertyName("height")]
    public required int Height { get; init; }

    [JsonPropertyName("startedAt")]
    public required DateTimeOffset StartedAt { get; init; }

    [JsonPropertyName("hasExited")]
    public required bool HasExited { get; init; }

    [JsonPropertyName("exitCode")]
    public required int? ExitCode { get; init; }

    [JsonPropertyName("runningFor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TimeSpan? RunningFor { get; init; }
}
