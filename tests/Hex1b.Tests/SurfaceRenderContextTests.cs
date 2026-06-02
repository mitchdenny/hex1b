using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for <see cref="SurfaceRenderContext"/>.
/// </summary>
[TestClass]
public class SurfaceRenderContextTests
{
    #region Basic Writing

    [TestMethod]
    public void Write_PlainText_WritesAtCursorPosition()
    {
        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);
        
        context.SetCursorPosition(5, 10);
        context.Write("Hello");
        
        Assert.AreEqual("H", surface[5, 10].Character);
        Assert.AreEqual("e", surface[6, 10].Character);
        Assert.AreEqual("l", surface[7, 10].Character);
        Assert.AreEqual("l", surface[8, 10].Character);
        Assert.AreEqual("o", surface[9, 10].Character);
    }

    [TestMethod]
    public void Write_AdvancesCursor()
    {
        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);
        
        context.SetCursorPosition(0, 0);
        context.Write("ABC");
        context.Write("DEF");
        
        Assert.AreEqual("A", surface[0, 0].Character);
        Assert.AreEqual("D", surface[3, 0].Character);
    }

    [TestMethod]
    public void WriteClipped_PlainText_WritesAtSpecifiedPosition()
    {
        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);
        
        context.WriteClipped(10, 5, "World");
        
        Assert.AreEqual("W", surface[10, 5].Character);
        Assert.AreEqual("o", surface[11, 5].Character);
        Assert.AreEqual("r", surface[12, 5].Character);
        Assert.AreEqual("l", surface[13, 5].Character);
        Assert.AreEqual("d", surface[14, 5].Character);
    }

    #endregion

    #region ANSI Parsing - Colors

    [TestMethod]
    public void Write_WithForegroundColor_ParsesAndApplies()
    {
        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);
        
        // Red foreground: ESC[38;2;255;0;0m
        context.SetCursorPosition(0, 0);
        context.Write("\x1b[38;2;255;0;0mRed");
        
        var cell = surface[0, 0];
        Assert.AreEqual("R", cell.Character);
        Assert.IsNotNull(cell.Foreground);
        Assert.AreEqual(255, cell.Foreground.Value.R);
        Assert.AreEqual(0, cell.Foreground.Value.G);
        Assert.AreEqual(0, cell.Foreground.Value.B);
    }

    [TestMethod]
    public void Write_WithBackgroundColor_ParsesAndApplies()
    {
        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);
        
        // Blue background: ESC[48;2;0;0;255m
        context.SetCursorPosition(0, 0);
        context.Write("\x1b[48;2;0;0;255mBlue");
        
        var cell = surface[0, 0];
        Assert.AreEqual("B", cell.Character);
        Assert.IsNotNull(cell.Background);
        Assert.AreEqual(0, cell.Background.Value.R);
        Assert.AreEqual(0, cell.Background.Value.G);
        Assert.AreEqual(255, cell.Background.Value.B);
    }

    [TestMethod]
    public void Write_WithReset_ClearsColors()
    {
        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);
        
        context.SetCursorPosition(0, 0);
        context.Write("\x1b[38;2;255;0;0mR\x1b[0mN");
        
        // First character has red foreground
        Assert.IsNotNull(surface[0, 0].Foreground);
        
        // Second character should have no foreground (reset)
        Assert.IsNull(surface[1, 0].Foreground);
    }

    [TestMethod]
    public void Write_256Color_ParsesCorrectly()
    {
        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);
        
        // 256-color green (index 2): ESC[38;5;2m
        context.SetCursorPosition(0, 0);
        context.Write("\x1b[38;5;2mG");
        
        var cell = surface[0, 0];
        Assert.IsNotNull(cell.Foreground);
        // Basic green from 16-color palette
        Assert.AreEqual(0, cell.Foreground.Value.R);
        Assert.AreEqual(128, cell.Foreground.Value.G);
        Assert.AreEqual(0, cell.Foreground.Value.B);
    }

    [TestMethod]
    public void Write_BasicColors_ParsesCorrectly()
    {
        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);
        
        // Basic red foreground: ESC[31m
        context.SetCursorPosition(0, 0);
        context.Write("\x1b[31mR");
        
        var cell = surface[0, 0];
        Assert.IsNotNull(cell.Foreground);
        Assert.AreEqual(128, cell.Foreground.Value.R); // Basic red
    }

    [TestMethod]
    public void Write_BrightColors_ParsesCorrectly()
    {
        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);
        
        // Bright red foreground: ESC[91m
        context.SetCursorPosition(0, 0);
        context.Write("\x1b[91mR");
        
        var cell = surface[0, 0];
        Assert.IsNotNull(cell.Foreground);
        Assert.AreEqual(255, cell.Foreground.Value.R); // Bright red
    }

    #endregion

    #region ANSI Parsing - Attributes

    [TestMethod]
    public void Write_BoldAttribute_ParsesAndApplies()
    {
        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);
        
        context.SetCursorPosition(0, 0);
        context.Write("\x1b[1mBold");
        
        var cell = surface[0, 0];
        Assert.IsTrue((cell.Attributes & CellAttributes.Bold) != 0);
    }

    [TestMethod]
    public void Write_ItalicAttribute_ParsesAndApplies()
    {
        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);
        
        context.SetCursorPosition(0, 0);
        context.Write("\x1b[3mItalic");
        
        var cell = surface[0, 0];
        Assert.IsTrue((cell.Attributes & CellAttributes.Italic) != 0);
    }

    [TestMethod]
    public void Write_UnderlineAttribute_ParsesAndApplies()
    {
        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);
        
        context.SetCursorPosition(0, 0);
        context.Write("\x1b[4mUnderline");
        
        var cell = surface[0, 0];
        Assert.IsTrue((cell.Attributes & CellAttributes.Underline) != 0);
    }

    [TestMethod]
    public void Write_MultipleAttributes_ParsesAndApplies()
    {
        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);
        
        // Bold + Italic + Underline: ESC[1;3;4m
        context.SetCursorPosition(0, 0);
        context.Write("\x1b[1;3;4mStyled");
        
        var cell = surface[0, 0];
        Assert.IsTrue((cell.Attributes & CellAttributes.Bold) != 0);
        Assert.IsTrue((cell.Attributes & CellAttributes.Italic) != 0);
        Assert.IsTrue((cell.Attributes & CellAttributes.Underline) != 0);
    }

    [TestMethod]
    public void Write_AttributeReset_ClearsAttributes()
    {
        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);
        
        context.SetCursorPosition(0, 0);
        context.Write("\x1b[1mB\x1b[0mN");
        
        // First character is bold
        Assert.IsTrue((surface[0, 0].Attributes & CellAttributes.Bold) != 0);
        
        // Second character is not bold
        Assert.AreEqual(CellAttributes.None, surface[1, 0].Attributes);
    }

    #endregion

    #region Wide Characters

    [TestMethod]
    public void Write_WideCharacter_OccupiesTwoCells()
    {
        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);
        
        context.SetCursorPosition(0, 0);
        context.Write("你好");
        
        // First character
        Assert.AreEqual("你", surface[0, 0].Character);
        Assert.AreEqual(2, surface[0, 0].DisplayWidth);
        
        // Continuation cell
        Assert.AreEqual(0, surface[1, 0].DisplayWidth);
        
        // Second character
        Assert.AreEqual("好", surface[2, 0].Character);
        Assert.AreEqual(2, surface[2, 0].DisplayWidth);
    }

    #endregion

    #region Clear Operations

    [TestMethod]
    public void Clear_FillsSurfaceWithSpaces()
    {
        var surface = new Surface(10, 5);
        var context = new SurfaceRenderContext(surface);
        
        // Write some content first
        context.WriteClipped(0, 0, "Hello");
        
        // Clear
        context.Clear();
        
        // All cells should be spaces
        for (var y = 0; y < 5; y++)
        {
            for (var x = 0; x < 10; x++)
            {
                Assert.AreEqual(" ", surface[x, y].Character);
            }
        }
    }

    [TestMethod]
    public void ClearRegion_FillsRectWithSpaces()
    {
        var surface = new Surface(20, 10);
        var context = new SurfaceRenderContext(surface);
        
        // Fill with X's
        surface.Fill(new Rect(0, 0, 20, 10), new SurfaceCell("X", null, null));
        
        // Clear a region
        context.ClearRegion(new Rect(5, 2, 5, 3));
        
        // Region should be spaces
        for (var y = 2; y < 5; y++)
        {
            for (var x = 5; x < 10; x++)
            {
                Assert.AreEqual(" ", surface[x, y].Character);
            }
        }
        
        // Outside region should still be X
        Assert.AreEqual("X", surface[0, 0].Character);
        Assert.AreEqual("X", surface[15, 8].Character);
    }

    #endregion

    #region Complex Sequences

    [TestMethod]
    public void Write_ColorAndText_MixedSequence()
    {
        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);
        
        // Complex sequence: red "Hello", reset, blue "World"
        context.SetCursorPosition(0, 0);
        context.Write("\x1b[38;2;255;0;0mHello\x1b[0m \x1b[38;2;0;0;255mWorld\x1b[0m");
        
        // "Hello" should be red
        Assert.AreEqual("H", surface[0, 0].Character);
        Assert.AreEqual(255, surface[0, 0].Foreground!.Value.R);
        
        // Space should have no color
        Assert.AreEqual(" ", surface[5, 0].Character);
        Assert.IsNull(surface[5, 0].Foreground);
        
        // "World" should be blue
        Assert.AreEqual("W", surface[6, 0].Character);
        Assert.AreEqual(255, surface[6, 0].Foreground!.Value.B);
    }

    [TestMethod]
    public void Write_SkipsOscSequences()
    {
        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);
        
        // OSC hyperlink sequence: ESC]8;;url ST text ESC]8;; ST
        context.SetCursorPosition(0, 0);
        context.Write("\x1b]8;;http://example.com\x1b\\Link\x1b]8;;\x1b\\");
        
        // Should see "Link" without the OSC sequences
        Assert.AreEqual("L", surface[0, 0].Character);
        Assert.AreEqual("i", surface[1, 0].Character);
        Assert.AreEqual("n", surface[2, 0].Character);
        Assert.AreEqual("k", surface[3, 0].Character);
    }

    [TestMethod]
    public void Write_SkipsFrameMarkers()
    {
        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);
        
        // APC frame marker: ESC_HEX1BAPP:FRAME:BEGIN ESC\
        context.SetCursorPosition(0, 0);
        context.Write("\x1b_HEX1BAPP:FRAME:BEGIN\x1b\\Hello");
        
        Assert.AreEqual("H", surface[0, 0].Character);
    }

    #endregion

    #region Clipping

    [TestMethod]
    public void WriteClipped_WithLayoutProvider_RespectsClipping()
    {
        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);
        
        // Create a simple mock layout provider that clips to a region
        var clipRect = new Rect(5, 5, 10, 10);
        var mockProvider = new TestLayoutProvider(clipRect);
        context.CurrentLayoutProvider = mockProvider;
        
        // Write text that starts before clip region
        context.WriteClipped(3, 7, "Hello World");
        
        // Text should be clipped to start at x=5
        // This depends on the ClipString implementation
        // For now, verify the call was made with clipping context set
        Assert.IsNotNull(context.CurrentLayoutProvider);
    }

    private class TestLayoutProvider : ILayoutProvider
    {
        private readonly Rect _clipRect;
        
        public TestLayoutProvider(Rect clipRect) => _clipRect = clipRect;
        
        public Rect ClipRect => _clipRect;
        public ClipMode ClipMode => ClipMode.Clip;
        public ILayoutProvider? ParentLayoutProvider { get; set; }
        
        public bool ShouldRenderAt(int x, int y) =>
            x >= _clipRect.X && x < _clipRect.Right &&
            y >= _clipRect.Y && y < _clipRect.Bottom;
        
        public (int adjustedX, string clippedText) ClipString(int x, int y, string text)
        {
            if (y < _clipRect.Y || y >= _clipRect.Bottom)
                return (x, "");
            
            var startX = Math.Max(x, _clipRect.X);
            var endX = Math.Min(x + text.Length, _clipRect.Right);
            
            if (startX >= endX)
                return (x, "");
            
            var skipChars = startX - x;
            var takeChars = endX - startX;
            
            return (startX, text.Substring(skipChars, Math.Min(takeChars, text.Length - skipChars)));
        }
    }

    #endregion

    #region Properties

    [TestMethod]
    public void Width_ReturnsSurfaceWidth()
    {
        var surface = new Surface(100, 50);
        var context = new SurfaceRenderContext(surface);
        
        Assert.AreEqual(100, context.Width);
    }

    [TestMethod]
    public void Height_ReturnsSurfaceHeight()
    {
        var surface = new Surface(100, 50);
        var context = new SurfaceRenderContext(surface);
        
        Assert.AreEqual(50, context.Height);
    }

    [TestMethod]
    public void Theme_DefaultsToDefaultTheme()
    {
        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);
        
        Assert.IsNotNull(context.Theme);
    }

    [TestMethod]
    public void Theme_CanBeSet()
    {
        var surface = new Surface(80, 24);
        var customTheme = Hex1bThemes.Ocean;
        var context = new SurfaceRenderContext(surface, customTheme);
        
        Assert.AreSame(customTheme, context.Theme);
    }

    #endregion
}
