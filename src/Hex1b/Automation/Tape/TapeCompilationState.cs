namespace Hex1b.Automation;

internal sealed class TapeCompilationState(TimeProvider clock)
{
    internal TimeSpan TypingSpeed { get; set; } = TimeSpan.FromMilliseconds(50);
    internal TimeSpan WaitTimeout { get; set; } = TimeSpan.FromSeconds(15);
    internal string WaitPattern { get; set; } = ">$";
    internal bool InPreamble { get; set; } = true;
    internal TapePlayContext Context => new(TypingSpeed, WaitTimeout, clock);
    internal TapePlayContext CreateContext(Func<TapeCommandResult> prepareBuiltIn) =>
        new(TypingSpeed, WaitTimeout, clock, prepareBuiltIn);
}
