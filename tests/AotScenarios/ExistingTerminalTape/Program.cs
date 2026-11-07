using AotScenarios;
using Hex1b;
using Hex1b.Automation;

using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var workload = new EchoWorkload();
await using (var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
{
    Width = 80,
    Height = 24,
    WorkloadAdapter = workload
}))
{
    var tape = new TapeParser().Parse("Set TypingSpeed 0 Type 'borrowed' Wait+Screen /borrowed/");
    using (var result = await new TapePlayer().PlayAsync(tape, terminal, cancellationToken: cancellation.Token))
    {
        if (result.CompletedCommandCount != 3 || !result.FinalSnapshot.ContainsText("borrowed") ||
            result.ProcessExitCode is not null || workload.Input != "borrowed" || workload.IsDisposed)
            throw new InvalidOperationException("Existing-terminal tape playback failed or took ownership.");
    }
    using var continued = await new Hex1bTerminalInputSequenceBuilder().Type("-continued")
        .WaitUntil(s => s.ContainsText("borrowed-continued"), TimeSpan.FromSeconds(5), "continued borrowed use")
        .Build().ApplyAsync(terminal, cancellation.Token);
    if (workload.Input != "borrowed-continued" || !continued.ContainsText("borrowed-continued") || workload.IsDisposed)
        throw new InvalidOperationException("Disposing the tape result prevented continued terminal use.");
}
if (!workload.IsDisposed)
    throw new InvalidOperationException("Caller-owned terminal did not clean up its workload.");
Console.WriteLine("NATIVE_AOT_OK:ExistingTerminalTape");
