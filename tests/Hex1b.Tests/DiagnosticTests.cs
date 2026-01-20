using Hex1b.Input;
using Hex1b.Automation;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Diagnostic tests to understand test failures.
/// These tests are slow/exploratory and should be skipped in regular test runs.
/// </summary>
public class DiagnosticTests
{
    [Fact(Skip = "Diagnostic test - slow and uses Task.Delay, not suitable for CI")]
    public async Task Diag_VStackRendering()
    {
        var logFile = "/tmp/hex1b-diag.log";
        void Log(string msg) => File.AppendAllText(logFile, msg + "\n");
        File.WriteAllText(logFile, "");  // Clear previous log
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("First Line"),
                    v.Text("Second Line"),
                    v.Text("Third Line")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        Log("Starting app...");
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait a bit for rendering
        await Task.Delay(100);
        var snapshot1 = terminal.CreateSnapshot();
        Log($"After 100ms: ContainsFirst={snapshot1.ContainsText("First Line")} ContainsThird={snapshot1.ContainsText("Third Line")}");
        Log($"Buffer (first 200 chars): '{snapshot1.GetText().Substring(0, Math.Min(200, snapshot1.GetText().Length))}'");

        // Wait more
        await Task.Delay(500);
        var snapshot2 = terminal.CreateSnapshot();
        Log($"After 600ms: ContainsFirst={snapshot2.ContainsText("First Line")} ContainsThird={snapshot2.ContainsText("Third Line")}");
        Log($"Buffer (first 200 chars): '{snapshot2.GetText().Substring(0, Math.Min(200, snapshot2.GetText().Length))}'");

        // Now send Ctrl+C
        Log("Sending Ctrl+C...");
        workload.SendKey(Hex1bKey.C, '\x03', Hex1bModifiers.Control);

        try
        {
            await runTask.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Log("App exited normally");
        }
        catch (TimeoutException)
        {
            Log("App did not exit within 2s");
        }

        var snapshot3 = terminal.CreateSnapshot();
        Log($"After exit: ContainsFirst={snapshot3.ContainsText("First Line")} ContainsThird={snapshot3.ContainsText("Third Line")}");
        Log($"Buffer (first 200 chars): '{snapshot3.GetText().Substring(0, Math.Min(200, snapshot3.GetText().Length))}'");
        
        Assert.True(snapshot2.ContainsText("First Line"), "Content should be visible during app run");
    }
}
