using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for PastableWidget/PastableNode container that intercepts paste events.
/// </summary>
[TestClass]
public class PastableWidgetTests
{
    /// <summary>
    /// Focusable leaf node that optionally handles paste.
    /// </summary>
    private sealed class TestFocusableNode : Hex1bNode
    {
        public override bool IsFocusable => true;
        private bool _isFocused;
        public override bool IsFocused { get => _isFocused; set => _isFocused = value; }
        public bool ShouldHandlePaste { get; set; }
        public List<Hex1bPasteEvent> ReceivedPastes { get; } = new();

        protected override Size MeasureCore(Constraints constraints) => new(10, 1);
        public override void Render(Hex1bRenderContext context) { }

        public override Task<InputResult> HandlePasteAsync(Hex1bPasteEvent pasteEvent)
        {
            ReceivedPastes.Add(pasteEvent);
            return Task.FromResult(ShouldHandlePaste ? InputResult.Handled : InputResult.NotHandled);
        }
    }

    private static PasteContext CreatePaste(string text)
    {
        var ctx = new PasteContext();
        ctx.TryWrite(text);
        ctx.Complete();
        return ctx;
    }

    [TestMethod]
    public async Task Pastable_InterceptsPaste()
    {
        // Focused child doesn't handle paste → PastableNode receives it
        var child = new TestFocusableNode { IsFocused = true, ShouldHandlePaste = false };
        string? receivedText = null;
        var node = new PastableNode
        {
            Child = child,
            PasteAction = async e => { receivedText = await e.Paste.ReadToEndAsync(); }
        };
        child.Parent = node;

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        focusRing.EnsureFocus();
        var paste = CreatePaste("intercepted");

        var result = await InputRouter.RouteInputAsync(
            node, new Hex1bPasteEvent(paste), focusRing, new InputRouterState());

        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual("intercepted", receivedText);
        await paste.DisposeAsync();
    }

    [TestMethod]
    public async Task Pastable_ChildHandlesFirst()
    {
        // Focused child handles paste → PastableNode NOT called
        var child = new TestFocusableNode { IsFocused = true, ShouldHandlePaste = true };
        bool pastableWasCalled = false;
        var node = new PastableNode
        {
            Child = child,
            PasteAction = _ => { pastableWasCalled = true; return Task.CompletedTask; }
        };
        child.Parent = node;

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        focusRing.EnsureFocus();
        var paste = CreatePaste("child handles");

        await InputRouter.RouteInputAsync(
            node, new Hex1bPasteEvent(paste), focusRing, new InputRouterState());

        Assert.IsFalse(pastableWasCalled);
        TestSeq.Single(child.ReceivedPastes);
        await paste.DisposeAsync();
    }

    [TestMethod]
    public async Task Pastable_OnPasteCallback()
    {
        var child = new TestFocusableNode { IsFocused = true, ShouldHandlePaste = false };
        string? receivedText = null;
        var node = new PastableNode
        {
            Child = child,
            PasteAction = async e => { receivedText = await e.Paste.ReadToEndAsync(); }
        };
        child.Parent = node;

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        focusRing.EnsureFocus();
        var paste = CreatePaste("callback data");

        await InputRouter.RouteInputAsync(
            node, new Hex1bPasteEvent(paste), focusRing, new InputRouterState());

        Assert.AreEqual("callback data", receivedText);
        await paste.DisposeAsync();
    }

    [TestMethod]
    public async Task Pastable_ReadChunks_MultipleChunks()
    {
        var child = new TestFocusableNode { IsFocused = true, ShouldHandlePaste = false };
        var chunks = new List<string>();
        var node = new PastableNode
        {
            Child = child,
            PasteAction = async e =>
            {
                await foreach (var chunk in e.Paste.ReadChunksAsync())
                    chunks.Add(chunk);
            }
        };
        child.Parent = node;

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        focusRing.EnsureFocus();

        var paste = new PasteContext();
        paste.TryWrite("chunk1");
        paste.TryWrite("chunk2");
        paste.TryWrite("chunk3");
        paste.Complete();

        await InputRouter.RouteInputAsync(
            node, new Hex1bPasteEvent(paste), focusRing, new InputRouterState());

        Assert.AreEqual(3, chunks.Count);
        Assert.AreEqual("chunk1", chunks[0]);
        Assert.AreEqual("chunk2", chunks[1]);
        Assert.AreEqual("chunk3", chunks[2]);
        await paste.DisposeAsync();
    }

    [TestMethod]
    public async Task Pastable_ReadLines_StreamsLines()
    {
        var child = new TestFocusableNode { IsFocused = true, ShouldHandlePaste = false };
        var lines = new List<string>();
        var node = new PastableNode
        {
            Child = child,
            PasteAction = async e =>
            {
                await foreach (var line in e.Paste.ReadLinesAsync())
                    lines.Add(line);
            }
        };
        child.Parent = node;

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        focusRing.EnsureFocus();
        var paste = CreatePaste("line1\nline2\nline3");

        await InputRouter.RouteInputAsync(
            node, new Hex1bPasteEvent(paste), focusRing, new InputRouterState());

        Assert.AreEqual(3, lines.Count);
        Assert.AreEqual("line1", lines[0]);
        Assert.AreEqual("line2", lines[1]);
        Assert.AreEqual("line3", lines[2]);
        await paste.DisposeAsync();
    }

    [TestMethod]
    public async Task Pastable_NoPasteHandler_NotHandled()
    {
        var child = new TestFocusableNode { IsFocused = true, ShouldHandlePaste = false };
        var node = new PastableNode
        {
            Child = child,
            PasteAction = null // no handler
        };
        child.Parent = node;

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        focusRing.EnsureFocus();
        var paste = CreatePaste("ignored");

        var result = await InputRouter.RouteInputAsync(
            node, new Hex1bPasteEvent(paste), focusRing, new InputRouterState());

        Assert.AreEqual(InputResult.NotHandled, result);
        await paste.DisposeAsync();
    }

    [TestMethod]
    public async Task Pastable_NestedPastable_InnerTakesPrecedence()
    {
        // root (outer Pastable) -> inner (inner Pastable) -> focused child
        var child = new TestFocusableNode { IsFocused = true, ShouldHandlePaste = false };
        string? innerReceived = null;
        bool outerCalled = false;

        var inner = new PastableNode
        {
            Child = child,
            PasteAction = async e => { innerReceived = await e.Paste.ReadToEndAsync(); }
        };
        child.Parent = inner;

        var outer = new PastableNode
        {
            Child = inner,
            PasteAction = _ => { outerCalled = true; return Task.CompletedTask; }
        };
        inner.Parent = outer;

        var focusRing = new FocusRing();
        focusRing.Rebuild(outer);
        focusRing.EnsureFocus();
        var paste = CreatePaste("inner wins");

        await InputRouter.RouteInputAsync(
            outer, new Hex1bPasteEvent(paste), focusRing, new InputRouterState());

        Assert.AreEqual("inner wins", innerReceived);
        Assert.IsFalse(outerCalled);
        await paste.DisposeAsync();
    }

    [TestMethod]
    public async Task Pastable_MaxSize_CancelsPaste()
    {
        var child = new TestFocusableNode { IsFocused = true, ShouldHandlePaste = false };
        bool wasCancelled = false;
        var node = new PastableNode
        {
            Child = child,
            MaxSize = 10,
            PasteAction = async e =>
            {
                try
                {
                    await e.Paste.ReadToEndAsync(maxCharacters: int.MaxValue);
                }
                catch
                {
                    // ReadToEndAsync may throw or return partial
                }
                wasCancelled = e.Paste.IsCancelled;
            }
        };
        child.Parent = node;

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        focusRing.EnsureFocus();

        // Create paste with more data than MaxSize, but write slowly so enforcement can poll
        var paste = new PasteContext();
        paste.TryWrite(new string('x', 5));

        // Start routing in background
        var routeTask = InputRouter.RouteInputAsync(
            node, new Hex1bPasteEvent(paste), focusRing, new InputRouterState());

        // Write more data to exceed MaxSize — the enforcement polls TotalCharactersWritten
        await Task.Delay(100);
        paste.TryWrite(new string('y', 10)); // total now 15 > 10

        // Wait for enforcement to notice
        await Task.Delay(200);
        paste.Complete();

        await routeTask;

        Assert.IsTrue(wasCancelled);
        await paste.DisposeAsync();
    }

    [TestMethod]
    public async Task Pastable_Timeout_CancelsPaste()
    {
        var child = new TestFocusableNode { IsFocused = true, ShouldHandlePaste = false };
        bool wasCancelled = false;
        var node = new PastableNode
        {
            Child = child,
            PasteTimeout = TimeSpan.FromMilliseconds(100),
            PasteAction = async e =>
            {
                // Handler that takes a long time (simulate slow processing)
                try
                {
                    await foreach (var chunk in e.Paste.ReadChunksAsync())
                    {
                        // keep reading
                    }
                }
                catch
                {
                    // cancelled
                }
                wasCancelled = e.Paste.IsCancelled;
            }
        };
        child.Parent = node;

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        focusRing.EnsureFocus();

        // Create a paste that doesn't complete before timeout
        var paste = new PasteContext();
        paste.TryWrite("start");

        var routeTask = InputRouter.RouteInputAsync(
            node, new Hex1bPasteEvent(paste), focusRing, new InputRouterState());

        // Don't complete the paste — let timeout fire
        await routeTask;

        Assert.IsTrue(wasCancelled);
        await paste.DisposeAsync();
    }

    [TestMethod]
    public async Task Pastable_CancelDuringStream()
    {
        var child = new TestFocusableNode { IsFocused = true, ShouldHandlePaste = false };
        var chunksRead = new List<string>();
        var node = new PastableNode
        {
            Child = child,
            PasteAction = async e =>
            {
                await foreach (var chunk in e.Paste.ReadChunksAsync())
                {
                    chunksRead.Add(chunk);
                    if (chunksRead.Count >= 2)
                        e.Paste.Cancel();
                }
            }
        };
        child.Parent = node;

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        focusRing.EnsureFocus();

        var paste = new PasteContext();
        paste.TryWrite("chunk1");
        paste.TryWrite("chunk2");
        // Don't write more chunks or call Complete() before handler runs
        // so that Cancel() actually fires before Complete()

        var routeTask = InputRouter.RouteInputAsync(
            node, new Hex1bPasteEvent(paste), focusRing, new InputRouterState());

        // Give handler time to read and cancel
        await Task.Delay(100);

        // Write more data — this should be ignored since handler cancelled
        paste.TryWrite("chunk3"); // may or may not succeed depending on cancel timing
        paste.Complete();

        await routeTask;

        Assert.IsTrue(paste.IsCancelled);
        Assert.IsTrue(chunksRead.Count >= 2);
        Assert.AreEqual("chunk1", chunksRead[0]);
        Assert.AreEqual("chunk2", chunksRead[1]);
        await paste.DisposeAsync();
    }

    [TestMethod]
    public async Task Pastable_TotalCharactersWritten_Tracked()
    {
        var paste = new PasteContext();
        Assert.AreEqual(0, paste.TotalCharactersWritten);

        paste.TryWrite("hello");
        Assert.AreEqual(5, paste.TotalCharactersWritten);

        paste.TryWrite(" world");
        Assert.AreEqual(11, paste.TotalCharactersWritten);

        paste.Complete();
        await paste.DisposeAsync();
    }

    [TestMethod]
    public async Task Pastable_Widget_ReconcilesToNode()
    {
        // Verify the widget creates the correct node type
        var childWidget = new TextBlockWidget("test");
        var widget = new PastableWidget(childWidget)
            .OnPaste(async e => await e.Paste.ReadToEndAsync())
            .MaxSize(1000)
            .Timeout(TimeSpan.FromSeconds(30));

        Assert.IsNotNull(widget.PasteHandler);
        Assert.AreEqual(1000, widget.MaxPasteSize);
        Assert.AreEqual(TimeSpan.FromSeconds(30), widget.PasteTimeout);
    }

    [TestMethod]
    public async Task Pastable_Widget_FluentApi()
    {
        // Verify fluent API methods return correct widget type
        var widget = new PastableWidget(new TextBlockWidget("test"));
        
        var withPaste = widget.OnPaste(e => { });
        Assert.IsNotNull(withPaste.PasteHandler);

        var withMax = widget.MaxSize(500);
        Assert.AreEqual(500, withMax.MaxPasteSize);

        var withTimeout = widget.Timeout(TimeSpan.FromMinutes(1));
        Assert.AreEqual(TimeSpan.FromMinutes(1), withTimeout.PasteTimeout);
    }
}
