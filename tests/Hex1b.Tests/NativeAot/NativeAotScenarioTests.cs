using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Tests.NativeAot;

[TestClass]
[DoNotParallelize]
[TestCategory("NativeAot")]
[NativeAotEnabled]
public sealed class NativeAotScenarioTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public Task DirectTerminal_NativePublish_ExcludesTapeAndCreationCode()
    {
        const string source = """
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
            """;
        return ScenarioRunner.RunAsync(Scenario.DirectTerminal, source, TestContext, TestContext.CancellationToken);
    }

    [TestMethod]
    public Task ExistingTerminalTape_NativePublish_ExcludesCreationCode()
    {
        const string source = """
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
            """;
        return ScenarioRunner.RunAsync(Scenario.ExistingTerminalTape, source, TestContext, TestContext.CancellationToken);
    }

    [TestMethod]
    public Task CreatedTerminalTape_NativePublish_RetainsCreationAndCleansUp()
    {
        const string source = """
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
            """;
        return ScenarioRunner.RunAsync(Scenario.CreatedTerminalTape, source, TestContext, TestContext.CancellationToken);
    }
}
