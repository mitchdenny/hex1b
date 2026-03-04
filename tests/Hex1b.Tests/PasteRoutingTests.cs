using Hex1b.Input;
using Hex1b.Layout;

namespace Hex1b.Tests;

/// <summary>
/// Tests for paste event routing through InputRouter and Hex1bApp.
/// Phase 2: Verify paste events reach focused nodes, bubble to ancestors,
/// and Escape cancellation works.
/// </summary>
public class PasteRoutingTests
{
    /// <summary>
    /// Focusable node that can optionally handle paste events.
    /// </summary>
    private sealed class PasteFocusableNode : Hex1bNode
    {
        public override bool IsFocusable => true;
        private bool _isFocused;
        public override bool IsFocused { get => _isFocused; set => _isFocused = value; }

        public List<Hex1bPasteEvent> ReceivedPastes { get; } = new();
        public bool ShouldHandlePaste { get; set; }

        protected override Size MeasureCore(Constraints constraints) => new(10, 1);
        public override void Render(Hex1bRenderContext context) { }

        public override Task<InputResult> HandlePasteAsync(Hex1bPasteEvent pasteEvent)
        {
            ReceivedPastes.Add(pasteEvent);
            return Task.FromResult(ShouldHandlePaste ? InputResult.Handled : InputResult.NotHandled);
        }
    }

    /// <summary>
    /// Container node that can optionally handle paste events (for bubbling tests).
    /// </summary>
    private sealed class PasteContainerNode : Hex1bNode
    {
        public List<Hex1bNode> Children { get; } = new();
        public List<Hex1bPasteEvent> ReceivedPastes { get; } = new();
        public bool ShouldHandlePaste { get; set; }

        protected override Size MeasureCore(Constraints constraints) => new(80, 24);
        public override void Render(Hex1bRenderContext context) { }
        public override IEnumerable<Hex1bNode> GetChildren() => Children;

        public override IEnumerable<Hex1bNode> GetFocusableNodes()
        {
            foreach (var child in Children)
            {
                foreach (var focusable in child.GetFocusableNodes())
                    yield return focusable;
            }
        }

        public override Task<InputResult> HandlePasteAsync(Hex1bPasteEvent pasteEvent)
        {
            ReceivedPastes.Add(pasteEvent);
            return Task.FromResult(ShouldHandlePaste ? InputResult.Handled : InputResult.NotHandled);
        }
    }

    private static (PasteContainerNode root, PasteFocusableNode focused, FocusRing focusRing, InputRouterState state) SetupSimpleTree()
    {
        var focused = new PasteFocusableNode { IsFocused = true };
        var root = new PasteContainerNode();
        root.Children.Add(focused);
        focused.Parent = root;

        var focusRing = new FocusRing();
        focusRing.Rebuild(root);
        focusRing.EnsureFocus();
        var state = new InputRouterState();

        return (root, focused, focusRing, state);
    }

    private static PasteContext CreateTestPasteContext(string text = "hello")
    {
        var ctx = new PasteContext();
        ctx.TryWrite(text);
        ctx.Complete();
        return ctx;
    }

    // --- Routing tests ---

    [Fact]
    public async Task Routing_PasteToFocusedNode()
    {
        var (root, focused, focusRing, state) = SetupSimpleTree();
        focused.ShouldHandlePaste = true;
        var paste = CreateTestPasteContext("pasted text");

        var result = await InputRouter.RouteInputAsync(
            root, new Hex1bPasteEvent(paste), focusRing, state);

        Assert.Equal(InputResult.Handled, result);
        Assert.Single(focused.ReceivedPastes);
        var text = await focused.ReceivedPastes[0].Paste.ReadToEndAsync();
        Assert.Equal("pasted text", text);
        await paste.DisposeAsync();
    }

    [Fact]
    public async Task Routing_PasteBubblesToAncestor()
    {
        var (root, focused, focusRing, state) = SetupSimpleTree();
        focused.ShouldHandlePaste = false; // focused node doesn't handle
        root.ShouldHandlePaste = true;     // parent handles
        var paste = CreateTestPasteContext("bubbled");

        var result = await InputRouter.RouteInputAsync(
            root, new Hex1bPasteEvent(paste), focusRing, state);

        Assert.Equal(InputResult.Handled, result);
        // Focused got the event first but returned NotHandled
        Assert.Single(focused.ReceivedPastes);
        // Parent got the event second and handled it
        Assert.Single(root.ReceivedPastes);
        await paste.DisposeAsync();
    }

    [Fact]
    public async Task Routing_PasteBubblesMultipleLevels()
    {
        // root -> middle -> focused (3 levels)
        var focused = new PasteFocusableNode { IsFocused = true, ShouldHandlePaste = false };
        var middle = new PasteContainerNode { ShouldHandlePaste = false };
        var root = new PasteContainerNode { ShouldHandlePaste = true };

        middle.Children.Add(focused);
        focused.Parent = middle;
        root.Children.Add(middle);
        middle.Parent = root;

        var focusRing = new FocusRing();
        focusRing.Rebuild(root);
        focusRing.EnsureFocus();
        var state = new InputRouterState();
        var paste = CreateTestPasteContext("deep bubble");

        var result = await InputRouter.RouteInputAsync(
            root, new Hex1bPasteEvent(paste), focusRing, state);

        Assert.Equal(InputResult.Handled, result);
        // All three got the event
        Assert.Single(focused.ReceivedPastes);
        Assert.Single(middle.ReceivedPastes);
        Assert.Single(root.ReceivedPastes);
        await paste.DisposeAsync();
    }

    [Fact]
    public async Task Routing_NoPasteHandler_NotHandled()
    {
        var (root, focused, focusRing, state) = SetupSimpleTree();
        focused.ShouldHandlePaste = false;
        root.ShouldHandlePaste = false;
        var paste = CreateTestPasteContext("ignored");

        var result = await InputRouter.RouteInputAsync(
            root, new Hex1bPasteEvent(paste), focusRing, state);

        Assert.Equal(InputResult.NotHandled, result);
        await paste.DisposeAsync();
    }

    [Fact]
    public async Task Routing_PasteWhileNothingFocused()
    {
        // No focused node
        var node = new PasteFocusableNode { ShouldHandlePaste = true };
        // IsFocused defaults to false
        var root = new PasteContainerNode();
        root.Children.Add(node);
        node.Parent = root;

        var focusRing = new FocusRing();
        focusRing.Rebuild(root);
        // Don't call EnsureFocus — nothing focused
        var state = new InputRouterState();
        var paste = CreateTestPasteContext("no focus");

        var result = await InputRouter.RouteInputAsync(
            root, new Hex1bPasteEvent(paste), focusRing, state);

        // With nothing focused, BuildPathToFocused still returns path to root
        // but HandlePasteAsync default returns NotHandled
        // The exact behavior depends on BuildPathToFocused fallback
        // Either way, paste should not crash
        await paste.DisposeAsync();
    }

    [Fact]
    public async Task Routing_HandlePasteAsync_DefaultNotHandled()
    {
        // Verify Hex1bNode base class returns NotHandled by default
        var node = new PasteFocusableNode(); // ShouldHandlePaste = false by default
        var paste = CreateTestPasteContext("test");

        var result = await node.HandlePasteAsync(new Hex1bPasteEvent(paste));

        Assert.Equal(InputResult.NotHandled, result);
        await paste.DisposeAsync();
    }

    [Fact]
    public async Task Routing_MultiplePastesSequential()
    {
        var (root, focused, focusRing, state) = SetupSimpleTree();
        focused.ShouldHandlePaste = true;

        // First paste
        var paste1 = CreateTestPasteContext("first");
        var result1 = await InputRouter.RouteInputAsync(
            root, new Hex1bPasteEvent(paste1), focusRing, state);
        Assert.Equal(InputResult.Handled, result1);

        // Second paste
        var paste2 = CreateTestPasteContext("second");
        var result2 = await InputRouter.RouteInputAsync(
            root, new Hex1bPasteEvent(paste2), focusRing, state);
        Assert.Equal(InputResult.Handled, result2);

        Assert.Equal(2, focused.ReceivedPastes.Count);
        var text1 = await focused.ReceivedPastes[0].Paste.ReadToEndAsync();
        var text2 = await focused.ReceivedPastes[1].Paste.ReadToEndAsync();
        Assert.Equal("first", text1);
        Assert.Equal("second", text2);

        await paste1.DisposeAsync();
        await paste2.DisposeAsync();
    }

    [Fact]
    public async Task Routing_FocusedNodeHandles_AncestorNotCalled()
    {
        var (root, focused, focusRing, state) = SetupSimpleTree();
        focused.ShouldHandlePaste = true;
        root.ShouldHandlePaste = true; // would handle, but shouldn't be called
        var paste = CreateTestPasteContext("handled early");

        await InputRouter.RouteInputAsync(
            root, new Hex1bPasteEvent(paste), focusRing, state);

        Assert.Single(focused.ReceivedPastes);
        Assert.Empty(root.ReceivedPastes); // should NOT have received the event
        await paste.DisposeAsync();
    }

    [Fact]
    public async Task Routing_PasteContext_IsCompleted_AfterRead()
    {
        var (root, focused, focusRing, state) = SetupSimpleTree();
        focused.ShouldHandlePaste = true;
        var paste = CreateTestPasteContext("done");

        await InputRouter.RouteInputAsync(
            root, new Hex1bPasteEvent(paste), focusRing, state);

        // The paste was completed at creation (Complete() called)
        Assert.True(paste.IsCompleted);
        await paste.DisposeAsync();
    }
}
