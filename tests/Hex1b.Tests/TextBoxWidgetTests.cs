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

    [Fact]
    public async Task Reconcile_HoistedState_RoutesParentInstanceIntoNode()
    {
        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var state = new TextBoxState("initial");
        var node = (TextBoxNode)await new TextBoxWidget().State(state).ReconcileAsync(null, context);

        // The node must be backed by the exact instance the parent supplied —
        // not a copy — so subsequent parent mutations are observed.
        Assert.Same(state, node.State);
        Assert.Equal("initial", node.Text);
    }

    [Fact]
    public async Task Reconcile_HoistedState_PreservedAcrossReconciles()
    {
        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var state = new TextBoxState("first");
        var node = (TextBoxNode)await new TextBoxWidget().State(state).ReconcileAsync(null, context);
        Assert.Same(state, node.State);

        // A new widget instance pointing at the same state must NOT replace
        // the state instance on the node.
        context.IsNew = false;
        var node2 = (TextBoxNode)await new TextBoxWidget().State(state).ReconcileAsync(node, context);

        Assert.Same(node, node2);
        Assert.Same(state, node2.State);
    }

    [Fact]
    public async Task Reconcile_HoistedState_ParentMutationVisibleAfterReconcile()
    {
        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var state = new TextBoxState("hello");
        var node = (TextBoxNode)await new TextBoxWidget().State(state).ReconcileAsync(null, context);
        Assert.Equal("hello", node.Text);

        // Parent rewrites the state from outside the widget tree, then
        // reconciles. The node should observe the new text — and a render
        // should be scheduled (the dirty-tracking version on the state must
        // have advanced past the node's last-seen value).
        state.Text = "world";

        context.IsNew = false;
        await new TextBoxWidget().State(state).ReconcileAsync(node, context);

        Assert.Equal("world", node.Text);
    }

    [Fact]
    public async Task Reconcile_HoistedState_PeerInstancesAreIndependent()
    {
        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var stateA = new TextBoxState("alpha");
        var stateB = new TextBoxState("beta");

        var nodeA = (TextBoxNode)await new TextBoxWidget().State(stateA).ReconcileAsync(null, context);
        var nodeB = (TextBoxNode)await new TextBoxWidget().State(stateB).ReconcileAsync(null, context);

        // Each node should have its own state — mutating one must not
        // affect the other.
        stateA.Text = "alpha-2";
        Assert.Equal("alpha-2", nodeA.Text);
        Assert.Equal("beta", nodeB.Text);
    }

    [Fact]
    public async Task Reconcile_HoistedStateAndCtorText_Throws()
    {
        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var state = new TextBoxState("from-state");
        var widget = new TextBoxWidget("from-ctor").State(state);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await widget.ReconcileAsync(null, context));
    }
}

public class TextBoxStateHoistedTests
{
    [Fact]
    public void DefaultCtor_ProducesEmptyState()
    {
        var state = new TextBoxState();
        Assert.Equal(string.Empty, state.Text);
        Assert.Equal(0, state.CursorPosition);
    }

    [Fact]
    public void InitialTextCtor_ClampsCursorToEnd()
    {
        var state = new TextBoxState("hello");
        Assert.Equal("hello", state.Text);
        Assert.Equal(5, state.CursorPosition);
    }

    [Fact]
    public void TextSetter_BumpsVersion()
    {
        var state = new TextBoxState("hello");
        var v0 = ReadVersion(state);

        state.Text = "world";
        var v1 = ReadVersion(state);
        Assert.True(v1 > v0);
    }

    [Fact]
    public void TextSetter_NoOp_DoesNotBumpVersion()
    {
        var state = new TextBoxState("hello");
        var v0 = ReadVersion(state);

        // Same value — no mutation, no version bump.
        state.Text = "hello";
        Assert.Equal(v0, ReadVersion(state));
    }

    [Fact]
    public void CursorSetter_BumpsVersion()
    {
        var state = new TextBoxState("hello");
        var v0 = ReadVersion(state);

        state.CursorPosition = 2;
        Assert.True(ReadVersion(state) > v0);
    }

    private static long ReadVersion(TextBoxState state)
    {
        var prop = typeof(TextBoxState).GetProperty(
            "Version",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return (long)prop.GetValue(state)!;
    }
}

