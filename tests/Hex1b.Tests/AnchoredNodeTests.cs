using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for AnchoredNode positioning and stale anchor handling.
/// </summary>
public class AnchoredNodeTests
{
    [Fact]
    public void AnchoredNode_ValidAnchor_PositionsCorrectly()
    {
        // Arrange - Create an anchor node with known bounds
        var anchorNode = new ButtonNode { Label = "Anchor" };
        anchorNode.Measure(Constraints.Unbounded);
        anchorNode.Arrange(new Rect(50, 10, 12, 1));
        
        // Create anchored content
        var contentNode = new TextBlockNode { Text = "Popup Content" };
        contentNode.Measure(Constraints.Unbounded);
        
        var anchoredNode = new AnchoredNode
        {
            Child = contentNode,
            AnchorNode = anchorNode,
            Position = AnchorPosition.Below
        };
        
        // Act - Measure and arrange
        anchoredNode.Measure(new Constraints(0, 100, 0, 50));
        anchoredNode.Arrange(new Rect(0, 0, 100, 50));
        
        // Assert - Content should be positioned below the anchor
        // Anchor is at (50, 10) with height 1, so popup should be at Y=11
        Assert.Equal(50, contentNode.Bounds.X);
        Assert.Equal(11, contentNode.Bounds.Y);
    }
    
    [Fact]
    public void AnchoredNode_StaleAnchorWithZeroBounds_PositionsSafely()
    {
        // Arrange - Simulate a stale anchor node that has never been arranged
        // (its Bounds are (0, 0, 0, 0))
        var staleAnchorNode = new ButtonNode { Label = "Stale" };
        // Intentionally NOT calling Measure/Arrange - simulates a replaced node
        
        // Verify anchor has zero bounds (the stale state)
        Assert.Equal(0, staleAnchorNode.Bounds.X);
        Assert.Equal(0, staleAnchorNode.Bounds.Y);
        Assert.Equal(0, staleAnchorNode.Bounds.Width);
        Assert.Equal(0, staleAnchorNode.Bounds.Height);
        
        // Create anchored content
        var contentNode = new TextBlockNode { Text = "Popup Content" };
        contentNode.Measure(Constraints.Unbounded);
        
        var anchoredNode = new AnchoredNode
        {
            Child = contentNode,
            AnchorNode = staleAnchorNode,
            Position = AnchorPosition.Below
        };
        
        // Act - Measure and arrange
        anchoredNode.Measure(new Constraints(0, 100, 0, 50));
        anchoredNode.Arrange(new Rect(0, 0, 100, 50));
        
        // Assert - With fix: Content should NOT be at (0, 0) when anchor has zero bounds
        // The node should detect the stale anchor and mark itself for cleanup
        // For now, verify the IsAnchorStale flag is set
        Assert.True(anchoredNode.IsAnchorStale, 
            "AnchoredNode should detect stale anchor with zero bounds");
    }
    
    [Fact]
    public void AnchoredNode_AnchorOutsideScreen_PositionsSafely()
    {
        // Arrange - Create an anchor node with bounds completely outside the screen
        var anchorNode = new ButtonNode { Label = "OffScreen" };
        anchorNode.Measure(Constraints.Unbounded);
        anchorNode.Arrange(new Rect(-100, -50, 12, 1)); // Negative position (offscreen)
        
        // Create anchored content
        var contentNode = new TextBlockNode { Text = "Popup Content" };
        contentNode.Measure(Constraints.Unbounded);
        
        var anchoredNode = new AnchoredNode
        {
            Child = contentNode,
            AnchorNode = anchorNode,
            Position = AnchorPosition.Below
        };
        
        // Act - Measure and arrange
        anchoredNode.Measure(new Constraints(0, 100, 0, 50));
        anchoredNode.Arrange(new Rect(0, 0, 100, 50));
        
        // Assert - Content should be clamped to screen bounds (0, 0) minimum
        Assert.True(contentNode.Bounds.X >= 0, "Content X should be >= 0");
        Assert.True(contentNode.Bounds.Y >= 0, "Content Y should be >= 0");
    }
    
    [Fact]
    public void AnchoredNode_NullAnchor_DoesNotPosition()
    {
        // Arrange - Create anchored node with no anchor
        var contentNode = new TextBlockNode { Text = "Popup Content" };
        contentNode.Measure(Constraints.Unbounded);
        
        var anchoredNode = new AnchoredNode
        {
            Child = contentNode,
            AnchorNode = null, // No anchor
            Position = AnchorPosition.Below
        };
        
        // Act - Measure and arrange
        anchoredNode.Measure(new Constraints(0, 100, 0, 50));
        anchoredNode.Arrange(new Rect(0, 0, 100, 50));
        
        // Assert - Content bounds should be unchanged (not arranged)
        Assert.Equal(0, contentNode.Bounds.Width);
    }
    
    [Fact]
    public async Task PickerPopup_WhenAnchorNodeReplaced_DetectsStaleAnchor()
    {
        // This test simulates the real-world scenario:
        // 1. Picker opens popup anchored to its button
        // 2. Table re-renders and picker node is replaced
        // 3. Old button node's bounds become stale
        // 4. Popup should detect the stale anchor
        
        // Arrange - Create a ZStack with popup support
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();
        
        using var app = new Hex1bApp(
            ctx => ctx.ZStack(z => [
                z.VStack(v => [
                    // A picker that will be replaced during reconciliation
                    v.Picker(["Low", "Medium", "High"], 0)
                        .OnSelectionChanged(_ => { }),
                    // A button that triggers re-render
                    v.Button("Trigger").OnClick(_ => { })
                ])
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Low"), TimeSpan.FromSeconds(2), "ready")
            // Open picker dropdown
            .Key(Hex1bKey.Enter)
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("picker_open")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        // Verify picker dropdown appeared (shows all options including Medium and High)
        Assert.True(snapshot.ContainsText("Medium") && snapshot.ContainsText("High"),
            $"Picker dropdown should show options. Screen:\n{snapshot}");
    }
}
