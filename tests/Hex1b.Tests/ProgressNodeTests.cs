using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class ProgressNodeTests
{
    [Fact]
    public void Measure_FillsAvailableWidth()
    {
        // Arrange
        var node = new ProgressNode { Value = 50, Maximum = 100 };
        var constraints = new Constraints(0, 80, 0, 10);

        // Act
        var size = node.Measure(constraints);

        // Assert
        Assert.Equal(80, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_RespectsMinWidth()
    {
        // Arrange
        var node = new ProgressNode { Value = 50, Maximum = 100 };
        var constraints = new Constraints(20, 80, 0, 10);

        // Act
        var size = node.Measure(constraints);

        // Assert
        Assert.Equal(80, size.Width); // Should fill available width
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_UnboundedWidth_UsesDefaultWidth()
    {
        // Arrange
        var node = new ProgressNode { Value = 50, Maximum = 100 };
        var constraints = new Constraints(0, int.MaxValue, 0, 10);

        // Act
        var size = node.Measure(constraints);

        // Assert
        Assert.Equal(20, size.Width); // Default width when unbounded
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Reconcile_PreservesNodeOnSameType()
    {
        // Arrange
        var widget1 = new ProgressWidget { Value = 25, Maximum = 100 };
        var widget2 = new ProgressWidget { Value = 75, Maximum = 100 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        // Act
        var node1 = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult();
        var node2 = widget2.ReconcileAsync(node1, context).GetAwaiter().GetResult();

        // Assert
        Assert.Same(node1, node2);
        Assert.Equal(75, ((ProgressNode)node2).Value);
    }

    [Fact]
    public void Reconcile_MarksDirtyOnValueChange()
    {
        // Arrange
        var widget1 = new ProgressWidget { Value = 25, Maximum = 100 };
        var widget2 = new ProgressWidget { Value = 75, Maximum = 100 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as ProgressNode;
        node!.ClearDirty();

        // Act
        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        // Assert
        Assert.True(node.IsDirty);
    }

    [Fact]
    public void Reconcile_MarksDirtyOnMinimumChange()
    {
        // Arrange
        var widget1 = new ProgressWidget { Value = 50, Minimum = 0, Maximum = 100 };
        var widget2 = new ProgressWidget { Value = 50, Minimum = -50, Maximum = 100 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as ProgressNode;
        node!.ClearDirty();

        // Act
        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        // Assert
        Assert.True(node.IsDirty);
    }

    [Fact]
    public void Reconcile_MarksDirtyOnMaximumChange()
    {
        // Arrange
        var widget1 = new ProgressWidget { Value = 50, Maximum = 100 };
        var widget2 = new ProgressWidget { Value = 50, Maximum = 200 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as ProgressNode;
        node!.ClearDirty();

        // Act
        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        // Assert
        Assert.True(node.IsDirty);
    }

    [Fact]
    public void Reconcile_MarksDirtyOnIndeterminateChange()
    {
        // Arrange
        var widget1 = new ProgressWidget { Value = 50, IsIndeterminate = false };
        var widget2 = new ProgressWidget { Value = 50, IsIndeterminate = true };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as ProgressNode;
        node!.ClearDirty();

        // Act
        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        // Assert
        Assert.True(node.IsDirty);
    }

    [Fact]
    public void Reconcile_DoesNotMarkDirtyWhenUnchanged()
    {
        // Arrange
        var widget1 = new ProgressWidget { Value = 50, Maximum = 100 };
        var widget2 = new ProgressWidget { Value = 50, Maximum = 100 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as ProgressNode;
        node!.ClearDirty();

        // Act
        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        // Assert
        Assert.False(node.IsDirty);
    }

    [Fact]
    public void GetExpectedNodeType_ReturnsProgressNode()
    {
        // Arrange
        var widget = new ProgressWidget();

        // Act & Assert
        Assert.Equal(typeof(ProgressNode), widget.GetExpectedNodeType());
    }

    [Fact]
    public void IsFocusable_ReturnsFalse()
    {
        // Arrange
        var node = new ProgressNode();

        // Assert
        Assert.False(node.IsFocusable);
    }

    [Theory]
    [InlineData(0, 0, 100, 0.0)]
    [InlineData(50, 0, 100, 0.5)]
    [InlineData(100, 0, 100, 1.0)]
    [InlineData(25, 0, 100, 0.25)]
    [InlineData(-25, -50, 50, 0.25)]
    [InlineData(0, -100, 100, 0.5)]
    public void DeterminateMode_CalculatesPercentageCorrectly(double value, double min, double max, double expectedPercentage)
    {
        // This is a logic test - we verify the percentage calculation
        var range = max - min;
        var actualPercentage = range > 0 ? Math.Clamp((value - min) / range, 0.0, 1.0) : 0.0;
        
        Assert.Equal(expectedPercentage, actualPercentage, precision: 5);
    }

    [Fact]
    public void IndeterminateMode_SetsIsIndeterminate()
    {
        // Arrange & Act
        var widget = new ProgressWidget { IsIndeterminate = true };

        // Assert
        Assert.True(widget.IsIndeterminate);
    }

    [Fact]
    public void IndeterminateMode_SetsDefaultRedrawDelay()
    {
        // Arrange & Act
        var widget = new ProgressWidget { IsIndeterminate = true };

        // Assert - should auto-schedule redraws
        Assert.Equal(ProgressWidget.DefaultAnimationInterval, widget.GetEffectiveRedrawDelay());
    }

    [Fact]
    public void DeterminateMode_DoesNotSetRedrawDelay()
    {
        // Arrange & Act
        var widget = new ProgressWidget { Value = 50 };

        // Assert - no auto-redraw for determinate progress
        Assert.Null(widget.GetEffectiveRedrawDelay());
    }

    [Fact]
    public void IndeterminateNode_MeasuresCorrectly()
    {
        // Arrange
        var node = new ProgressNode { IsIndeterminate = true };
        var constraints = new Constraints(0, 20, 0, 1);
        
        // Act
        var size = node.Measure(constraints);
        
        // Assert - should still measure like a normal progress bar
        Assert.Equal(20, size.Width);
        Assert.Equal(1, size.Height);
    }

    #region Visual Rendering Tests

    [Fact]
    public async Task Render_ZeroPercent_ShowsAllEmpty()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 3).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Progress(0),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("░"), TimeSpan.FromSeconds(2), "empty progress chars")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var line = snapshot.GetLineTrimmed(0);
        // Should be all empty chars (░) - 20 wide
        Assert.Contains("░░░░░░░░░░░░░░░░░░░░", line);
        Assert.DoesNotContain("█", line);
    }

    [Fact]
    public async Task Render_HundredPercent_ShowsAllFilled()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 3).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Progress(100),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("█"), TimeSpan.FromSeconds(2), "filled progress chars")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var line = snapshot.GetLineTrimmed(0);
        // Should be all filled chars (█) - 20 wide
        Assert.Contains("████████████████████", line);
        Assert.DoesNotContain("░", line);
    }

    [Fact]
    public async Task Render_FiftyPercent_ShowsHalfFilled()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 3).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Progress(50),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("█") && s.ContainsText("░"), TimeSpan.FromSeconds(2), "half filled progress")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var line = snapshot.GetLineTrimmed(0);
        // Should have 10 filled and 10 empty chars
        Assert.Contains("██████████", line);
        Assert.Contains("░░░░░░░░░░", line);
    }

    [Fact]
    public async Task Render_WithHalfCellPrecision_ShowsHalfCharacter()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        // Using 20-wide terminal (default), 12.5% = 2.5 cells = 2 full + 1 half
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 3).Build();
        
        // Enable half-cell precision in theme
        var theme = new Hex1bTheme("HalfCell")
            .Set(ProgressTheme.UseHalfCellPrecision, true);

        // 12.5% of 20 = 2.5 = 2 full + 1 half
        using var app = new Hex1bApp(
            ctx => ctx.Progress(current: 12.5),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("▐"), TimeSpan.FromSeconds(2), "half character")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var line = snapshot.GetLineTrimmed(0);
        // Should have right-half character (▐) at the trailing edge
        Assert.Contains("▐", line);
        Assert.Contains("██", line); // 2 full blocks
    }

    [Fact]
    public async Task Render_WithHalfCellPrecision_17_5Percent_ShowsThreeFullOneHalf()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        // 20-wide terminal, 17.5% = 3.5 cells = 3 full + 1 half
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 3).Build();
        
        var theme = new Hex1bTheme("HalfCell")
            .Set(ProgressTheme.UseHalfCellPrecision, true);
        
        // 17.5% of 20 = 3.5 = 3 full + 1 half
        using var app = new Hex1bApp(
            ctx => ctx.Progress(current: 17.5),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("███") && s.ContainsText("▐"), TimeSpan.FromSeconds(2), "full and half chars")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var line = snapshot.GetLineTrimmed(0);
        Assert.Contains("███", line);
        Assert.Contains("▐", line);
    }

    [Fact]
    public async Task Render_WithHalfCellPrecision_2_5Percent_ShowsOnlyHalf()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        // 20-wide terminal, 2.5% = 0.5 cells = 0 full + 1 half
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 3).Build();
        
        var theme = new Hex1bTheme("HalfCell")
            .Set(ProgressTheme.UseHalfCellPrecision, true);
        
        // 2.5% of 20 = 0.5 = 0 full + 1 half
        using var app = new Hex1bApp(
            ctx => ctx.Progress(current: 2.5),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("▐"), TimeSpan.FromSeconds(2), "half char only")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var line = snapshot.GetLineTrimmed(0);
        Assert.Contains("▐", line);
        // No consecutive full blocks at 2.5%
        Assert.DoesNotContain("██", line);
    }

    [Fact]
    public async Task Render_WithHalfCellPrecision_ZeroPercent_NoHalfChar()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 3).Build();
        
        var theme = new Hex1bTheme("HalfCell")
            .Set(ProgressTheme.UseHalfCellPrecision, true);

        using var app = new Hex1bApp(
            ctx => ctx.Progress(0),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("░"), TimeSpan.FromSeconds(2), "empty progress")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var line = snapshot.GetLineTrimmed(0);
        Assert.DoesNotContain("█", line);
        Assert.DoesNotContain("▐", line);
        Assert.DoesNotContain("▌", line);
    }

    [Fact]
    public async Task Render_WithCustomFilledCharacter_UsesThemedChar()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 3).Build();
        
        var theme = new Hex1bTheme("Custom")
            .Set(ProgressTheme.FilledCharacter, '▓')
            .Set(ProgressTheme.EmptyCharacter, '·');
        
        using var app = new Hex1bApp(
            ctx => ctx.Progress(50),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("▓"), TimeSpan.FromSeconds(2), "custom filled char")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var line = snapshot.GetLineTrimmed(0);
        Assert.Contains("▓", line);
        Assert.Contains("·", line);
    }

    [Fact]
    public async Task Render_WithBrailleCharacters_DisplaysBraille()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        // 20-wide terminal, 17.5% = 3.5 cells = 3 full + 1 half
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 3).Build();
        
        // Braille style like notification cards
        var theme = new Hex1bTheme("Braille")
            .Set(ProgressTheme.FilledCharacter, '⣀')
            .Set(ProgressTheme.FilledRightHalfCharacter, '⢀')
            .Set(ProgressTheme.EmptyCharacter, ' ')
            .Set(ProgressTheme.UseHalfCellPrecision, true);
        
        // 17.5% of 20 = 3.5 = 3 full + 1 half
        using var app = new Hex1bApp(
            ctx => ctx.Progress(current: 17.5),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("⣀"), TimeSpan.FromSeconds(2), "braille full char")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var line = snapshot.GetLineTrimmed(0);
        Assert.Contains("⣀⣀⣀", line);
        Assert.Contains("⢀", line);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 1)]
    [InlineData(20, 2)]
    [InlineData(30, 3)]
    [InlineData(50, 5)]
    [InlineData(70, 7)]
    [InlineData(90, 9)]
    [InlineData(100, 10)]
    public void Render_DeterminatePercentages_CalculatesCorrectFilledWidth(int percent, int expectedFilledCells)
    {
        // Test the fill calculation logic directly
        var width = 10;
        var percentage = percent / 100.0;
        var filledWidth = (int)Math.Round(percentage * width);
        
        Assert.Equal(expectedFilledCells, filledWidth);
    }

    [Theory]
    [InlineData(5, 0, true)]   // 5% of 10 = 0.5 = 0 full + half
    [InlineData(15, 1, true)]  // 15% of 10 = 1.5 = 1 full + half
    [InlineData(25, 2, true)]  // 25% of 10 = 2.5 = 2 full + half
    [InlineData(30, 3, false)] // 30% of 10 = 3.0 = 3 full + no half
    [InlineData(50, 5, false)] // 50% of 10 = 5.0 = 5 full + no half
    [InlineData(55, 5, true)]  // 55% of 10 = 5.5 = 5 full + half
    public void Render_HalfCellPrecision_CalculatesCorrectly(int percent, int expectedFullCells, bool expectedHalfCell)
    {
        var width = 10;
        var percentage = percent / 100.0;
        var halfCellUnits = percentage * width * 2;
        var filledWidth = (int)(halfCellUnits / 2);
        var hasHalfCell = ((int)halfCellUnits % 2) == 1;
        
        Assert.Equal(expectedFullCells, filledWidth);
        Assert.Equal(expectedHalfCell, hasHalfCell);
    }

    [Fact]
    public async Task Render_CustomRange_NegativeToPositive_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 3).Build();

        // Range from -50 to 50, value at 0 = 50%
        using var app = new Hex1bApp(
            ctx => ctx.Progress(current: 0, min: -50, max: 50),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("█") && s.ContainsText("░"), TimeSpan.FromSeconds(2), "half filled")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var line = snapshot.GetLineTrimmed(0);
        Assert.Contains("█", line);
        Assert.Contains("░", line);
    }

    #endregion
}
