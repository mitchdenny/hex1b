#pragma warning disable HEX1B_SIXEL // Testing experimental Sixel API

using Hex1b;
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
        var mockOutput = new MockTerminalOutput();
        var context = new Hex1bRenderContext(mockOutput);
        node.Arrange(new Rect(0, 0, 40, 20));
        node.Render(context);
        
        // With no image data, should show "[No image data]"
        Assert.Contains("[No image data]", mockOutput.Output);
    }

    [Fact]
    public void SetSixelSupport_False_RendersFallback()
    {
        var node = new SixelNode
        {
            Fallback = new TextBlockNode { Text = "Fallback content" }
        };
        node.SetSixelSupport(false);
        
        var mockOutput = new MockTerminalOutput();
        var context = new Hex1bRenderContext(mockOutput);
        node.Fallback.Arrange(new Rect(0, 0, 40, 1));
        node.Arrange(new Rect(0, 0, 40, 20));
        node.Render(context);
        
        Assert.Contains("Fallback content", mockOutput.Output);
    }

    [Fact]
    public void ResetSixelDetection_ResetsState()
    {
        var node = new SixelNode();
        node.SetSixelSupport(true);
        
        node.ResetSixelDetection();
        
        // After reset, node should query again on next render
        var mockOutput = new MockTerminalOutput();
        var context = new Hex1bRenderContext(mockOutput);
        node.Arrange(new Rect(0, 0, 40, 20));
        node.Render(context);
        
        // Should send DA1 query
        Assert.Contains("\x1b[c", mockOutput.Output);
    }

    [Fact]
    public void Render_WhenWaitingForResponse_ShowsLoadingMessage()
    {
        var node = new SixelNode();
        // Don't set support - node should query
        
        var mockOutput = new MockTerminalOutput();
        var context = new Hex1bRenderContext(mockOutput);
        node.Arrange(new Rect(0, 0, 40, 20));
        
        // First render sends query
        node.Render(context);
        mockOutput.Clear();
        
        // Second render while waiting shows loading
        node.Render(context);
        Assert.Contains("Checking Sixel support", mockOutput.Output);
    }

    [Fact]
    public void HandleTerminalResponse_WithSixelSupport_EnablesSixel()
    {
        var node = new SixelNode();
        
        var mockOutput = new MockTerminalOutput();
        var context = new Hex1bRenderContext(mockOutput);
        node.Arrange(new Rect(0, 0, 40, 20));
        
        // Start query
        node.Render(context);
        
        // Simulate terminal response with Sixel support (;4; indicates graphics)
        SixelNode.HandleDA1Response("\x1b[?62;4;6;22c");
        
        mockOutput.Clear();
        node.Render(context);
        
        // Should now render Sixel (or no image data message)
        Assert.Contains("[No image data]", mockOutput.Output);
    }

    [Fact]
    public void HandleTerminalResponse_WithoutSixelSupport_RendersFallback()
    {
        var node = new SixelNode
        {
            Fallback = new TextBlockNode { Text = "No Sixel" }
        };
        
        var mockOutput = new MockTerminalOutput();
        var context = new Hex1bRenderContext(mockOutput);
        node.Fallback.Arrange(new Rect(0, 0, 40, 1));
        node.Arrange(new Rect(0, 0, 40, 20));
        
        // Start query
        node.Render(context);
        
        // Simulate terminal response without Sixel support
        SixelNode.HandleDA1Response("\x1b[?62;6;22c");
        
        mockOutput.Clear();
        node.Render(context);
        
        Assert.Contains("No Sixel", mockOutput.Output);
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
    public void HandleInput_WhenShowingFallback_DelegatesToFallback()
    {
        var clickedCount = 0;
        var buttonNode = new ButtonNode 
        { 
            Label = "Test",
            OnClick = () => clickedCount++,
            IsFocused = true
        };
        
        var node = new SixelNode { Fallback = buttonNode };
        node.SetSixelSupport(false);
        
        var enterEvent = new KeyInputEvent(ConsoleKey.Enter, '\r', false, false, false);
        var handled = node.HandleInput(enterEvent);
        
        Assert.True(handled);
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
        
        var mockOutput = new MockTerminalOutput();
        var context = new Hex1bRenderContext(mockOutput);
        node.Arrange(new Rect(0, 0, 40, 20));
        node.Render(context);
        
        // Should wrap in Sixel DCS sequence: ESC P q ... ESC \
        Assert.Contains("\x1bPq", mockOutput.Output);
        Assert.Contains("\x1b\\", mockOutput.Output);
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
        
        var mockOutput = new MockTerminalOutput();
        var context = new Hex1bRenderContext(mockOutput);
        node.Arrange(new Rect(0, 0, 40, 20));
        node.Render(context);
        
        // Should output as-is without double-wrapping
        Assert.Equal(1, CountOccurrences(mockOutput.Output, "\x1bPq"));
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

    /// <summary>
    /// Mock terminal output for testing render output.
    /// </summary>
    private class MockTerminalOutput : IHex1bTerminalOutput
    {
        private readonly System.Text.StringBuilder _output = new();
        
        public string Output => _output.ToString();
        public int Width => 80;
        public int Height => 24;

        public void Write(string text) => _output.Append(text);
        public void Clear() => _output.Clear();
        public void SetCursorPosition(int left, int top) { }
        public void EnterAlternateScreen() { }
        public void ExitAlternateScreen() { }
    }
}
