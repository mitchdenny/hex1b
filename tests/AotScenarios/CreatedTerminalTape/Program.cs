using AotScenarios;
using Hex1b.Automation;

using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var workload = new EchoWorkload();
var factoryCalls = 0;
var tape = new TapeParser().Parse("Set TypingSpeed 0 Type 'created' Wait+Screen /created/");
using (var result = await new TapePlayer().PlayAsync(tape, new TapePlaybackOptions
{
    TerminalFactory = builder =>
    {
        factoryCalls++;
        return builder.WithWorkload(workload).WithDimensions(80, 24).Build();
    }
}, cancellation.Token))
{
    if (factoryCalls != 1 || result.CompletedCommandCount != 3 || workload.Input != "created" ||
        !result.FinalSnapshot.ContainsText("created") || !workload.IsDisposed ||
        result.TerminalSize.Width != 80 || result.TerminalSize.Height != 24)
        throw new InvalidOperationException("Created-terminal playback, final snapshot, or owned cleanup failed.");
}
Console.WriteLine("NATIVE_AOT_OK:CreatedTerminalTape");
