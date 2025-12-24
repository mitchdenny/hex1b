#pragma warning disable HEX1B_SIXEL // Testing experimental Sixel API

using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Terminal;
using Hex1b.Terminal.Testing;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Unit tests for SixelNode.
/// Sixel support is controlled via TerminalCapabilities, not static state.
/// </summary>
public class SixelNodeTests
{
    /// <summary>
    /// Creates a workload adapter with sixel support enabled.
    /// </summary>
    private static Hex1bAppWorkloadAdapter CreateSixelEnabledWorkload() 
        => new(new TerminalCapabilities 
        { 
            SupportsSixel = true,
            SupportsMouse = true,
            SupportsTrueColor = true,
            Supports256Colors = true
        });
    
    /// <summary>
    /// Creates a workload adapter with sixel support disabled.
    /// </summary>
    private static Hex1bAppWorkloadAdapter CreateSixelDisabledWorkload() 
        => new(new TerminalCapabilities 
        { 
            SupportsSixel = false,
            SupportsMouse = true,
            SupportsTrueColor = true,
            Supports256Colors = true
        });

    [Fact]
    public void Measure_WithRequestedDimensions_ReturnsRequestedSize()
    {
        var node = new SixelNode
        {
            RequestedWidth = 50,
            RequestedHeight = 25
        };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(50, size.Width);
        Assert.Equal(25, size.Height);
    }

    [Fact]
    public void Measure_WithoutRequestedDimensions_ReturnsDefaultSize()
    {
        var node = new SixelNode();

        var size = node.Measure(Constraints.Unbounded);

        // Default size is 40x20
        Assert.Equal(40, size.Width);
        Assert.Equal(20, size.Height);
    }

    [Fact]
    public void Measure_WithFallback_ReturnsFallbackSize()
    {
        var fallbackNode = new TextBlockNode { Text = "Fallback text" };
        var node = new SixelNode
        {
            Fallback = fallbackNode
        };

        var size = node.Measure(Constraints.Unbounded);

        // Should return the larger of sixel or fallback size
        Assert.True(size.Width > 0);
    }

    [Fact]
    public void Render_WithSixelSupport_RendersImageData()
    {
        var node = new SixelNode();
        
        using var workload = CreateSixelEnabledWorkload();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var context = new Hex1bRenderContext(workload);
        node.Arrange(new Rect(0, 0, 40, 20));
        node.Render(context);
        
        // With no image data, should show "[No image data]"
        Assert.True(terminal.CreateSnapshot().ContainsText("[No image data]"));
    }

    [Fact]
    public void Render_WithoutSixelSupport_RendersFallback()
    {
        var node = new SixelNode
        {
            Fallback = new TextBlockNode { Text = "Fallback content" }
        };
        
        using var workload = CreateSixelDisabledWorkload();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var context = new Hex1bRenderContext(workload);
        node.Fallback.Arrange(new Rect(0, 0, 40, 1));
        node.Arrange(new Rect(0, 0, 40, 20));
        node.Render(context);
        
        Assert.True(terminal.CreateSnapshot().ContainsText("Fallback content"));
    }

    [Fact]
    public void Render_WithoutSixelSupport_NoFallback_ShowsPlaceholder()
    {
        var node = new SixelNode();
        
        using var workload = CreateSixelDisabledWorkload();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var context = new Hex1bRenderContext(workload);
        node.Arrange(new Rect(0, 0, 40, 20));
        node.Render(context);
        
        Assert.True(terminal.CreateSnapshot().ContainsText("[Sixel not supported]"));
    }

    [Fact]
    public void GetFocusableNodes_WithFallback_ReturnsFallbackFocusables()
    {
        var buttonNode = new ButtonNode { Label = "Test" };
        var fallback = new VStackNode();
        fallback.Children = [buttonNode];
        
        var node = new SixelNode { Fallback = fallback };
        
        var focusables = node.GetFocusableNodes().ToList();
        
        // Fallback focusables are always returned since we don't know at this point
        // whether sixel will be rendered or not
        Assert.Contains(buttonNode, focusables);
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
        
        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        focusRing.EnsureFocus();
        var routerState = new InputRouterState();
        
        // Use InputRouter to route input to the focused child in the fallback
        var enterEvent = new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None);
        var result = await InputRouter.RouteInputAsync(node, enterEvent, focusRing, routerState, null, TestContext.Current.CancellationToken);
        
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
        
        using var workload = CreateSixelEnabledWorkload();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var context = new Hex1bRenderContext(workload);
        node.Arrange(new Rect(0, 0, 40, 20));
        node.Render(context);
        
        // Sixel data should be tracked in the terminal
        Assert.True(terminal.ContainsSixelData());
        
        // The origin cell should have the Sixel data
        var sixelData = terminal.GetSixelDataAt(0, 0);
        Assert.NotNull(sixelData);
        Assert.Contains("#0;2;100;0;0#0~~~~~~", sixelData.Payload);
    }

    [Fact]
    public void Render_WithPreformattedSixelData_OutputsAsIs()
    {
        // Data already has DCS header
        var sixelPayload = "\x1bPq#0;2;100;0;0#0~~~~~~\x1b\\";
        var node = new SixelNode
        {
            ImageData = sixelPayload
        };
        
        using var workload = CreateSixelEnabledWorkload();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var context = new Hex1bRenderContext(workload);
        node.Arrange(new Rect(0, 0, 40, 20));
        node.Render(context);
        
        // Flush pending output before checking
        terminal.FlushOutput();
        
        // Should be tracked as a single Sixel object
        Assert.Equal(1, terminal.TrackedSixelCount);
        
        // The tracked data should contain the original payload
        var trackedSixel = terminal.GetSixelDataAt(0, 0);
        Assert.NotNull(trackedSixel);
        Assert.Contains("#0;2;100;0;0#0~~~~~~", trackedSixel.Payload);
    }

    [Fact]
    public void Render_WithSmpteColorBars_ProducesSvgWithEmbeddedImage()
    {
        // SMPTE color bars: White, Yellow, Cyan, Green, Magenta, Red, Blue
        // This is the classic TV test pattern - easy to visually verify
        const int width = 70;  // 7 bars * 10 pixels each
        const int height = 30; // 30 pixels tall
        
        var pixels = TestPatternGenerator.GenerateSmpteColorBars(width, height);
        var sixelPayload = TestPatternGenerator.ConvertToSixel(pixels, width, height);
        
        var node = new SixelNode
        {
            ImageData = sixelPayload,
            RequestedWidth = 10,
            RequestedHeight = 5
        };
        
        using var workload = CreateSixelEnabledWorkload();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var context = new Hex1bRenderContext(workload);
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);
        terminal.FlushOutput();
        
        // Create snapshot and generate SVG
        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();
        
        // Attach SVG for visual inspection
        TestCaptureHelper.AttachSvg("sixel-smpte-colorbars.svg", svg);
        
        // Also attach the reference BMP directly for comparison
        var image = new SixelImage(width, height, pixels);
        var bmpDataUri = BmpEncoder.ToDataUri(image);
        var referenceSvg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{width * 3}" height="{height * 3}">
              <image x="0" y="0" width="{width * 3}" height="{height * 3}" href="{bmpDataUri}" style="image-rendering: pixelated;"/>
            </svg>
            """;
        TestCaptureHelper.AttachSvg("sixel-smpte-reference.svg", referenceSvg);
        
        // Verify SVG contains an embedded image
        Assert.Contains("<image", svg);
        Assert.Contains("data:image/bmp;base64,", svg);
        Assert.True(terminal.ContainsSixelData());
    }

    [Fact]
    public void Render_WithColorGrid_ProducesSvgWithEmbeddedImage()
    {
        // 3x3 grid of colors - easy to verify each color block
        // Layout:
        //   Red    Green  Blue
        //   Yellow Cyan   Magenta
        //   Black  Gray   White
        const int width = 60;  // 3 columns * 20 pixels
        const int height = 60; // 3 rows * 20 pixels
        
        var pixels = TestPatternGenerator.GenerateColorGrid(width, height);
        var sixelPayload = TestPatternGenerator.ConvertToSixel(pixels, width, height);
        
        var node = new SixelNode
        {
            ImageData = sixelPayload,
            RequestedWidth = 10,
            RequestedHeight = 5
        };
        
        using var workload = CreateSixelEnabledWorkload();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var context = new Hex1bRenderContext(workload);
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);
        terminal.FlushOutput();
        
        // Create snapshot and generate SVG
        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();
        
        // Attach SVG for visual inspection
        TestCaptureHelper.AttachSvg("sixel-color-grid.svg", svg);
        
        // Also attach reference
        var image = new SixelImage(width, height, pixels);
        var bmpDataUri = BmpEncoder.ToDataUri(image);
        var referenceSvg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{width * 3}" height="{height * 3}">
              <image x="0" y="0" width="{width * 3}" height="{height * 3}" href="{bmpDataUri}" style="image-rendering: pixelated;"/>
            </svg>
            """;
        TestCaptureHelper.AttachSvg("sixel-color-grid-reference.svg", referenceSvg);
        
        Assert.Contains("<image", svg);
        Assert.True(terminal.ContainsSixelData());
    }

    [Fact]
    public void Render_WithGrayscaleGradient_ProducesSvgWithEmbeddedImage()
    {
        // Horizontal grayscale gradient - black on left to white on right
        // Easy to verify smooth transitions
        const int width = 80;
        const int height = 24;
        
        var pixels = TestPatternGenerator.GenerateGrayscaleGradient(width, height);
        var sixelPayload = TestPatternGenerator.ConvertToSixel(pixels, width, height);
        
        var node = new SixelNode
        {
            ImageData = sixelPayload,
            RequestedWidth = 10,
            RequestedHeight = 3
        };
        
        using var workload = CreateSixelEnabledWorkload();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var context = new Hex1bRenderContext(workload);
        node.Arrange(new Rect(0, 0, 10, 3));
        node.Render(context);
        terminal.FlushOutput();
        
        // Create snapshot and generate SVG
        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();
        
        // Attach SVG for visual inspection
        TestCaptureHelper.AttachSvg("sixel-grayscale-gradient.svg", svg);
        
        // Also attach reference
        var image = new SixelImage(width, height, pixels);
        var bmpDataUri = BmpEncoder.ToDataUri(image);
        var referenceSvg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{width * 3}" height="{height * 3}">
              <image x="0" y="0" width="{width * 3}" height="{height * 3}" href="{bmpDataUri}" style="image-rendering: pixelated;"/>
            </svg>
            """;
        TestCaptureHelper.AttachSvg("sixel-grayscale-gradient-reference.svg", referenceSvg);
        
        Assert.Contains("<image", svg);
        Assert.Contains("data:image/bmp;base64,", svg);
    }

    [Fact]
    public void Render_WithRgbGradients_ProducesSvgWithEmbeddedImage()
    {
        // Three horizontal bands: R gradient, G gradient, B gradient
        // Easy to verify each color channel
        const int width = 80;
        const int height = 36; // 3 bands of 12 pixels each
        
        var pixels = TestPatternGenerator.GenerateRgbGradients(width, height);
        var sixelPayload = TestPatternGenerator.ConvertToSixel(pixels, width, height);
        
        var node = new SixelNode
        {
            ImageData = sixelPayload,
            RequestedWidth = 10,
            RequestedHeight = 4
        };
        
        using var workload = CreateSixelEnabledWorkload();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var context = new Hex1bRenderContext(workload);
        node.Arrange(new Rect(0, 0, 10, 4));
        node.Render(context);
        terminal.FlushOutput();
        
        // Create snapshot and generate SVG
        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();
        
        // Attach SVG for visual inspection
        TestCaptureHelper.AttachSvg("sixel-rgb-gradients.svg", svg);
        
        // Also attach reference
        var image = new SixelImage(width, height, pixels);
        var bmpDataUri = BmpEncoder.ToDataUri(image);
        var referenceSvg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{width * 3}" height="{height * 3}">
              <image x="0" y="0" width="{width * 3}" height="{height * 3}" href="{bmpDataUri}" style="image-rendering: pixelated;"/>
            </svg>
            """;
        TestCaptureHelper.AttachSvg("sixel-rgb-gradients-reference.svg", referenceSvg);
        
        Assert.Contains("<image", svg);
        Assert.True(terminal.ContainsSixelData());
    }

    [Fact]
    public void Render_WithCheckerboard_ProducesSvgWithEmbeddedImage()
    {
        // Checkerboard pattern - easy to verify alignment and scaling
        const int width = 64;
        const int height = 48;
        const int squareSize = 8;
        
        var pixels = TestPatternGenerator.GenerateCheckerboard(width, height, squareSize);
        var sixelPayload = TestPatternGenerator.ConvertToSixel(pixels, width, height);
        
        var node = new SixelNode
        {
            ImageData = sixelPayload,
            RequestedWidth = 10,
            RequestedHeight = 5
        };
        
        using var workload = CreateSixelEnabledWorkload();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var context = new Hex1bRenderContext(workload);
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);
        terminal.FlushOutput();
        
        // Create snapshot and generate SVG
        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();
        
        // Attach SVG for visual inspection
        TestCaptureHelper.AttachSvg("sixel-checkerboard.svg", svg);
        
        // Also attach reference
        var image = new SixelImage(width, height, pixels);
        var bmpDataUri = BmpEncoder.ToDataUri(image);
        var referenceSvg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{width * 3}" height="{height * 3}">
              <image x="0" y="0" width="{width * 3}" height="{height * 3}" href="{bmpDataUri}" style="image-rendering: pixelated;"/>
            </svg>
            """;
        TestCaptureHelper.AttachSvg("sixel-checkerboard-reference.svg", referenceSvg);
        
        Assert.Contains("<image", svg);
        Assert.Contains("data:image/bmp;base64,", svg);
    }

    [Fact]
    public void Render_WithRegistrationMarks_ProducesSvgWithEmbeddedImage()
    {
        // Registration marks with center crosshair and colored corners
        // Useful for verifying alignment and position accuracy
        const int width = 80;
        const int height = 60;
        
        var pixels = TestPatternGenerator.GenerateRegistrationMarks(width, height);
        var sixelPayload = TestPatternGenerator.ConvertToSixel(pixels, width, height);
        
        var node = new SixelNode
        {
            ImageData = sixelPayload,
            RequestedWidth = 12,
            RequestedHeight = 6
        };
        
        using var workload = CreateSixelEnabledWorkload();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var context = new Hex1bRenderContext(workload);
        node.Arrange(new Rect(0, 0, 12, 6));
        node.Render(context);
        terminal.FlushOutput();
        
        // Create snapshot and generate SVG
        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();
        
        // Attach SVG for visual inspection
        TestCaptureHelper.AttachSvg("sixel-registration-marks.svg", svg);
        
        // Also attach reference
        var image = new SixelImage(width, height, pixels);
        var bmpDataUri = BmpEncoder.ToDataUri(image);
        var referenceSvg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{width * 3}" height="{height * 3}">
              <image x="0" y="0" width="{width * 3}" height="{height * 3}" href="{bmpDataUri}" style="image-rendering: pixelated;"/>
            </svg>
            """;
        TestCaptureHelper.AttachSvg("sixel-registration-marks-reference.svg", referenceSvg);
        
        Assert.Contains("<image", svg);
        Assert.True(terminal.ContainsSixelData());
    }
}
