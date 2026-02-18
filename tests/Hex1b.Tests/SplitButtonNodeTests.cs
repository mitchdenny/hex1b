using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for SplitButtonNode rendering and behavior.
/// </summary>
public class SplitButtonNodeTests
{
    [Fact]
    public async Task SplitButton_RendersWithLabel()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 5)
            .Build();

        using var app = new Hex1bApp(
            ctx => ctx.SplitButton().PrimaryAction("Action", _ => { }),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Action"), TimeSpan.FromSeconds(10), "split button label")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var line = snapshot.GetLineTrimmed(0);
        Assert.Contains("Action", line);
    }

    [Fact]
    public async Task SplitButton_SecondaryActions_ShowsDropdownArrow()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 5)
            .Build();

        using var app = new Hex1bApp(
            ctx => ctx.SplitButton()
                .PrimaryAction("Action", _ => { })
                .SecondaryAction("Option A", _ => { }),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Action") && s.ContainsText("▼"), TimeSpan.FromSeconds(10), "dropdown arrow")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var line = snapshot.GetLineTrimmed(0);
        Assert.Contains("Action", line);
        Assert.Contains("▼", line);
    }

    [Fact]
    public async Task SplitButton_WithoutSecondaryActions_NoDropdownArrow()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 5)
            .Build();

        using var app = new Hex1bApp(
            ctx => ctx.SplitButton().PrimaryAction("Action", _ => { }),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Action"), TimeSpan.FromSeconds(10), "split button")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var line = snapshot.GetLineTrimmed(0);
        Assert.Contains("Action", line);
        Assert.DoesNotContain("▼", line);
    }

    [Fact]
    public void Measure_ReturnsCorrectSize()
    {
        var node = new SplitButtonNode { PrimaryLabel = "Test" };
        
        var size = node.Measure(Constraints.Unbounded);
        
        // "[ Test ]" = 8 chars (4 for brackets/spaces + 4 for label)
        Assert.Equal(8, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_SecondaryActions_IncludesArrow()
    {
        var node = new SplitButtonNode 
        { 
            PrimaryLabel = "Test",
            SecondaryActions = [new SplitButtonAction("A", _ => Task.CompletedTask)]
        };
        
        var size = node.Measure(Constraints.Unbounded);
        
        // "[ Test ▼ ]" = 10 chars (4 for brackets/spaces + 4 for label + 2 for " ▼")
        Assert.Equal(10, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void IsFocusable_ReturnsTrue()
    {
        var node = new SplitButtonNode();
        Assert.True(node.IsFocusable);
    }
}
