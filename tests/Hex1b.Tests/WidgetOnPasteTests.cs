using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for OnPaste fluent methods on widgets (Phase 5).
/// Verifies that custom paste handlers override default behavior.
/// </summary>
public class WidgetOnPasteTests
{
    private static PasteContext CreatePaste(string text)
    {
        var ctx = new PasteContext();
        ctx.TryWrite(text);
        ctx.Complete();
        return ctx;
    }

    [Fact]
    public async Task OnPaste_OverridesDefault()
    {
        // With OnPaste set, default text insertion should NOT happen
        string? customReceived = null;
        var node = new TextBoxNode { Text = "original" };
        node.State.CursorPosition = 8;
        node.CustomPasteAction = async paste =>
        {
            customReceived = await paste.ReadToEndAsync();
        };
        var paste = CreatePaste("custom");

        var result = await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal("custom", customReceived);
        // Text should NOT have been modified by default handler
        Assert.Equal("original", node.Text);
        await paste.DisposeAsync();
    }

    [Fact]
    public async Task OnPaste_ReceivesPasteContext()
    {
        PasteContext? receivedContext = null;
        var node = new TextBoxNode();
        node.CustomPasteAction = paste =>
        {
            receivedContext = paste;
            return Task.CompletedTask;
        };
        var paste = CreatePaste("test");

        await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.Same(paste, receivedContext);
        await paste.DisposeAsync();
    }

    [Fact]
    public async Task OnPaste_CanReadToEnd()
    {
        string? result = null;
        var node = new TextBoxNode();
        node.CustomPasteAction = async paste =>
        {
            result = await paste.ReadToEndAsync();
        };
        var paste = CreatePaste("hello world");

        await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.Equal("hello world", result);
        await paste.DisposeAsync();
    }

    [Fact]
    public async Task OnPaste_CanCancel()
    {
        var node = new TextBoxNode();
        node.CustomPasteAction = paste =>
        {
            paste.Cancel();
            return Task.CompletedTask;
        };
        // Don't call Complete() so Cancel() can fire
        var paste = new PasteContext();
        paste.TryWrite("cancelled");

        await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.True(paste.IsCancelled);
        await paste.DisposeAsync();
    }

    [Fact]
    public async Task OnPaste_WithoutHandler_DefaultBehavior()
    {
        // Without OnPaste, default text insertion should happen
        var node = new TextBoxNode { Text = "" };
        node.State.CursorPosition = 0;
        // CustomPasteAction is null by default
        var paste = CreatePaste("inserted");

        await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.Equal("inserted", node.Text);
        await paste.DisposeAsync();
    }

    [Fact]
    public void Widget_OnPaste_FluentApi()
    {
        var widget = new TextBoxWidget("test");

        var withPaste = widget.OnPaste(async paste => await paste.ReadToEndAsync());
        Assert.NotNull(withPaste.PasteHandler);

        var withSyncPaste = widget.OnPaste(paste => { });
        Assert.NotNull(withSyncPaste.PasteHandler);
    }

    [Fact]
    public async Task Widget_OnPaste_WiredThroughReconciliation()
    {
        bool handlerCalled = false;
        var widget = new TextBoxWidget("test")
            .OnPaste(async paste =>
            {
                handlerCalled = true;
                await paste.ReadToEndAsync();
            });

        // Reconcile to create node
        var context = ReconcileContext.CreateRoot();
        var node = await widget.ReconcileAsync(null, context) as TextBoxNode;

        Assert.NotNull(node);
        Assert.NotNull(node!.CustomPasteAction);

        // Invoke the handler
        var paste = CreatePaste("test");
        await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.True(handlerCalled);
        // Default insert should NOT have happened since custom handler was set
        Assert.Equal("test", node.Text); // original text unchanged
        await paste.DisposeAsync();
    }
}
