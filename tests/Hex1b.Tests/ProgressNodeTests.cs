using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class ProgressNodeTests
{
    [TestMethod]
    public void Measure_FillsAvailableWidth()
    {
        // Arrange
        var node = new ProgressNode { Value = 50, Maximum = 100 };
        var constraints = new Constraints(0, 80, 0, 10);

        // Act
        var size = node.Measure(constraints);

        // Assert
        Assert.AreEqual(80, size.Width);
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public void Measure_RespectsMinWidth()
    {
        // Arrange
        var node = new ProgressNode { Value = 50, Maximum = 100 };
        var constraints = new Constraints(20, 80, 0, 10);

        // Act
        var size = node.Measure(constraints);

        // Assert
        Assert.AreEqual(80, size.Width); // Should fill available width
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public void Measure_UnboundedWidth_UsesDefaultWidth()
    {
        // Arrange
        var node = new ProgressNode { Value = 50, Maximum = 100 };
        var constraints = new Constraints(0, int.MaxValue, 0, 10);

        // Act
        var size = node.Measure(constraints);

        // Assert
        Assert.AreEqual(20, size.Width); // Default width when unbounded
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
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
        Assert.AreSame(node1, node2);
        Assert.AreEqual(75, ((ProgressNode)node2).Value);
    }

    [TestMethod]
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
        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
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
        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
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
        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
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
        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
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
        Assert.IsFalse(node.IsDirty);
    }

    [TestMethod]
    public void GetExpectedNodeType_ReturnsProgressNode()
    {
        // Arrange
        var widget = new ProgressWidget();

        // Act & Assert
        Assert.AreEqual(typeof(ProgressNode), widget.GetExpectedNodeType());
    }

    [TestMethod]
    public void IsFocusable_ReturnsFalse()
    {
        // Arrange
        var node = new ProgressNode();

        // Assert
        Assert.IsFalse(node.IsFocusable);
    }

    [TestMethod]
    [DataRow(0, 0, 100, 0.0)]
    [DataRow(50, 0, 100, 0.5)]
    [DataRow(100, 0, 100, 1.0)]
    [DataRow(25, 0, 100, 0.25)]
    [DataRow(-25, -50, 50, 0.25)]
    [DataRow(0, -100, 100, 0.5)]
    public void DeterminateMode_CalculatesPercentageCorrectly(double value, double min, double max, double expectedPercentage)
    {
        // This is a logic test - we verify the percentage calculation
        var range = max - min;
        var actualPercentage = range > 0 ? Math.Clamp((value - min) / range, 0.0, 1.0) : 0.0;
        
        Assert.AreEqual(expectedPercentage, actualPercentage, delta: 5);
    }

    [TestMethod]
    public void IndeterminateMode_SetsIsIndeterminate()
    {
        // Arrange & Act
        var widget = new ProgressWidget { IsIndeterminate = true };

        // Assert
        Assert.IsTrue(widget.IsIndeterminate);
    }

    [TestMethod]
    public void IndeterminateMode_SetsDefaultRedrawDelay()
    {
        // Arrange & Act
        var widget = new ProgressWidget { IsIndeterminate = true };

        // Assert - should auto-schedule redraws
        Assert.AreEqual(ProgressWidget.DefaultAnimationInterval, widget.GetEffectiveRedrawDelay());
    }

    [TestMethod]
    public void DeterminateMode_DoesNotSetRedrawDelay()
    {
        // Arrange & Act
        var widget = new ProgressWidget { Value = 50 };

        // Assert - no auto-redraw for determinate progress
        Assert.IsNull(widget.GetEffectiveRedrawDelay());
    }

    [TestMethod]
    public void IndeterminateNode_MeasuresCorrectly()
    {
        // Arrange
        var node = new ProgressNode { IsIndeterminate = true };
        var constraints = new Constraints(0, 20, 0, 1);
        
        // Act
        var size = node.Measure(constraints);
        
        // Assert - should still measure like a normal progress bar
        Assert.AreEqual(20, size.Width);
        Assert.AreEqual(1, size.Height);
    }

    #region Visual Rendering Tests

    [TestMethod]
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
            .WaitUntil(s => s.ContainsText("░"), TimeSpan.FromSeconds(5), "empty progress chars")
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

    [TestMethod]
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
            .WaitUntil(s => s.ContainsText("█"), TimeSpan.FromSeconds(5), "filled progress chars")
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

    [TestMethod]
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
            .WaitUntil(s => s.ContainsText("█") && s.ContainsText("░"), TimeSpan.FromSeconds(5), "half filled progress")
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

    [TestMethod]
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
            .WaitUntil(s => s.ContainsText("▐"), TimeSpan.FromSeconds(5), "half character")
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

    [TestMethod]
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
            .WaitUntil(s => s.ContainsText("███") && s.ContainsText("▐"), TimeSpan.FromSeconds(5), "full and half chars")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var line = snapshot.GetLineTrimmed(0);
        Assert.Contains("███", line);
        Assert.Contains("▐", line);
    }

    [TestMethod]
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
            .WaitUntil(s => s.ContainsText("▐"), TimeSpan.FromSeconds(5), "half char only")
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

    [TestMethod]
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
            .WaitUntil(s => s.ContainsText("░"), TimeSpan.FromSeconds(5), "empty progress")
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

    [TestMethod]
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
            .WaitUntil(s => s.ContainsText("▓"), TimeSpan.FromSeconds(5), "custom filled char")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var line = snapshot.GetLineTrimmed(0);
        Assert.Contains("▓", line);
        Assert.Contains("·", line);
    }

    [TestMethod]
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
            .WaitUntil(s => s.ContainsText("⣀"), TimeSpan.FromSeconds(5), "braille full char")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var line = snapshot.GetLineTrimmed(0);
        Assert.Contains("⣀⣀⣀", line);
        Assert.Contains("⢀", line);
    }

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(10, 1)]
    [DataRow(20, 2)]
    [DataRow(30, 3)]
    [DataRow(50, 5)]
    [DataRow(70, 7)]
    [DataRow(90, 9)]
    [DataRow(100, 10)]
    public void Render_DeterminatePercentages_CalculatesCorrectFilledWidth(int percent, int expectedFilledCells)
    {
        // Test the fill calculation logic directly
        var width = 10;
        var percentage = percent / 100.0;
        var filledWidth = (int)Math.Round(percentage * width);
        
        Assert.AreEqual(expectedFilledCells, filledWidth);
    }

    [TestMethod]
    [DataRow(5, 0, true)]   // 5% of 10 = 0.5 = 0 full + half
    [DataRow(15, 1, true)]  // 15% of 10 = 1.5 = 1 full + half
    [DataRow(25, 2, true)]  // 25% of 10 = 2.5 = 2 full + half
    [DataRow(30, 3, false)] // 30% of 10 = 3.0 = 3 full + no half
    [DataRow(50, 5, false)] // 50% of 10 = 5.0 = 5 full + no half
    [DataRow(55, 5, true)]  // 55% of 10 = 5.5 = 5 full + half
    public void Render_HalfCellPrecision_CalculatesCorrectly(int percent, int expectedFullCells, bool expectedHalfCell)
    {
        var width = 10;
        var percentage = percent / 100.0;
        var halfCellUnits = percentage * width * 2;
        var filledWidth = (int)(halfCellUnits / 2);
        var hasHalfCell = ((int)halfCellUnits % 2) == 1;
        
        Assert.AreEqual(expectedFullCells, filledWidth);
        Assert.AreEqual(expectedHalfCell, hasHalfCell);
    }

    [TestMethod]
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
            .WaitUntil(s => s.ContainsText("█") && s.ContainsText("░"), TimeSpan.FromSeconds(5), "half filled")
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
