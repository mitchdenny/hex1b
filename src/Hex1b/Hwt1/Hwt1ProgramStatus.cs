namespace Hex1b;

internal sealed record Hwt1ProgramStatus(Hwt1ProgramStatusRecord[] Records)
{
    internal static Hwt1ProgramStatus From(TerminalProgramStatusSnapshot snapshot)
        => new(snapshot.Records.Select(Hwt1ProgramStatusRecord.From).ToArray());
}
