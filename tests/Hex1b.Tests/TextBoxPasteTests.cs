using Hex1b.Input;

namespace Hex1b.Tests;

/// <summary>
/// Tests for TextBoxNode paste handling.
/// Verifies that bracketed paste inserts text at cursor, replaces selection,
/// strips newlines for single-line boxes, and fires TextChanged callbacks.
/// </summary>
public class TextBoxPasteTests
{
    private static PasteContext CreatePaste(string text)
    {
        var ctx = new PasteContext();
        ctx.TryWrite(text);
        ctx.Complete();
        return ctx;
    }

    [Fact]
    public async Task Paste_InsertsAtCursor()
    {
        var node = new TextBoxNode { Text = "abcd" };
        node.State.CursorPosition = 2; // between 'b' and 'c'
        var paste = CreatePaste("XY");

        var result = await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal("abXYcd", node.Text);
        Assert.Equal(4, node.State.CursorPosition); // after "XY"
        await paste.DisposeAsync();
    }

    [Fact]
    public async Task Paste_InsertsAtEnd()
    {
        var node = new TextBoxNode { Text = "hello" };
        node.State.CursorPosition = 5;
        var paste = CreatePaste(" world");

        await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.Equal("hello world", node.Text);
        Assert.Equal(11, node.State.CursorPosition);
        await paste.DisposeAsync();
    }

    [Fact]
    public async Task Paste_InsertsAtStart()
    {
        var node = new TextBoxNode { Text = "world" };
        node.State.CursorPosition = 0;
        var paste = CreatePaste("hello ");

        await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.Equal("hello world", node.Text);
        Assert.Equal(6, node.State.CursorPosition);
        await paste.DisposeAsync();
    }

    [Fact]
    public async Task Paste_ReplacesSelection()
    {
        var node = new TextBoxNode { Text = "hello world" };
        // Select "world" (positions 6-11)
        node.State.CursorPosition = 11;
        node.State.SelectionAnchor = 6;
        var paste = CreatePaste("earth");

        await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.Equal("hello earth", node.Text);
        Assert.Equal(11, node.State.CursorPosition);
        await paste.DisposeAsync();
    }

    [Fact]
    public async Task Paste_EmptyString_NoChange()
    {
        var node = new TextBoxNode { Text = "hello" };
        node.State.CursorPosition = 3;
        var paste = CreatePaste("");

        var result = await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal("hello", node.Text);
        Assert.Equal(3, node.State.CursorPosition);
        await paste.DisposeAsync();
    }

    [Fact]
    public async Task Paste_MultiLine_NewlinesReplacedWithSpaces()
    {
        var node = new TextBoxNode { Text = "" };
        node.State.CursorPosition = 0;
        var paste = CreatePaste("line1\nline2\r\nline3");

        await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.Equal("line1 line2 line3", node.Text);
        await paste.DisposeAsync();
    }

    [Fact]
    public async Task Paste_Unicode_InsertedCorrectly()
    {
        var node = new TextBoxNode { Text = "abc" };
        node.State.CursorPosition = 3;
        var paste = CreatePaste("こんにちは 🌍");

        await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.Equal("abcこんにちは 🌍", node.Text);
        await paste.DisposeAsync();
    }

    [Fact]
    public async Task Paste_LargeText_Inserted()
    {
        var node = new TextBoxNode { Text = "" };
        node.State.CursorPosition = 0;
        var largeText = new string('x', 10_000);
        var paste = CreatePaste(largeText);

        await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.Equal(10_000, node.Text.Length);
        Assert.Equal(10_000, node.State.CursorPosition);
        await paste.DisposeAsync();
    }

    [Fact]
    public async Task Paste_FiresTextChangedCallback()
    {
        string? callbackOldText = null;
        string? callbackNewText = null;
        var node = new TextBoxNode { Text = "old" };
        node.State.CursorPosition = 3;
        node.TextChangedAction = (ctx, oldText, newText) =>
        {
            callbackOldText = oldText;
            callbackNewText = newText;
            return Task.CompletedTask;
        };
        var paste = CreatePaste("new");

        await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.Equal("old", callbackOldText);
        Assert.Equal("oldnew", callbackNewText);
        await paste.DisposeAsync();
    }
}
