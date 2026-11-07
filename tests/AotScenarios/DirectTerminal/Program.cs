using System.Text;
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
    await terminal.SendInputAsync(Encoding.UTF8.GetBytes("direct-input"), cancellation.Token);
    using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
        .WaitUntil(s => s.ContainsText("direct-input"), TimeSpan.FromSeconds(5), "direct echo")
        .Build().ApplyAsync(terminal, cancellation.Token);
    if (workload.Input != "direct-input" || !snapshot.ContainsText("direct-input"))
        throw new InvalidOperationException("Direct terminal did not echo the automated input.");
}
if (!workload.IsDisposed)
    throw new InvalidOperationException("Direct terminal did not dispose its workload.");
Console.WriteLine("NATIVE_AOT_OK:DirectTerminal");
