#pragma warning disable HEX1B_SIXEL // Testing experimental Sixel API

using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Sixel;
using Hex1b.Surfaces;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Unit tests for SixelNode.
/// Sixel support is controlled via TerminalCapabilities, not static state.
/// </summary>
[TestClass]
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

    private static SixelPixelBuffer CreatePixelBuffer(byte[] rgba, int width, int height)
    {
        var pixels = new Rgba32[width * height];
        for (var i = 0; i < pixels.Length; i++)
        {
            var offset = i * 4;
            pixels[i] = new Rgba32(
                rgba[offset],
                rgba[offset + 1],
                rgba[offset + 2],
                rgba[offset + 3]);
        }

        return new SixelPixelBuffer(width, height, pixels);
    }

    [TestMethod]
    public void Measure_WithRequestedDimensions_ReturnsRequestedSize()
    {
        var node = new SixelNode
        {
            RequestedWidth = 50,
            RequestedHeight = 25
        };
        node.SetPixels(new SixelPixelBuffer(80, 40));
        node.SetTerminalCapabilities(CreateSixelEnabledWorkload().Capabilities);

        var size = node.Measure(Constraints.Unbounded);

        Assert.AreEqual(50, size.Width);
        Assert.AreEqual(25, size.Height);
    }

    [TestMethod]
    public void Measure_WithoutRequestedDimensions_UsesSixelCellMetrics()
    {
        var node = new SixelNode();
        node.SetPixels(new SixelPixelBuffer(81, 41));
        node.SetTerminalCapabilities(new TerminalCapabilities
        {
            SixelSupport = SixelPresentationSupport.Native,
            SixelCellMetrics = new SixelCellMetrics(
                10,
                20,
                SixelCellMetricsSource.Direct,
                SixelCellMetricsReliability.Authoritative)
        });

        var size = node.Measure(Constraints.Unbounded);

        Assert.AreEqual(9, size.Width);
        Assert.AreEqual(3, size.Height);
    }

    [TestMethod]
    public void Measure_WithUnknownSupport_ReturnsFallbackSize()
    {
        var fallbackNode = new TextBlockNode { Text = "Fallback text" };
        var node = new SixelNode
        {
            Fallback = fallbackNode
        };
        node.SetPixels(new SixelPixelBuffer(400, 400));

        var size = node.Measure(Constraints.Unbounded);

        Assert.AreEqual("Fallback text".Length, size.Width);
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public async Task Render_WithSixelSupport_RendersImageData()
    {
        var node = new SixelNode();
        
        using var workload = CreateSixelEnabledWorkload();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var context = new Hex1bRenderContext(workload);
        node.Arrange(new Rect(0, 0, 40, 20));
        node.Render(context);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        // With no image data, should show "[No image data]"
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[No image data]"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal);
        Assert.IsTrue(terminal.CreateSnapshot().ContainsText("[No image data]"));
    }

    [TestMethod]
    public async Task Render_WithoutSixelSupport_RendersFallback()
    {
        var node = new SixelNode
        {
            Fallback = new TextBlockNode { Text = "Fallback content" }
        };
        
        using var workload = CreateSixelDisabledWorkload();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var context = new Hex1bRenderContext(workload);
        node.Fallback.Arrange(new Rect(0, 0, 40, 1));
        node.Arrange(new Rect(0, 0, 40, 20));
        node.Render(context);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Fallback content"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal);
        Assert.IsTrue(terminal.CreateSnapshot().ContainsText("Fallback content"));
    }

    [TestMethod]
    public async Task Render_WithoutSixelSupport_NoFallback_ShowsPlaceholder()
    {
        var node = new SixelNode();
        
        using var workload = CreateSixelDisabledWorkload();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var context = new Hex1bRenderContext(workload);
        node.Arrange(new Rect(0, 0, 40, 20));
        node.Render(context);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[Sixel not supported]"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal);
        Assert.IsTrue(terminal.CreateSnapshot().ContainsText("[Sixel not supported]"));
    }

    [TestMethod]
    public async Task GetFocusableNodes_WithFallback_ReturnsFallbackFocusables()
    {
        var buttonNode = new ButtonNode { Label = "Test" };
        var fallback = new VStackNode();
        fallback.Children = [buttonNode];
        
        var node = new SixelNode { Fallback = fallback };
        
        var focusables = node.GetFocusableNodes().ToList();
        
        Assert.Contains(buttonNode, focusables);
    }

    [TestMethod]
    public void GetFocusableNodes_WithNativeSupport_ExcludesFallbackFocusables()
    {
        var buttonNode = new ButtonNode { Label = "Test" };
        var node = new SixelNode { Fallback = buttonNode };
        node.SetTerminalCapabilities(new TerminalCapabilities
        {
            SixelSupport = SixelPresentationSupport.Native
        });

        Assert.IsEmpty(node.GetFocusableNodes());
    }

    [TestMethod]
    public void IsSixelSupported_TypedSupportTakesPrecedenceOverLegacyFlag()
    {
        Assert.IsFalse(SixelNode.IsSixelSupported(new TerminalCapabilities
        {
            SupportsSixel = true,
            SixelSupport = SixelPresentationSupport.None
        }));
        Assert.IsTrue(SixelNode.IsSixelSupported(new TerminalCapabilities
        {
            SupportsSixel = false,
            SixelSupport = SixelPresentationSupport.Headless
        }));
        Assert.IsTrue(SixelNode.IsSixelSupported(new TerminalCapabilities
        {
            SupportsSixel = true,
            SixelSupport = SixelPresentationSupport.Unknown
        }));
    }

    [TestMethod]
    public void SixelWidget_PreEncodedData_ValidatesAndFramesOnce()
    {
        var body = "#0;2;100;0;0#0~~~~~~";
        var fromBody = new SixelWidget(body, new TextBlockWidget("fallback"));
        var framed = new SixelWidget($"\x1bPq{body}\x1b\\", new TextBlockWidget("fallback"));

        Assert.AreEqual($"\x1bPq{body}\x1b\\", fromBody.ImageData);
        Assert.AreEqual(fromBody.ImageData, framed.ImageData);
        Assert.Throws<ArgumentException>(() =>
            new SixelWidget("\x1bPqmissing-terminator", new TextBlockWidget("fallback")));
    }

    [TestMethod]
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
        
        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual(1, clickedCount);
    }

    [TestMethod]
    public async Task Render_WithSixelData_OutputsSixelSequence()
    {
        var node = new SixelNode
        {
            ImageData = "#0;2;100;0;0#0~~~~~~"
        };
        
        using var workload = CreateSixelEnabledWorkload();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var context = new Hex1bRenderContext(workload);
        node.Arrange(new Rect(0, 0, 1, 1));
        node.Render(context);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        // Sixel data should be tracked in the terminal
        Assert.IsTrue(terminal.ContainsSixelData());
        
        // The origin cell should have the Sixel data
        var sixelData = terminal.GetSixelDataAt(0, 0);
        Assert.IsNotNull(sixelData);
        Assert.Contains("#0;2;100;0;0#0~~~~~~", sixelData.Payload);
    }

    [TestMethod]
    public async Task Render_WithPreformattedSixelData_OutputsAsIs()
    {
        // Data already has DCS header
        var sixelPayload = "\x1bPq#0;2;100;0;0#0~~~~~~\x1b\\";
        var node = new SixelNode
        {
            ImageData = sixelPayload
        };
        
        using var workload = CreateSixelEnabledWorkload();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var context = new Hex1bRenderContext(workload);
        node.Arrange(new Rect(0, 0, 1, 1));
        node.Render(context);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        // Flush pending output before checking
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        // Should be tracked as a single Sixel object
        Assert.AreEqual(1, terminal.TrackedSixelCount);
        
        // The tracked data should contain the original payload
        var trackedSixel = terminal.GetSixelDataAt(0, 0);
        Assert.IsNotNull(trackedSixel);
        Assert.Contains("#0;2;100;0;0#0~~~~~~", trackedSixel.Payload);
    }

    [TestMethod]
    public async Task Render_WithSmpteColorBars_ProducesSvgWithEmbeddedImage()
    {
        // SMPTE color bars: White, Yellow, Cyan, Green, Magenta, Red, Blue
        // This is the classic TV test pattern - easy to visually verify
        const int width = 70;  // 7 bars * 10 pixels each
        const int height = 30; // 30 pixels tall
        
        var pixels = TestPatternGenerator.GenerateSmpteColorBars(width, height);
        var node = new SixelNode
        {
            RequestedWidth = 10,
            RequestedHeight = 5
        };
        node.SetPixels(CreatePixelBuffer(pixels, width, height));
        
        using var workload = CreateSixelEnabledWorkload();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var context = new Hex1bRenderContext(workload);
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        // Create snapshot and generate SVG
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
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
        Assert.IsTrue(terminal.ContainsSixelData());
    }

    [TestMethod]
    public async Task Render_WithColorGrid_ProducesSvgWithEmbeddedImage()
    {
        // 3x3 grid of colors - easy to verify each color block
        // Layout:
        //   Red    Green  Blue
        //   Yellow Cyan   Magenta
        //   Black  Gray   White
        const int width = 60;  // 3 columns * 20 pixels
        const int height = 60; // 3 rows * 20 pixels
        
        var pixels = TestPatternGenerator.GenerateColorGrid(width, height);
        var node = new SixelNode
        {
            RequestedWidth = 10,
            RequestedHeight = 5
        };
        node.SetPixels(CreatePixelBuffer(pixels, width, height));
        
        using var workload = CreateSixelEnabledWorkload();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var context = new Hex1bRenderContext(workload);
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        // Create snapshot and generate SVG
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
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
        Assert.IsTrue(terminal.ContainsSixelData());
    }

    [TestMethod]
    public async Task Render_WithGrayscaleGradient_ProducesSvgWithEmbeddedImage()
    {
        // Horizontal grayscale gradient - black on left to white on right
        // Easy to verify smooth transitions
        const int width = 80;
        const int height = 24;
        
        var pixels = TestPatternGenerator.GenerateGrayscaleGradient(width, height);
        var node = new SixelNode
        {
            RequestedWidth = 10,
            RequestedHeight = 3
        };
        node.SetPixels(CreatePixelBuffer(pixels, width, height));
        
        using var workload = CreateSixelEnabledWorkload();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var context = new Hex1bRenderContext(workload);
        node.Arrange(new Rect(0, 0, 10, 3));
        node.Render(context);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        // Create snapshot and generate SVG
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
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

    [TestMethod]
    public async Task Render_WithRgbGradients_ProducesSvgWithEmbeddedImage()
    {
        // Three horizontal bands: R gradient, G gradient, B gradient
        // Easy to verify each color channel
        const int width = 80;
        const int height = 36; // 3 bands of 12 pixels each
        
        var pixels = TestPatternGenerator.GenerateRgbGradients(width, height);
        var node = new SixelNode
        {
            RequestedWidth = 10,
            RequestedHeight = 4
        };
        node.SetPixels(CreatePixelBuffer(pixels, width, height));
        
        using var workload = CreateSixelEnabledWorkload();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var context = new Hex1bRenderContext(workload);
        node.Arrange(new Rect(0, 0, 10, 4));
        node.Render(context);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        // Create snapshot and generate SVG
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
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
        Assert.IsTrue(terminal.ContainsSixelData());
    }

    [TestMethod]
    public async Task Render_WithCheckerboard_ProducesSvgWithEmbeddedImage()
    {
        // Checkerboard pattern - easy to verify alignment and scaling
        const int width = 64;
        const int height = 48;
        const int squareSize = 8;
        
        var pixels = TestPatternGenerator.GenerateCheckerboard(width, height, squareSize);
        var node = new SixelNode
        {
            RequestedWidth = 10,
            RequestedHeight = 5
        };
        node.SetPixels(CreatePixelBuffer(pixels, width, height));
        
        using var workload = CreateSixelEnabledWorkload();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var context = new Hex1bRenderContext(workload);
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        // Create snapshot and generate SVG
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
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

    [TestMethod]
    public async Task Render_WithRegistrationMarks_ProducesSvgWithEmbeddedImage()
    {
        // Registration marks with center crosshair and colored corners
        // Useful for verifying alignment and position accuracy
        const int width = 80;
        const int height = 60;
        
        var pixels = TestPatternGenerator.GenerateRegistrationMarks(width, height);
        var node = new SixelNode
        {
            RequestedWidth = 12,
            RequestedHeight = 6
        };
        node.SetPixels(CreatePixelBuffer(pixels, width, height));
        
        using var workload = CreateSixelEnabledWorkload();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var context = new Hex1bRenderContext(workload);
        node.Arrange(new Rect(0, 0, 12, 6));
        node.Render(context);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        // Create snapshot and generate SVG
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
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
        Assert.IsTrue(terminal.ContainsSixelData());
    }
}
