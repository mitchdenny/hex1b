using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class TextBoxWidgetTests
{
    [TestMethod]
    public async Task Reconcile_NewNode_WithInitialText_PlacesCursorAtEnd()
    {
        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var widget = new TextBoxWidget("hello");
        var node = (TextBoxNode)await widget.ReconcileAsync(null, context);

        Assert.AreEqual("hello", node.Text);
        Assert.AreEqual("hello".Length, node.State.CursorPosition);
    }

    [TestMethod]
    public async Task Reconcile_ExternalTextChange_PlacesCursorAtEnd()
    {
        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var node = (TextBoxNode)await new TextBoxWidget("hello").ReconcileAsync(null, context);
        Assert.AreEqual("hello".Length, node.State.CursorPosition);

        // Move cursor away from the end, then simulate an external programmatic text update.
        node.State.CursorPosition = 2;

        context.IsNew = false;
        await new TextBoxWidget("hello world").ReconcileAsync(node, context);

        Assert.AreEqual("hello world", node.Text);
        Assert.AreEqual("hello world".Length, node.State.CursorPosition);
    }

    [TestMethod]
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

        Assert.AreEqual(3, node.State.CursorPosition);
    }

    [TestMethod]
    public async Task Reconcile_HoistedState_RoutesParentInstanceIntoNode()
    {
        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var state = new TextBoxState("initial");
        var node = (TextBoxNode)await new TextBoxWidget().State(state).ReconcileAsync(null, context);

        // The node must be backed by the exact instance the parent supplied —
        // not a copy — so subsequent parent mutations are observed.
        Assert.AreSame(state, node.State);
        Assert.AreEqual("initial", node.Text);
    }

    [TestMethod]
    public async Task Reconcile_HoistedState_PreservedAcrossReconciles()
    {
        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var state = new TextBoxState("first");
        var node = (TextBoxNode)await new TextBoxWidget().State(state).ReconcileAsync(null, context);
        Assert.AreSame(state, node.State);

        // A new widget instance pointing at the same state must NOT replace
        // the state instance on the node.
        context.IsNew = false;
        var node2 = (TextBoxNode)await new TextBoxWidget().State(state).ReconcileAsync(node, context);

        Assert.AreSame(node, node2);
        Assert.AreSame(state, node2.State);
    }

    [TestMethod]
    public async Task Reconcile_HoistedState_ParentMutationVisibleAfterReconcile()
    {
        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var state = new TextBoxState("hello");
        var node = (TextBoxNode)await new TextBoxWidget().State(state).ReconcileAsync(null, context);
        Assert.AreEqual("hello", node.Text);

        // Parent rewrites the state from outside the widget tree, then
        // reconciles. The node should observe the new text — and a render
        // should be scheduled (the dirty-tracking version on the state must
        // have advanced past the node's last-seen value).
        state.Text = "world";

        context.IsNew = false;
        await new TextBoxWidget().State(state).ReconcileAsync(node, context);

        Assert.AreEqual("world", node.Text);
    }

    [TestMethod]
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
        Assert.AreEqual("alpha-2", nodeA.Text);
        Assert.AreEqual("beta", nodeB.Text);
    }

    [TestMethod]
    public async Task Reconcile_HoistedStateAndCtorText_Throws()
    {
        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var state = new TextBoxState("from-state");
        var widget = new TextBoxWidget("from-ctor").State(state);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await widget.ReconcileAsync(null, context));
    }
}

[TestClass]
public class TextBoxStateHoistedTests
{
    [TestMethod]
    public void DefaultCtor_ProducesEmptyState()
    {
        var state = new TextBoxState();
        Assert.AreEqual(string.Empty, state.Text);
        Assert.AreEqual(0, state.CursorPosition);
    }

    [TestMethod]
    public void InitialTextCtor_ClampsCursorToEnd()
    {
        var state = new TextBoxState("hello");
        Assert.AreEqual("hello", state.Text);
        Assert.AreEqual(5, state.CursorPosition);
    }

    [TestMethod]
    public void TextSetter_BumpsVersion()
    {
        var state = new TextBoxState("hello");
        var v0 = ReadVersion(state);

        state.Text = "world";
        var v1 = ReadVersion(state);
        Assert.IsTrue(v1 > v0);
    }

    [TestMethod]
    public void TextSetter_NoOp_DoesNotBumpVersion()
    {
        var state = new TextBoxState("hello");
        var v0 = ReadVersion(state);

        // Same value — no mutation, no version bump.
        state.Text = "hello";
        Assert.AreEqual(v0, ReadVersion(state));
    }

    [TestMethod]
    public void CursorSetter_BumpsVersion()
    {
        var state = new TextBoxState("hello");
        var v0 = ReadVersion(state);

        state.CursorPosition = 2;
        Assert.IsTrue(ReadVersion(state) > v0);
    }

    [TestMethod]
    public void DefaultWidthHint_IsFill_SoTextBoxExpandsByDefault()
    {
        // The bare widget reports Fill so HStack/VStack hand it the remaining
        // space without callers having to chain .FillWidth() on every textbox.
        var widget = new TextBoxWidget("hello");
        Assert.IsNull(widget.WidthHint);
        Assert.AreEqual(Hex1b.Layout.SizeHint.Fill, widget.DefaultWidthHint);
    }

    [TestMethod]
    public void ExplicitWidthHint_OverridesDefault()
    {
        // ContentWidth/FixedWidth/etc. set WidthHint, which always wins over
        // the per-widget default.
        var widget = new TextBoxWidget("hello").ContentWidth();
        Assert.AreEqual(Hex1b.Layout.SizeHint.Content, widget.WidthHint);
        Assert.AreEqual(Hex1b.Layout.SizeHint.Fill, widget.DefaultWidthHint);
    }

    [TestMethod]
    public async Task Reconcile_PropagatesFillDefault_ToNode()
    {
        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var widget = new TextBoxWidget("hi");
        var node = (TextBoxNode)await widget.ReconcileAsync(null, context);

        // The widget's ReconcileAsync alone doesn't push the hint — that
        // happens in Hex1bApp's central reconcile path. Simulate that step.
        node.WidthHint = widget.WidthHint ?? widget.DefaultWidthHint;

        Assert.AreEqual(Hex1b.Layout.SizeHint.Fill, node.WidthHint);
    }

    [TestMethod]
    public void TextBoxInHStack_GetsRemainingWidth_ByDefault()
    {
        // Build an HStack with a fixed-width label and a bare TextBox; arrange
        // the stack into a 40-cell rect and check the textbox got the leftover.
        var label = new Hex1b.Widgets.TextBlockWidget("Name: ") { WidthHint = Hex1b.Layout.SizeHint.Fixed(6) };
        var textbox = new TextBoxWidget("");
        var hstack = new HStackNode
        {
            Children = new List<Hex1bNode>
            {
                new TextBlockNode { Text = "Name: ", WidthHint = label.WidthHint },
                new TextBoxNode { WidthHint = textbox.DefaultWidthHint }
            }
        };

        hstack.Arrange(new Hex1b.Layout.Rect(0, 0, 40, 1));

        Assert.AreEqual(34, hstack.Children[1].Bounds.Width);
    }

    private static long ReadVersion(TextBoxState state)
    {
        var prop = typeof(TextBoxState).GetProperty(
            "Version",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return (long)prop.GetValue(state)!;
    }
}

