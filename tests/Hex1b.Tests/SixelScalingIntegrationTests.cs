#pragma warning disable HEX1B_SIXEL // Testing experimental Sixel API

using System.Text;
using Hex1b.Input;
using Hex1b.Terminal;
using Hex1b.Terminal.Testing;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Integration tests for Sixel rendering at various cell dimensions.
/// Tests the full Hex1bApp pipeline with different pixel scales.
/// Sixel support is configured through TerminalCapabilities, not static state.
/// </summary>
public class SixelScalingIntegrationTests
{
    
    /// <summary>
    /// Creates terminal capabilities with sixel support and custom cell dimensions.
    /// </summary>
    private static TerminalCapabilities CreateSixelCapabilities(int cellPixelWidth, int cellPixelHeight)
        => new()
        {
            SupportsMouse = true,
            SupportsTrueColor = true,
            Supports256Colors = true,
            SupportsAlternateScreen = true,
            SupportsBracketedPaste = true,
            SupportsSixel = true,
            CellPixelWidth = cellPixelWidth,
            CellPixelHeight = cellPixelHeight
        };

    /// <summary>
    /// Cell dimension configurations to test.
    /// </summary>
    public static TheoryData<int, int, string> CellDimensions => new()
    {
        { 8, 16, "small" },     // Small font
        { 10, 20, "default" },  // Default/Medium
        { 12, 24, "large" }     // Large font
    };

    [Theory]
    [MemberData(nameof(CellDimensions))]
    public async Task SmpteColorBars_RendersCorrectlyAtScale(int cellWidth, int cellHeight, string scaleName)
    {
        // Generate SMPTE color bar pattern (70x30 pixels = 7 bars)
        const int imageWidth = 70;
        const int imageHeight = 30;
        var pixels = TestPatternGenerator.GenerateSmpteColorBars(imageWidth, imageHeight);
        var sixelPayload = TestPatternGenerator.ConvertToSixel(pixels, imageWidth, imageHeight);
        
        // Calculate expected cell dimensions for the image
        var expectedCellsWide = (imageWidth + cellWidth - 1) / cellWidth;
        var expectedCellsTall = (imageHeight + cellHeight - 1) / cellHeight;
        
        // Terminal size: ruler column (3) + image width + padding
        var terminalWidth = 3 + expectedCellsWide + 2;
        var terminalHeight = 2 + expectedCellsTall + 2; // header + ruler row + image + padding
        
        // Create workload adapter with sixel-enabled capabilities
        var capabilities = CreateSixelCapabilities(cellWidth, cellHeight);
        using var workload = new Hex1bAppWorkloadAdapter(capabilities);
        using var terminal = new Hex1bTerminal(workload, terminalWidth, terminalHeight);
        
        // Build widget tree using Hex1bApp infrastructure
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    // Header with scale info
                    new TextBlockWidget($"{cellWidth}x{cellHeight}px/cell ({scaleName})"),
                    // Column ruler row
                    new HStackWidget([
                        new TextBlockWidget("   "),  // Spacer for row ruler column
                        new TextBlockWidget(BuildColumnRuler(expectedCellsWide))
                    ]),
                    // Image row with row ruler
                    new HStackWidget([
                        // Row numbers
                        new VStackWidget(
                            Enumerable.Range(0, expectedCellsTall)
                                .Select(row => new TextBlockWidget($"{row,2}|"))
                                .ToArray<Hex1bWidget>()
                        ),
                        // The sixel image with fallback
                        new SixelWidget(
                            sixelPayload,
                            new TextBlockWidget($"[{expectedCellsWide}x{expectedCellsTall}]"),
                            expectedCellsWide,
                            expectedCellsTall)
                    ])
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        // Run app briefly to render
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.Terminal.ContainsSixelData(), TimeSpan.FromSeconds(2))
            .Capture("rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // Move cursor to bottom-right so it doesn't interfere with sixel image
        workload.SetCursorPosition(terminalWidth - 1, terminalHeight - 1);
        
        // Create snapshot and verify
        var snapshot = terminal.CreateSnapshot();
        
        // Verify cell dimensions are correct
        Assert.Equal(cellWidth, snapshot.CellPixelWidth);
        Assert.Equal(cellHeight, snapshot.CellPixelHeight);
        
        // Verify sixel is tracked
        Assert.True(terminal.ContainsSixelData());
        
        // Capture all formats using infrastructure
        TestCaptureHelper.Capture(snapshot, $"sixel-smpte-{scaleName}-{cellWidth}x{cellHeight}");
        
        // Also generate reference image
        var image = new SixelImage(imageWidth, imageHeight, pixels);
        var refSvg = GenerateReferenceSvg(image, $"Reference: {imageWidth}x{imageHeight} pixels");
        TestCaptureHelper.AttachSvg($"sixel-smpte-{scaleName}-reference.svg", refSvg);
    }

    [Theory]
    [MemberData(nameof(CellDimensions))]
    public async Task Checkerboard_RendersCorrectlyAtScale(int cellWidth, int cellHeight, string scaleName)
    {
        // Generate checkerboard pattern (64x48 pixels with 8x8 squares)
        const int imageWidth = 64;
        const int imageHeight = 48;
        const int squareSize = 8;
        var pixels = TestPatternGenerator.GenerateCheckerboard(imageWidth, imageHeight, squareSize);
        var sixelPayload = TestPatternGenerator.ConvertToSixel(pixels, imageWidth, imageHeight);
        
        // Calculate expected cell dimensions
        var expectedCellsWide = (imageWidth + cellWidth - 1) / cellWidth;
        var expectedCellsTall = (imageHeight + cellHeight - 1) / cellHeight;
        
        // Terminal size
        var terminalWidth = 3 + expectedCellsWide + 2;
        var terminalHeight = 2 + expectedCellsTall + 2;
        
        // Create workload adapter with sixel-enabled capabilities
        var capabilities = CreateSixelCapabilities(cellWidth, cellHeight);
        using var workload = new Hex1bAppWorkloadAdapter(capabilities);
        using var terminal = new Hex1bTerminal(workload, terminalWidth, terminalHeight);
        
        // Build widget tree with rulers
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new TextBlockWidget($"{cellWidth}x{cellHeight}px/cell ({scaleName})"),
                    new HStackWidget([
                        new TextBlockWidget("   "),
                        new TextBlockWidget(BuildColumnRuler(expectedCellsWide))
                    ]),
                    new HStackWidget([
                        new VStackWidget(
                            Enumerable.Range(0, expectedCellsTall)
                                .Select(row => new TextBlockWidget($"{row,2}|"))
                                .ToArray<Hex1bWidget>()
                        ),
                        new SixelWidget(
                            sixelPayload,
                            new TextBlockWidget($"[{expectedCellsWide}x{expectedCellsTall}]"),
                            expectedCellsWide,
                            expectedCellsTall)
                    ])
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.Terminal.ContainsSixelData(), TimeSpan.FromSeconds(2))
            .Capture("rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // Move cursor to bottom-right so it doesn't interfere with sixel image
        workload.SetCursorPosition(terminalWidth - 1, terminalHeight - 1);
        
        var snapshot = terminal.CreateSnapshot();
        
        Assert.Equal(cellWidth, snapshot.CellPixelWidth);
        Assert.Equal(cellHeight, snapshot.CellPixelHeight);
        Assert.True(terminal.ContainsSixelData());
        
        TestCaptureHelper.Capture(snapshot, $"sixel-checkerboard-{scaleName}-{cellWidth}x{cellHeight}");
        
        var image = new SixelImage(imageWidth, imageHeight, pixels);
        var refSvg = GenerateReferenceSvg(image, $"Reference: {imageWidth}x{imageHeight}px, {squareSize}px squares");
        TestCaptureHelper.AttachSvg($"sixel-checkerboard-{scaleName}-reference.svg", refSvg);
    }
    
    /// <summary>
    /// Builds a column ruler string showing column positions.
    /// Shows digits 0-9 repeating for each column.
    /// </summary>
    private static string BuildColumnRuler(int width)
    {
        var sb = new StringBuilder(width);
        for (int col = 0; col < width; col++)
        {
            sb.Append((col % 10).ToString());
        }
        return sb.ToString();
    }

    /// <summary>
    /// Generates a reference SVG with the original image at 1:1 scale.
    /// </summary>
    private static string GenerateReferenceSvg(SixelImage image, string title)
    {
        var dataUri = BmpEncoder.ToDataUri(image);
        var scale = 3; // 3x for visibility
        var width = image.Width * scale;
        var height = image.Height * scale + 30;
        
        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}">
              <rect width="{width}" height="{height}" fill="#1e1e1e"/>
              <text x="5" y="15" style="font-family: 'Segoe UI', Arial; font-size: 12px; font-weight: bold; fill: #d4d4d4;">{title}</text>
              <image x="0" y="30" width="{image.Width * scale}" height="{image.Height * scale}" href="{dataUri}" style="image-rendering: pixelated;"/>
            </svg>
            """;
    }
}
