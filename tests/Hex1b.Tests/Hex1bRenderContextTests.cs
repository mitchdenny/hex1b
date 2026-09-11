#pragma warning disable HEX1B_SIXEL // Testing experimental Sixel API

using System.Text;
using Hex1b.Layout;
using Hex1b.Sixel;
using Hex1b.Surfaces;

namespace Hex1b.Tests;

/// <summary>
/// Tests for Hex1bRenderContext functionality.
/// </summary>
[TestClass]
public class Hex1bRenderContextTests
{
    [TestMethod]
    public void WriteSixel_StructuredPixels_ResamplesToRequestedProtocolSpan()
    {
        var context = new CapturingRenderContext(CreateSixelCapabilities(10, 20));
        var pixels = new SixelPixelBuffer(240, 120);
        pixels[0, 0] = Rgba32.FromRgb(255, 0, 0);

        context.WriteSixel(pixels, 32, 9);

        var payload = TestSeq.Single(context.Writes);
        var parsed = SixelParser.ParsePayload(payload);
        Assert.AreEqual(320, parsed.DeclaredExtent.Width);
        Assert.AreEqual(180, parsed.DeclaredExtent.Height);
        Assert.AreEqual(32, context.Capabilities.SixelCellMetrics!.Value.ColumnsFor(parsed.LogicalCanvasExtent.Width));
        Assert.AreEqual(9, context.Capabilities.SixelCellMetrics!.Value.RowsFor(parsed.LogicalCanvasExtent.Height));
    }

    [TestMethod]
    public void WriteSixel_PreEncodedPayloadWithDifferentSpan_Throws()
    {
        var context = new CapturingRenderContext(CreateSixelCapabilities(10, 20));
        var pixels = new SixelPixelBuffer(20, 20);
        pixels[0, 0] = Rgba32.FromRgb(255, 0, 0);
        var payload = SixelEncoder.Encode(pixels);

        var exception = Assert.Throws<ArgumentException>(() => context.WriteSixel(payload, 3, 1));

        Assert.Contains("cannot be resized", exception.Message);
        Assert.IsEmpty(context.Writes);
    }

    [TestMethod]
    public void WriteSixel_FractionalProtocolMetrics_PreservesRequestedCellSpan()
    {
        var context = new CapturingRenderContext(CreateSixelCapabilities(9.6, 19.6));
        var pixels = new SixelPixelBuffer(20, 20);
        pixels[0, 0] = Rgba32.FromRgb(255, 0, 0);

        context.WriteSixel(pixels, 1, 1);

        var parsed = SixelParser.ParsePayload(TestSeq.Single(context.Writes));
        Assert.AreEqual(9, parsed.DeclaredExtent.Width);
        Assert.AreEqual(19, parsed.DeclaredExtent.Height);
        Assert.AreEqual(1, context.Capabilities.SixelCellMetrics!.Value.ColumnsFor(parsed.LogicalCanvasExtent.Width));
        Assert.AreEqual(1, context.Capabilities.SixelCellMetrics!.Value.RowsFor(parsed.LogicalCanvasExtent.Height));
    }

    [TestMethod]
    public void WriteSixel_EightBitFraming_EmitsCanonicalSevenBitBytes()
    {
        var context = new CapturingRenderContext(CreateSixelCapabilities(10, 20));
        var pixels = new SixelPixelBuffer(10, 20);
        pixels[0, 0] = Rgba32.FromRgb(255, 0, 0);
        var sevenBit = SixelEncoder.Encode(pixels);
        var eightBit = $"\x90{sevenBit[2..^2]}\x9c";

        context.WriteSixel(eightBit, 1, 1);

        var emitted = TestSeq.Single(context.Writes);
        var bytes = Encoding.UTF8.GetBytes(emitted);
        Assert.StartsWith("\x1bP", emitted);
        Assert.EndsWith("\x1b\\", emitted);
        Assert.AreEqual(0x1b, bytes[0]);
        Assert.AreEqual((byte)'P', bytes[1]);
        Assert.AreEqual(0x1b, bytes[^2]);
        Assert.AreEqual((byte)'\\', bytes[^1]);
        Assert.IsFalse(bytes.Contains((byte)0xc2));
    }

    #region ClearRegion Tests

    [TestMethod]
    public async Task ClearRegion_WritesSpacesToRegion()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        var context = new Hex1bRenderContext(workload);
        
        // First, write some content
        context.SetCursorPosition(5, 2);
        context.Write("Hello World");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal);
        Assert.IsTrue(terminal.CreateSnapshot().ContainsText("Hello World"));
        
        // Clear the region where the text is
        context.ClearRegion(new Rect(5, 2, 11, 1));
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        // Text should be gone (replaced with spaces)
        Assert.IsFalse(terminal.CreateSnapshot().ContainsText("Hello World"));
    }

    [TestMethod]
    public async Task ClearRegion_MultipleRows()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        var context = new Hex1bRenderContext(workload);
        
        // Write content on multiple rows
        context.SetCursorPosition(0, 0);
        context.Write("Line 0");
        context.SetCursorPosition(0, 1);
        context.Write("Line 1");
        context.SetCursorPosition(0, 2);
        context.Write("Line 2");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        // Clear a region spanning all three rows
        context.ClearRegion(new Rect(0, 0, 10, 3));
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        var snapshot = terminal.CreateSnapshot();
        Assert.IsFalse(snapshot.ContainsText("Line 0"));
        Assert.IsFalse(snapshot.ContainsText("Line 1"));
        Assert.IsFalse(snapshot.ContainsText("Line 2"));
    }

    [TestMethod]
    public async Task ClearRegion_PartialClear()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        var context = new Hex1bRenderContext(workload);
        
        context.SetCursorPosition(0, 0);
        context.Write("ABCDEFGHIJ");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        // Clear only the middle portion
        context.ClearRegion(new Rect(3, 0, 4, 1));
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        var line = terminal.CreateSnapshot().GetLine(0);
        Assert.AreEqual("ABC    HIJ", line.Substring(0, 10));
    }

    [TestMethod]
    public async Task ClearRegion_ClampsToTerminalBounds()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var context = new Hex1bRenderContext(workload);
        
        // Try to clear a region that extends beyond terminal bounds
        // This should not throw and should clear only the valid portion
        context.ClearRegion(new Rect(15, 3, 100, 100));
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        // If we got here without exception, the clamping worked
        Assert.IsTrue(true);
    }

    [TestMethod]
    public async Task ClearRegion_EmptyRect_DoesNothing()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        var context = new Hex1bRenderContext(workload);
        
        context.SetCursorPosition(5, 2);
        context.Write("Should remain");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        // Clear with zero dimensions
        context.ClearRegion(new Rect(5, 2, 0, 0));
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Should remain"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal);
        Assert.IsTrue(terminal.CreateSnapshot().ContainsText("Should remain"));
    }

    [TestMethod]
    public async Task ClearRegion_NegativePosition_ClampsToZero()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        var context = new Hex1bRenderContext(workload);
        
        context.SetCursorPosition(0, 0);
        context.Write("Hello");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        // Clear with negative position (should clamp to 0)
        context.ClearRegion(new Rect(-5, -5, 10, 10));
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        // Content at origin should be cleared
        Assert.IsFalse(terminal.CreateSnapshot().ContainsText("Hello"));
    }

    #endregion

    private static TerminalCapabilities CreateSixelCapabilities(double width, double height)
        => new()
        {
            SixelSupport = SixelPresentationSupport.Native,
            SixelCellMetrics = new SixelCellMetrics(
                width,
                height,
                SixelCellMetricsSource.Direct,
                SixelCellMetricsReliability.Authoritative)
        };

    private sealed class CapturingRenderContext(TerminalCapabilities capabilities)
        : Hex1bRenderContext(theme: null)
    {
        public override TerminalCapabilities Capabilities => capabilities;

        internal List<string> Writes { get; } = [];

        public override void Write(string text) => Writes.Add(text);
    }
}
