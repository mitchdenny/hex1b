#pragma warning disable HEX1B_SIXEL // Testing experimental Sixel API

using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Tests;

public class SixelNodeTests : IDisposable
{
    public SixelNodeTests()
    {
        // Reset global state before each test for proper isolation
        SixelNode.ResetGlobalSixelDetection();
    }

    public void Dispose()
    {
        // Also reset after each test to be safe
        SixelNode.ResetGlobalSixelDetection();
    }

    [Fact]
    public void Measure_WithRequestedDimensions_ReturnsRequestedSize()
    {
        var node = new SixelNode
        {
            RequestedWidth = 50,
            RequestedHeight = 25
        };
        node.SetSixelSupport(true); // Simulate Sixel support

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(50, size.Width);
        Assert.Equal(25, size.Height);
    }

    [Fact]
    public void Measure_WithoutRequestedDimensions_ReturnsDefaultSize()
    {
        var node = new SixelNode();
        node.SetSixelSupport(true);

        var size = node.Measure(Constraints.Unbounded);

        // Default size is 40x20
        Assert.Equal(40, size.Width);
        Assert.Equal(20, size.Height);
    }

    [Fact]
    public void Measure_WithFallback_WhenSixelNotSupported_ReturnsFallbackSize()
    {
        var fallbackNode = new TextBlockNode { Text = "Fallback text" };
        var node = new SixelNode
        {
            Fallback = fallbackNode
        };
        node.SetSixelSupport(false);

        var size = node.Measure(Constraints.Unbounded);

        // Should return fallback's measured size
        Assert.True(size.Width > 0);
    }

    [Fact]
    public void SetSixelSupport_True_SetsSupported()
    {
        var node = new SixelNode();
        
        node.SetSixelSupport(true);
        
        // Should not throw and should render Sixel (tested indirectly)
        using var terminal = new Hex1bTerminal(80, 24);
        var context = new Hex1bRenderContext(terminal.WorkloadAdapter);
        node.Arrange(new Rect(0, 0, 40, 20));
        node.Render(context);
        terminal.FlushOutput();
        
        // With no image data, should show "[No image data]"
        Assert.Contains("[No image data]", terminal.RawOutput);
    }

    [Fact]
    public void SetSixelSupport_False_RendersFallback()
    {
        var node = new SixelNode
        {
            Fallback = new TextBlockNode { Text = "Fallback content" }
        };
        node.SetSixelSupport(false);
        
        using var terminal = new Hex1bTerminal(80, 24);
        var context = new Hex1bRenderContext(terminal.WorkloadAdapter);
        node.Fallback.Arrange(new Rect(0, 0, 40, 1));
        node.Arrange(new Rect(0, 0, 40, 20));
        node.Render(context);
        terminal.FlushOutput();
        
        Assert.Contains("Fallback content", terminal.RawOutput);
    }

    [Fact]
    public void ResetSixelDetection_ResetsState()
    {
        var node = new SixelNode();
        node.SetSixelSupport(true);
        
        node.ResetSixelDetection();
        
        // After reset, node should query again on next render
        using var terminal = new Hex1bTerminal(80, 24);
        var context = new Hex1bRenderContext(terminal.WorkloadAdapter);
        node.Arrange(new Rect(0, 0, 40, 20));
        node.Render(context);
        terminal.FlushOutput();
        
        // Should send DA1 query
        Assert.Contains("\x1b[c", terminal.RawOutput);
    }

    [Fact]
    public void Render_WhenWaitingForResponse_ShowsLoadingMessage()
    {
        var node = new SixelNode();
        // Don't set support - node should query
        
        using var terminal = new Hex1bTerminal(80, 24);
        var context = new Hex1bRenderContext(terminal.WorkloadAdapter);
        node.Arrange(new Rect(0, 0, 40, 20));
        
        // First render sends query
        node.Render(context);
        terminal.ClearRawOutput();
        
        // Second render while waiting shows loading
        node.Render(context);
        terminal.FlushOutput();
        Assert.Contains("Checking Sixel support", terminal.RawOutput);
    }

    [Fact]
    public void HandleTerminalResponse_WithSixelSupport_EnablesSixel()
    {
        var node = new SixelNode();
        
        using var terminal = new Hex1bTerminal(80, 24);
        var context = new Hex1bRenderContext(terminal.WorkloadAdapter);
        node.Arrange(new Rect(0, 0, 40, 20));
        
        // Start query
        node.Render(context);
        
        // Simulate terminal response with Sixel support (;4; indicates graphics)
        SixelNode.HandleDA1Response("\x1b[?62;4;6;22c");
        
        terminal.ClearRawOutput();
        node.Render(context);
        terminal.FlushOutput();
        
        // Should now render Sixel (or no image data message)
        Assert.Contains("[No image data]", terminal.RawOutput);
    }

    [Fact]
    public void HandleTerminalResponse_WithoutSixelSupport_RendersFallback()
    {
        var node = new SixelNode
        {
            Fallback = new TextBlockNode { Text = "No Sixel" }
        };
        
        using var terminal = new Hex1bTerminal(80, 24);
        var context = new Hex1bRenderContext(terminal.WorkloadAdapter);
        node.Fallback.Arrange(new Rect(0, 0, 40, 1));
        node.Arrange(new Rect(0, 0, 40, 20));
        
        // Start query
        node.Render(context);
        
        // Simulate terminal response without Sixel support
        SixelNode.HandleDA1Response("\x1b[?62;6;22c");
        
        terminal.ClearRawOutput();
        node.Render(context);
        terminal.FlushOutput();
        
        Assert.Contains("No Sixel", terminal.RawOutput);
    }

    [Fact]
    public void GetFocusableNodes_WhenFallbackHasFocusables_ReturnsThem()
    {
        var buttonNode = new ButtonNode { Label = "Test" };
        var fallback = new VStackNode();
        fallback.Children = [buttonNode];
        
        var node = new SixelNode { Fallback = fallback };
        node.SetSixelSupport(false);
        
        var focusables = node.GetFocusableNodes().ToList();
        
        Assert.Contains(buttonNode, focusables);
    }

    [Fact]
    public void GetFocusableNodes_WhenSixelSupported_ReturnsEmpty()
    {
        var buttonNode = new ButtonNode { Label = "Test" };
        var fallback = new VStackNode();
        fallback.Children = [buttonNode];
        
        var node = new SixelNode { Fallback = fallback };
        node.SetSixelSupport(true);
        
        var focusables = node.GetFocusableNodes().ToList();
        
        // Sixel widget itself is not focusable, and fallback is not used
        Assert.Empty(focusables);
    }

    [Fact]
    public async Task HandleInput_WhenShowingFallback_DelegatesToFallback()
    {
        var clickedCount = 0;
        var buttonNode = new ButtonNode 
        { 
            Label = "Test",
            ClickAction = _ => { clickedCount++; return Task.CompletedTask; },
            IsFocused = true
        };
        
        var node = new SixelNode { Fallback = buttonNode };
        node.SetSixelSupport(false);
        
        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        focusRing.EnsureFocus();
        var routerState = new InputRouterState();
        
        // Use InputRouter to route input to the focused child in the fallback
        var enterEvent = new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None);
        var result = await InputRouter.RouteInputAsync(node, enterEvent, focusRing, routerState);
        
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, clickedCount);
    }

    [Fact]
    public void Render_WithSixelData_OutputsSixelSequence()
    {
        var node = new SixelNode
        {
            ImageData = "#0;2;100;0;0#0~~~~~~"
        };
        node.SetSixelSupport(true);
        
        using var terminal = new Hex1bTerminal(80, 24);
        var context = new Hex1bRenderContext(terminal.WorkloadAdapter);
        node.Arrange(new Rect(0, 0, 40, 20));
        node.Render(context);
        terminal.FlushOutput();
        
        // Should wrap in Sixel DCS sequence: ESC P q ... ESC \
        Assert.Contains("\x1bPq", terminal.RawOutput);
        Assert.Contains("\x1b\\", terminal.RawOutput);
    }

    [Fact]
    public void Render_WithPreformattedSixelData_OutputsAsIs()
    {
        // Data already has DCS header
        var sixelData = "\x1bPq#0;2;100;0;0#0~~~~~~\x1b\\";
        var node = new SixelNode
        {
            ImageData = sixelData
        };
        node.SetSixelSupport(true);
        
        using var terminal = new Hex1bTerminal(80, 24);
        var context = new Hex1bRenderContext(terminal.WorkloadAdapter);
        node.Arrange(new Rect(0, 0, 40, 20));
        node.Render(context);
        terminal.FlushOutput();
        
        // Should output as-is without double-wrapping
        Assert.Equal(1, CountOccurrences(terminal.RawOutput, "\x1bPq"));
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
