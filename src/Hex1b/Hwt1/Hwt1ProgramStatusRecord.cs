namespace Hex1b;

internal sealed record Hwt1ProgramStatusRecord(
    string Id, string State, string? Kind, int? Progress, string? App,
    string? Title, string? Message, string? EffectiveApp)
{
    internal static Hwt1ProgramStatusRecord From(TerminalProgramStatusRecord record) => new(
        record.Id,
        record.State switch
        {
            TerminalProgramStatusState.Idle => "idle",
            TerminalProgramStatusState.Working => "working",
            TerminalProgramStatusState.Blocked => "blocked",
            TerminalProgramStatusState.Done => "done",
            TerminalProgramStatusState.Error => "error",
            _ => throw new InvalidDataException("Unknown program status state.")
        },
        record.Kind switch
        {
            null => null,
            TerminalProgramStatusKind.Permission => "permission",
            TerminalProgramStatusKind.Question => "question",
            TerminalProgramStatusKind.Auth => "auth",
            _ => throw new InvalidDataException("Unknown program status kind.")
        },
        record.Progress, record.App, record.Title, record.Message, record.EffectiveApp);
}
