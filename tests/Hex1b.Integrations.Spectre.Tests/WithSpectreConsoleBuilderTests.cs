using System.Text;
using Hex1b;
using Hex1b.Integrations.Spectre.SpectreConsole;
using Spectre.Console;

namespace Hex1b.Integrations.Spectre.Tests;

[TestClass]
public class WithSpectreConsoleBuilderTests
{
    [TestMethod]
    public async Task WithSpectreConsole_RunsUserDelegate_AndForwardsOutput()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithDimensions(80, 24)
            .WithHeadless()
            .WithSpectreConsole(async (console, ct) =>
            {
                console.MarkupLine("[bold]hi[/]");
                await Task.CompletedTask;
            })
            .Build();

        // RunAsync drives the workload to completion; a Spectre delegate that
        // returns immediately makes the run terminate after a single pump.
        await terminal.RunAsync().WaitAsync(TimeSpan.FromSeconds(5));

        var screenText = terminal.CreateSnapshot().GetScreenText();
        StringAssert.Contains(screenText, "hi");
    }

    [TestMethod]
    public async Task WithSpectreConsole_SyncOverload_RunsToCompletion()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithDimensions(80, 24)
            .WithHeadless()
            .WithSpectreConsole(console =>
            {
                console.WriteLine("plain text");
            })
            .Build();

        await terminal.RunAsync().WaitAsync(TimeSpan.FromSeconds(5));

        var screenText = terminal.CreateSnapshot().GetScreenText();
        StringAssert.Contains(screenText, "plain text");
    }

    [TestMethod]
    public void WithSpectreConsole_NullDelegate_Throws()
    {
        var builder = Hex1bTerminal.CreateBuilder();
        Assert.ThrowsExactly<ArgumentNullException>(
            () => builder.WithSpectreConsole((Func<global::Spectre.Console.IAnsiConsole, CancellationToken, Task>)null!));
    }

    [TestMethod]
    public void WithSpectreConsole_NullBuilder_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => ((Hex1bTerminalBuilder)null!).WithSpectreConsole((c, ct) => Task.CompletedTask));
    }
}
