using Hex1b.Automation;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests that ANSI color indices are preserved through the terminal color pipeline.
/// Standard ANSI colors (SGR 30-37), bright colors (SGR 90-97), and 256-color indices
/// (SGR 38;5;N) must be stored with their original encoding and re-emitted faithfully
/// so that the user's terminal palette is respected.
/// </summary>
public class AnsiColorPreservationTests
{
    #region Hex1bColor Kind Preservation

    [Fact]
    public void FromStandard_PreservesKindAndIndex()
    {
        var color = Hex1bColor.FromStandard(4, 0, 0, 128);

        Assert.Equal(Hex1bColorKind.Standard, color.Kind);
        Assert.Equal(4, color.AnsiIndex);
        Assert.Equal(0, color.R);
        Assert.Equal(0, color.G);
        Assert.Equal(128, color.B);
    }

    [Fact]
    public void FromBright_PreservesKindAndIndex()
    {
        var color = Hex1bColor.FromBright(1, 255, 0, 0);

        Assert.Equal(Hex1bColorKind.Bright, color.Kind);
        Assert.Equal(1, color.AnsiIndex);
    }

    [Fact]
    public void FromIndexed_PreservesKindAndIndex()
    {
        var color = Hex1bColor.FromIndexed(196, 255, 0, 0);

        Assert.Equal(Hex1bColorKind.Indexed, color.Kind);
        Assert.Equal(196, color.AnsiIndex);
    }

    [Fact]
    public void FromRgb_HasRgbKind()
    {
        var color = Hex1bColor.FromRgb(100, 200, 50);

        Assert.Equal(Hex1bColorKind.Rgb, color.Kind);
        Assert.Equal(0, color.AnsiIndex);
    }

    #endregion

    #region Hex1bColor ANSI Serialization

    [Theory]
    [InlineData(0, "\x1b[30m")]  // Black
    [InlineData(1, "\x1b[31m")]  // Red
    [InlineData(2, "\x1b[32m")]  // Green
    [InlineData(3, "\x1b[33m")]  // Yellow
    [InlineData(4, "\x1b[34m")]  // Blue
    [InlineData(5, "\x1b[35m")]  // Magenta
    [InlineData(6, "\x1b[36m")]  // Cyan
    [InlineData(7, "\x1b[37m")]  // White
    public void ToForegroundAnsi_StandardColor_EmitsOriginalCode(int index, string expected)
    {
        var color = Hex1bColor.FromStandard((byte)index, 0, 0, 0);
        Assert.Equal(expected, color.ToForegroundAnsi());
    }

    [Theory]
    [InlineData(0, "\x1b[40m")]
    [InlineData(4, "\x1b[44m")]  // Blue background
    public void ToBackgroundAnsi_StandardColor_EmitsOriginalCode(int index, string expected)
    {
        var color = Hex1bColor.FromStandard((byte)index, 0, 0, 0);
        Assert.Equal(expected, color.ToBackgroundAnsi());
    }

    [Theory]
    [InlineData(0, "\x1b[90m")]   // Bright black
    [InlineData(1, "\x1b[91m")]   // Bright red
    [InlineData(4, "\x1b[94m")]   // Bright blue
    [InlineData(7, "\x1b[97m")]   // Bright white
    public void ToForegroundAnsi_BrightColor_EmitsOriginalCode(int index, string expected)
    {
        var color = Hex1bColor.FromBright((byte)index, 0, 0, 0);
        Assert.Equal(expected, color.ToForegroundAnsi());
    }

    [Theory]
    [InlineData(0, "\x1b[100m")]
    [InlineData(4, "\x1b[104m")]  // Bright blue background
    public void ToBackgroundAnsi_BrightColor_EmitsOriginalCode(int index, string expected)
    {
        var color = Hex1bColor.FromBright((byte)index, 0, 0, 0);
        Assert.Equal(expected, color.ToBackgroundAnsi());
    }

    [Fact]
    public void ToForegroundAnsi_IndexedColor_Emits256Code()
    {
        var color = Hex1bColor.FromIndexed(196, 255, 0, 0);
        Assert.Equal("\x1b[38;5;196m", color.ToForegroundAnsi());
    }

    [Fact]
    public void ToBackgroundAnsi_IndexedColor_Emits256Code()
    {
        var color = Hex1bColor.FromIndexed(21, 0, 0, 255);
        Assert.Equal("\x1b[48;5;21m", color.ToBackgroundAnsi());
    }

    [Fact]
    public void ToForegroundAnsi_RgbColor_Emits24BitCode()
    {
        var color = Hex1bColor.FromRgb(100, 200, 50);
        Assert.Equal("\x1b[38;2;100;200;50m", color.ToForegroundAnsi());
    }

    #endregion

    #region Terminal ProcessSgr Preservation

    [Theory]
    [InlineData("34", Hex1bColorKind.Standard, 4)]     // Blue
    [InlineData("31", Hex1bColorKind.Standard, 1)]     // Red
    [InlineData("37", Hex1bColorKind.Standard, 7)]     // White
    public void ProcessSgr_StandardForeground_PreservesIndex(string sgr, Hex1bColorKind expectedKind, int expectedIndex)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();

        terminal.ApplyTokens([new SgrToken(sgr), new TextToken("X")]);

        var cell = terminal.CreateSnapshot().GetCell(0, 0);
        Assert.NotNull(cell.Foreground);
        Assert.Equal(expectedKind, cell.Foreground.Value.Kind);
        Assert.Equal(expectedIndex, cell.Foreground.Value.AnsiIndex);
    }

    [Theory]
    [InlineData("44", Hex1bColorKind.Standard, 4)]     // Blue bg
    [InlineData("41", Hex1bColorKind.Standard, 1)]     // Red bg
    public void ProcessSgr_StandardBackground_PreservesIndex(string sgr, Hex1bColorKind expectedKind, int expectedIndex)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();

        terminal.ApplyTokens([new SgrToken(sgr), new TextToken("X")]);

        var cell = terminal.CreateSnapshot().GetCell(0, 0);
        Assert.NotNull(cell.Background);
        Assert.Equal(expectedKind, cell.Background.Value.Kind);
        Assert.Equal(expectedIndex, cell.Background.Value.AnsiIndex);
    }

    [Theory]
    [InlineData("94", Hex1bColorKind.Bright, 4)]    // Bright blue
    [InlineData("91", Hex1bColorKind.Bright, 1)]    // Bright red
    public void ProcessSgr_BrightForeground_PreservesIndex(string sgr, Hex1bColorKind expectedKind, int expectedIndex)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();

        terminal.ApplyTokens([new SgrToken(sgr), new TextToken("X")]);

        var cell = terminal.CreateSnapshot().GetCell(0, 0);
        Assert.NotNull(cell.Foreground);
        Assert.Equal(expectedKind, cell.Foreground.Value.Kind);
        Assert.Equal(expectedIndex, cell.Foreground.Value.AnsiIndex);
    }

    [Fact]
    public void ProcessSgr_256ColorForeground_PreservesIndex()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();

        terminal.ApplyTokens([new SgrToken("38;5;196"), new TextToken("X")]);

        var cell = terminal.CreateSnapshot().GetCell(0, 0);
        Assert.NotNull(cell.Foreground);
        Assert.Equal(Hex1bColorKind.Indexed, cell.Foreground.Value.Kind);
        Assert.Equal(196, cell.Foreground.Value.AnsiIndex);
    }

    [Fact]
    public void ProcessSgr_24BitRgb_StaysRgbKind()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();

        terminal.ApplyTokens([new SgrToken("38;2;100;200;50"), new TextToken("X")]);

        var cell = terminal.CreateSnapshot().GetCell(0, 0);
        Assert.NotNull(cell.Foreground);
        Assert.Equal(Hex1bColorKind.Rgb, cell.Foreground.Value.Kind);
        Assert.Equal(100, cell.Foreground.Value.R);
        Assert.Equal(200, cell.Foreground.Value.G);
        Assert.Equal(50, cell.Foreground.Value.B);
    }

    #endregion

    #region ToAnsi Round-Trip Preservation

    [Fact]
    public void ToAnsi_StandardBlue_EmitsOriginalSgrCode()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();

        // Apply standard blue foreground
        terminal.ApplyTokens([new SgrToken("34"), new TextToken("X")]);

        var snapshot = terminal.CreateSnapshot();
        var ansi = snapshot.ToAnsi(new TerminalAnsiOptions { IncludeClearScreen = true });

        // Should contain SGR 34 (standard blue), NOT 38;2;0;0;128 (RGB)
        Assert.Contains("\x1b[34m", ansi);
        Assert.DoesNotContain("38;2;0;0;128", ansi);
    }

    [Fact]
    public void ToAnsi_BrightRed_EmitsOriginalSgrCode()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();

        terminal.ApplyTokens([new SgrToken("91"), new TextToken("X")]);

        var snapshot = terminal.CreateSnapshot();
        var ansi = snapshot.ToAnsi(new TerminalAnsiOptions { IncludeClearScreen = true });

        Assert.Contains("\x1b[91m", ansi);
        Assert.DoesNotContain("38;2;255;0;0", ansi);
    }

    [Fact]
    public void ToAnsi_256Color_Emits256Code()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();

        terminal.ApplyTokens([new SgrToken("38;5;196"), new TextToken("X")]);

        var snapshot = terminal.CreateSnapshot();
        var ansi = snapshot.ToAnsi(new TerminalAnsiOptions { IncludeClearScreen = true });

        Assert.Contains("38;5;196", ansi);
    }

    [Fact]
    public void ToAnsi_24BitRgb_Emits24BitCode()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();

        terminal.ApplyTokens([new SgrToken("38;2;100;200;50"), new TextToken("X")]);

        var snapshot = terminal.CreateSnapshot();
        var ansi = snapshot.ToAnsi(new TerminalAnsiOptions { IncludeClearScreen = true });

        Assert.Contains("38;2;100;200;50", ansi);
    }

    #endregion

    #region SurfaceComparer Preservation

    [Fact]
    public void SurfaceComparer_StandardColor_EmitsOriginalCode()
    {
        var previous = new Surface(10, 1);
        var current = new Surface(10, 1);
        current[0, 0] = new SurfaceCell("X", Hex1bColor.FromStandard(4, 0, 0, 128), null);

        var diff = SurfaceComparer.Compare(previous, current);
        var tokens = SurfaceComparer.ToTokens(diff);

        var sgr = tokens.OfType<SgrToken>().FirstOrDefault();
        Assert.NotNull(sgr);
        // Should emit "34" (standard blue), not "38;2;0;0;128"
        Assert.Contains("34", sgr.Parameters.Split(';'));
        Assert.DoesNotContain("38;2", sgr.Parameters);
    }

    [Fact]
    public void SurfaceComparer_BrightColor_EmitsOriginalCode()
    {
        var previous = new Surface(10, 1);
        var current = new Surface(10, 1);
        current[0, 0] = new SurfaceCell("X", Hex1bColor.FromBright(4, 0, 0, 255), null);

        var diff = SurfaceComparer.Compare(previous, current);
        var tokens = SurfaceComparer.ToTokens(diff);

        var sgr = tokens.OfType<SgrToken>().FirstOrDefault();
        Assert.NotNull(sgr);
        Assert.Contains("94", sgr.Parameters.Split(';'));
    }

    [Fact]
    public void SurfaceComparer_IndexedColor_Emits256Code()
    {
        var previous = new Surface(10, 1);
        var current = new Surface(10, 1);
        current[0, 0] = new SurfaceCell("X", Hex1bColor.FromIndexed(196, 255, 0, 0), null);

        var diff = SurfaceComparer.Compare(previous, current);
        var tokens = SurfaceComparer.ToTokens(diff);

        var sgr = tokens.OfType<SgrToken>().FirstOrDefault();
        Assert.NotNull(sgr);
        Assert.Contains("38;5;196", sgr.Parameters);
    }

    [Fact]
    public void SurfaceComparer_RgbColor_Emits24BitCode()
    {
        var previous = new Surface(10, 1);
        var current = new Surface(10, 1);
        current[0, 0] = new SurfaceCell("X", Hex1bColor.FromRgb(100, 200, 50), null);

        var diff = SurfaceComparer.Compare(previous, current);
        var tokens = SurfaceComparer.ToTokens(diff);

        var sgr = tokens.OfType<SgrToken>().FirstOrDefault();
        Assert.NotNull(sgr);
        Assert.Contains("38;2;100;200;50", sgr.Parameters);
    }

    [Fact]
    public void SurfaceComparer_StandardBackground_EmitsOriginalCode()
    {
        var previous = new Surface(10, 1);
        var current = new Surface(10, 1);
        current[0, 0] = new SurfaceCell("X", null, Hex1bColor.FromStandard(1, 128, 0, 0));

        var diff = SurfaceComparer.Compare(previous, current);
        var tokens = SurfaceComparer.ToTokens(diff);

        var sgr = tokens.OfType<SgrToken>().FirstOrDefault();
        Assert.NotNull(sgr);
        // Background standard red = SGR 41
        Assert.Contains("41", sgr.Parameters.Split(';'));
    }

    #endregion

    #region Full Pipeline (Terminal → Surface → ANSI Output)

    [Fact]
    public async Task EmbeddedTerminal_StandardBlue_PreservedThroughSurface()
    {
        // This tests the full rendering pipeline used by EmbeddedTerminalDemo:
        // 1. Child terminal receives ESC[34m (standard blue)
        // 2. TerminalNode renders to SurfaceRenderContext via WriteClipped
        // 3. SurfaceRenderContext parses ANSI and stores Hex1bColor with preserved index
        // 4. SurfaceComparer diffs and emits final ANSI with original SGR code

        using var workload = new Hex1bAppWorkloadAdapter();
        using var childTerminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(20, 5)
            .Build();

        // Apply standard blue to the child terminal
        childTerminal.ApplyTokens([new SgrToken("34"), new TextToken("Blue!")]);

        // Verify the child terminal stores the color with its index
        var childSnapshot = childTerminal.CreateSnapshot();
        var childCell = childSnapshot.GetCell(0, 0);
        Assert.NotNull(childCell.Foreground);
        Assert.Equal(Hex1bColorKind.Standard, childCell.Foreground.Value.Kind);
        Assert.Equal(4, childCell.Foreground.Value.AnsiIndex);

        // Now verify the ToAnsi output preserves it
        var ansi = childSnapshot.ToAnsi(new TerminalAnsiOptions { IncludeClearScreen = true });
        Assert.Contains("\x1b[34m", ansi);
        Assert.DoesNotContain("38;2;0;0;128", ansi);
    }

    [Fact]
    public void SurfaceRenderContext_StandardBlue_PreservesColorKind()
    {
        // Test that SurfaceRenderContext's ANSI parser preserves color indices
        // when parsing ANSI codes written by TerminalNode.RenderRow

        var surface = new Surface(20, 1);
        var context = new SurfaceRenderContext(surface);

        // Simulate what TerminalNode.RenderRow writes: ESC[0m ESC[34m Blue!
        context.WriteClipped(0, 0, "\x1b[0m\x1b[34mBlue!");

        // The surface cell should preserve the standard blue index
        var cell = surface[0, 0];
        Assert.Equal("B", cell.Character);
        Assert.NotNull(cell.Foreground);
        Assert.Equal(Hex1bColorKind.Standard, cell.Foreground.Value.Kind);
        Assert.Equal(4, cell.Foreground.Value.AnsiIndex);
    }

    [Fact]
    public void SurfaceRenderContext_BrightRed_PreservesColorKind()
    {
        var surface = new Surface(20, 1);
        var context = new SurfaceRenderContext(surface);

        context.WriteClipped(0, 0, "\x1b[91mRed!");

        var cell = surface[0, 0];
        Assert.NotNull(cell.Foreground);
        Assert.Equal(Hex1bColorKind.Bright, cell.Foreground.Value.Kind);
        Assert.Equal(1, cell.Foreground.Value.AnsiIndex);
    }

    [Fact]
    public void SurfaceRenderContext_256Color_PreservesColorKind()
    {
        var surface = new Surface(20, 1);
        var context = new SurfaceRenderContext(surface);

        context.WriteClipped(0, 0, "\x1b[38;5;196mRed!");

        var cell = surface[0, 0];
        Assert.NotNull(cell.Foreground);
        Assert.Equal(Hex1bColorKind.Indexed, cell.Foreground.Value.Kind);
        Assert.Equal(196, cell.Foreground.Value.AnsiIndex);
    }

    [Fact]
    public void SurfaceRenderContext_24BitRgb_StaysRgbKind()
    {
        var surface = new Surface(20, 1);
        var context = new SurfaceRenderContext(surface);

        context.WriteClipped(0, 0, "\x1b[38;2;100;200;50mGreen!");

        var cell = surface[0, 0];
        Assert.NotNull(cell.Foreground);
        Assert.Equal(Hex1bColorKind.Rgb, cell.Foreground.Value.Kind);
        Assert.Equal(100, cell.Foreground.Value.R);
        Assert.Equal(200, cell.Foreground.Value.G);
        Assert.Equal(50, cell.Foreground.Value.B);
    }

    #endregion
}
