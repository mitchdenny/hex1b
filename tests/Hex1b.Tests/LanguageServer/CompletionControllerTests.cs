using Hex1b.Documents;
using Hex1b.LanguageServer;
using Hex1b.LanguageServer.Protocol;
using Hex1b.Widgets;

namespace Hex1b.Tests.LanguageServer;

[TestClass]
public class CompletionControllerTests
{
    private readonly CompletionController _controller = new();
    private readonly TestEditorSession _session;

    public CompletionControllerTests()
    {
        var doc = new Hex1bDocument("hello world");
        var state = new EditorState(doc);
        _session = new TestEditorSession(state);
        _controller.Attach(_session);
    }

    [TestMethod]
    public void Show_WithItems_IsActive()
    {
        var items = MakeItems("foo", "bar", "baz");
        _controller.Show(items, new DocumentPosition(1, 5));

        Assert.IsTrue(_controller.IsActive);
        TestSeq.Single(_session.Overlays);
        Assert.AreEqual(CompletionController.CompletionOverlayId, _session.Overlays[0].Id);
    }

    [TestMethod]
    public void Show_WithEmptyItems_IsNotActive()
    {
        _controller.Show([], new DocumentPosition(1, 5));

        Assert.IsFalse(_controller.IsActive);
        Assert.IsEmpty(_session.Overlays);
    }

    [TestMethod]
    public void SelectNext_CyclesForward()
    {
        var items = MakeItems("foo", "bar", "baz");
        _controller.Show(items, new DocumentPosition(1, 5));

        // Initially first item is selected (index 0)
        _controller.SelectNext(); // index 1
        _controller.SelectNext(); // index 2
        _controller.SelectNext(); // wraps to 0

        Assert.IsTrue(_controller.IsActive);
        TestSeq.Single(_session.Overlays); // Still one overlay
    }

    [TestMethod]
    public void SelectPrev_CyclesBackward()
    {
        var items = MakeItems("foo", "bar", "baz");
        _controller.Show(items, new DocumentPosition(1, 5));

        _controller.SelectPrev(); // wraps to index 2

        Assert.IsTrue(_controller.IsActive);
    }

    [TestMethod]
    public void Accept_ReturnsSelectedLabel()
    {
        var items = MakeItems("foo", "bar", "baz");
        _controller.Show(items, new DocumentPosition(1, 5));

        _controller.SelectNext(); // select "bar"
        var text = _controller.Accept();

        Assert.AreEqual("bar", text);
        Assert.IsFalse(_controller.IsActive);
    }

    [TestMethod]
    public void Accept_ReturnsInsertText_WhenAvailable()
    {
        var items = new[]
        {
            new CompletionItem { Label = "display", InsertText = "insertMe" }
        };
        _controller.Show(items, new DocumentPosition(1, 1));

        var text = _controller.Accept();

        Assert.AreEqual("insertMe", text);
    }

    [TestMethod]
    public void Filter_NarrowsItems()
    {
        var items = MakeItems("forEach", "find", "filter", "map", "reduce");
        _controller.Show(items, new DocumentPosition(1, 5));
        Assert.IsTrue(_controller.IsActive);

        _controller.Filter("fi");

        Assert.IsTrue(_controller.IsActive);
        var overlay = _session.Overlays[0];
        Assert.AreEqual(2, overlay.Content.Count); // "find" and "filter"
    }

    [TestMethod]
    public void Filter_DismissesWhenNoMatch()
    {
        var items = MakeItems("forEach", "find", "filter");
        _controller.Show(items, new DocumentPosition(1, 5));

        _controller.Filter("xyz");

        Assert.IsFalse(_controller.IsActive);
        Assert.IsEmpty(_session.Overlays);
    }

    [TestMethod]
    public void Filter_IsCaseInsensitive()
    {
        var items = MakeItems("forEach", "Find", "FILTER");
        _controller.Show(items, new DocumentPosition(1, 5));

        _controller.Filter("f");

        Assert.IsTrue(_controller.IsActive);
        var overlay = _session.Overlays[0];
        Assert.AreEqual(3, overlay.Content.Count); // All match case-insensitively
    }

    [TestMethod]
    public void Accept_AfterFilter_StripsPrefix()
    {
        var items = MakeItems("forEach", "find", "filter");
        _controller.Show(items, new DocumentPosition(1, 5));

        _controller.Filter("fi");
        var text = _controller.Accept();

        // "find" minus the "fi" prefix already typed
        Assert.AreEqual("nd", text);
    }

    [TestMethod]
    public void Filter_UsesFilterTextWhenAvailable()
    {
        var items = new[]
        {
            new CompletionItem { Label = "display()", FilterText = "display" },
            new CompletionItem { Label = "dispose()", FilterText = "dispose" },
            new CompletionItem { Label = "map()", FilterText = "map" },
        };
        _controller.Show(items, new DocumentPosition(1, 1));

        _controller.Filter("dis");

        Assert.IsTrue(_controller.IsActive);
        var overlay = _session.Overlays[0];
        Assert.AreEqual(2, overlay.Content.Count); // display() and dispose()
    }

    [TestMethod]
    public void Filter_ContainsFallback_MatchesSubstring()
    {
        var items = MakeItems("groupCollapsed", "groupEnd", "log", "warn");
        _controller.Show(items, new DocumentPosition(1, 5));

        _controller.Filter("Coll");

        Assert.IsTrue(_controller.IsActive);
        var overlay = _session.Overlays[0];
        TestSeq.Single(overlay.Content); // "groupCollapsed" via Contains
    }

    [TestMethod]
    public void Dismiss_ClearsOverlay()
    {
        var items = MakeItems("foo", "bar");
        _controller.Show(items, new DocumentPosition(1, 5));
        Assert.IsTrue(_controller.IsActive);

        _controller.Dismiss();

        Assert.IsFalse(_controller.IsActive);
        Assert.IsEmpty(_session.Overlays);
    }

    [TestMethod]
    public void Overlay_HasCorrectAnchor()
    {
        var items = MakeItems("method1");
        var anchor = new DocumentPosition(5, 10);
        _controller.Show(items, anchor);

        TestSeq.Single(_session.Overlays);
        Assert.AreEqual(anchor, _session.Overlays[0].AnchorPosition);
        Assert.AreEqual(OverlayPlacement.Below, _session.Overlays[0].Placement);
    }

    [TestMethod]
    public void Overlay_DoesNotDismissOnCursorMove()
    {
        var items = MakeItems("foo");
        _controller.Show(items, new DocumentPosition(1, 1));

        // The completion overlay should NOT auto-dismiss on cursor move
        // (the controller manages its own lifecycle)
        Assert.IsFalse(_session.Overlays[0].DismissOnCursorMove);
    }

    [TestMethod]
    public void Overlay_ShowsKindIcons()
    {
        var items = new[]
        {
            new CompletionItem { Label = "myMethod", Kind = CompletionItemKind.Method },
            new CompletionItem { Label = "MyClass", Kind = CompletionItemKind.Class },
            new CompletionItem { Label = "myProp", Kind = CompletionItemKind.Property },
        };
        _controller.Show(items, new DocumentPosition(1, 1));

        var overlay = _session.Overlays[0];
        Assert.AreEqual(3, overlay.Content.Count);
        Assert.Contains("ƒ", overlay.Content[0].Text); // Method icon
        Assert.Contains("C", overlay.Content[1].Text);  // Class icon
        Assert.Contains("P", overlay.Content[2].Text);  // Property icon
    }

    private static CompletionItem[] MakeItems(params string[] labels)
        => labels.Select(l => new CompletionItem { Label = l }).ToArray();

    private sealed class TestEditorSession : IEditorSession
    {
        public TestEditorSession(EditorState state) => State = state;
        public EditorState State { get; }
        public TerminalCapabilities Capabilities => TerminalCapabilities.Modern;
        public void Invalidate() { }
        public List<EditorOverlay> Overlays { get; } = [];

        public void PushOverlay(EditorOverlay overlay)
        {
            Overlays.RemoveAll(o => o.Id == overlay.Id);
            Overlays.Add(overlay);
        }

        public void DismissOverlay(string id)
        {
            Overlays.RemoveAll(o => o.Id == id);
        }

        public IReadOnlyList<EditorOverlay> ActiveOverlays => Overlays;

        public void PushInlineHints(IReadOnlyList<InlineHint> hints) { }
        public void ClearInlineHints() { }
        public IReadOnlyList<InlineHint> ActiveInlineHints => [];
        public void PushRangeHighlights(IReadOnlyList<RangeHighlight> highlights) { }
        public void ClearRangeHighlights() { }
        public IReadOnlyList<RangeHighlight> ActiveRangeHighlights => [];
        public void PushGutterDecorations(IReadOnlyList<GutterDecoration> decorations) { }
        public void ClearGutterDecorations() { }
        public IReadOnlyList<GutterDecoration> ActiveGutterDecorations => [];
        public void SetFoldingRegions(IReadOnlyList<FoldingRegion> regions) { }
        public IReadOnlyList<FoldingRegion> FoldingRegions => [];
        public void SetBreadcrumbs(BreadcrumbData? data) { }
        public BreadcrumbData? Breadcrumbs => null;
        public Task<string?> ShowActionMenuAsync(ActionMenu menu) => Task.FromResult<string?>(null);
        public void ShowSignaturePanel(SignaturePanel panel) { }
        public void DismissSignaturePanel() { }
    }
}
