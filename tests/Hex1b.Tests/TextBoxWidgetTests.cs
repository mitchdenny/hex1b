using Hex1b.Widgets;

namespace Hex1b.Tests;

public class TextBoxWidgetTests
{
    [Fact]
    public async Task Reconcile_NewNode_WithInitialText_PlacesCursorAtEnd()
    {
        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var widget = new TextBoxWidget("hello");
        var node = (TextBoxNode)await widget.ReconcileAsync(null, context);

        Assert.Equal("hello", node.Text);
        Assert.Equal("hello".Length, node.State.CursorPosition);
    }

    [Fact]
    public async Task Reconcile_ExternalTextChange_PlacesCursorAtEnd()
    {
        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var node = (TextBoxNode)await new TextBoxWidget("hello").ReconcileAsync(null, context);
        Assert.Equal("hello".Length, node.State.CursorPosition);

        // Move cursor away from the end, then simulate an external programmatic text update.
        node.State.CursorPosition = 2;

        context.IsNew = false;
        await new TextBoxWidget("hello world").ReconcileAsync(node, context);

        Assert.Equal("hello world", node.Text);
        Assert.Equal("hello world".Length, node.State.CursorPosition);
    }

    [Fact]
    public async Task Reconcile_ControlledSync_DoesNotResetCursorWhenTextAlreadyMatchesState()
    {
        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var node = (TextBoxNode)await new TextBoxWidget("hello").ReconcileAsync(null, context);

        // Simulate user input updating internal node state first (cursor in the middle),
        // and then the owner syncing the same text value on the next render.
        node.State.Text = "heXllo";
        node.State.CursorPosition = 3;

        context.IsNew = false;
        await new TextBoxWidget("heXllo").ReconcileAsync(node, context);

        Assert.Equal(3, node.State.CursorPosition);
    }
}

