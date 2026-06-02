using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for OnPaste fluent methods on widgets (Phase 5).
/// Verifies that custom paste handlers override default behavior.
/// </summary>
[TestClass]
public class WidgetOnPasteTests
{
    private static PasteContext CreatePaste(string text)
    {
        var ctx = new PasteContext();
        ctx.TryWrite(text);
        ctx.Complete();
        return ctx;
    }

    [TestMethod]
    public async Task OnPaste_OverridesDefault()
    {
        // With OnPaste set, default text insertion should NOT happen
        string? customReceived = null;
        var node = new TextBoxNode { Text = "original" };
        node.State.CursorPosition = 8;
        node.CustomPasteAction = async e =>
        {
            customReceived = await e.Paste.ReadToEndAsync();
        };
        var paste = CreatePaste("custom");

        var result = await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual("custom", customReceived);
        // Text should NOT have been modified by default handler
        Assert.AreEqual("original", node.Text);
        await paste.DisposeAsync();
    }

    [TestMethod]
    public async Task OnPaste_ReceivesPasteContext()
    {
        PasteContext? receivedContext = null;
        var node = new TextBoxNode();
        node.CustomPasteAction = e =>
        {
            receivedContext = e.Paste;
            return Task.CompletedTask;
        };
        var paste = CreatePaste("test");

        await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.AreSame(paste, receivedContext);
        await paste.DisposeAsync();
    }

    [TestMethod]
    public async Task OnPaste_CanReadToEnd()
    {
        string? result = null;
        var node = new TextBoxNode();
        node.CustomPasteAction = async e =>
        {
            result = await e.Paste.ReadToEndAsync();
        };
        var paste = CreatePaste("hello world");

        await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.AreEqual("hello world", result);
        await paste.DisposeAsync();
    }

    [TestMethod]
    public async Task OnPaste_CanCancel()
    {
        var node = new TextBoxNode();
        node.CustomPasteAction = e =>
        {
            e.Paste.Cancel();
            return Task.CompletedTask;
        };
        // Don't call Complete() so Cancel() can fire
        var paste = new PasteContext();
        paste.TryWrite("cancelled");

        await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.IsTrue(paste.IsCancelled);
        await paste.DisposeAsync();
    }

    [TestMethod]
    public async Task OnPaste_WithoutHandler_DefaultBehavior()
    {
        // Without OnPaste, default text insertion should happen
        var node = new TextBoxNode { Text = "" };
        node.State.CursorPosition = 0;
        // CustomPasteAction is null by default
        var paste = CreatePaste("inserted");

        await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.AreEqual("inserted", node.Text);
        await paste.DisposeAsync();
    }

    [TestMethod]
    public void Widget_OnPaste_FluentApi()
    {
        var widget = new TextBoxWidget("test");

        var withPaste = widget.OnPaste(async e => await e.Paste.ReadToEndAsync());
        Assert.IsNotNull(withPaste.PasteHandler);

        var withSyncPaste = widget.OnPaste(e => { });
        Assert.IsNotNull(withSyncPaste.PasteHandler);
    }

    [TestMethod]
    public async Task Widget_OnPaste_WiredThroughReconciliation()
    {
        bool handlerCalled = false;
        var widget = new TextBoxWidget("test")
            .OnPaste(async e =>
            {
                handlerCalled = true;
                await e.Paste.ReadToEndAsync();
            });

        // Reconcile to create node
        var context = ReconcileContext.CreateRoot();
        var node = await widget.ReconcileAsync(null, context) as TextBoxNode;

        Assert.IsNotNull(node);
        Assert.IsNotNull(node!.CustomPasteAction);

        // Invoke the handler
        var paste = CreatePaste("test");
        await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.IsTrue(handlerCalled);
        // Default insert should NOT have happened since custom handler was set
        Assert.AreEqual("test", node.Text); // original text unchanged
        await paste.DisposeAsync();
    }
}
