using Hex1b.Documents;
using Hex1b.LanguageServer;
using Hex1b.LanguageServer.Protocol;
using Hex1b.Widgets;

namespace Hex1b.Tests.LanguageServer;

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

    [Fact]
    public void Show_WithItems_IsActive()
    {
        var items = MakeItems("foo", "bar", "baz");
        _controller.Show(items, new DocumentPosition(1, 5));

        Assert.True(_controller.IsActive);
        Assert.Single(_session.Overlays);
        Assert.Equal(CompletionController.CompletionOverlayId, _session.Overlays[0].Id);
    }

    [Fact]
    public void Show_WithEmptyItems_IsNotActive()
    {
        _controller.Show([], new DocumentPosition(1, 5));

        Assert.False(_controller.IsActive);
        Assert.Empty(_session.Overlays);
    }

    [Fact]
    public void SelectNext_CyclesForward()
    {
        var items = MakeItems("foo", "bar", "baz");
        _controller.Show(items, new DocumentPosition(1, 5));

        // Initially first item is selected (index 0)
        _controller.SelectNext(); // index 1
        _controller.SelectNext(); // index 2
        _controller.SelectNext(); // wraps to 0

        Assert.True(_controller.IsActive);
        Assert.Single(_session.Overlays); // Still one overlay
    }

    [Fact]
    public void SelectPrev_CyclesBackward()
    {
        var items = MakeItems("foo", "bar", "baz");
        _controller.Show(items, new DocumentPosition(1, 5));

        _controller.SelectPrev(); // wraps to index 2

        Assert.True(_controller.IsActive);
    }

    [Fact]
    public void Accept_ReturnsSelectedLabel()
    {
        var items = MakeItems("foo", "bar", "baz");
        _controller.Show(items, new DocumentPosition(1, 5));

        _controller.SelectNext(); // select "bar"
        var text = _controller.Accept();

        Assert.Equal("bar", text);
        Assert.False(_controller.IsActive);
    }

    [Fact]
    public void Accept_ReturnsInsertText_WhenAvailable()
    {
        var items = new[]
        {
            new CompletionItem { Label = "display", InsertText = "insertMe" }
        };
        _controller.Show(items, new DocumentPosition(1, 1));

        var text = _controller.Accept();

        Assert.Equal("insertMe", text);
    }

    [Fact]
    public void Dismiss_ClearsOverlay()
    {
        var items = MakeItems("foo", "bar");
        _controller.Show(items, new DocumentPosition(1, 5));
        Assert.True(_controller.IsActive);

        _controller.Dismiss();

        Assert.False(_controller.IsActive);
        Assert.Empty(_session.Overlays);
    }

    [Fact]
    public void Overlay_HasCorrectAnchor()
    {
        var items = MakeItems("method1");
        var anchor = new DocumentPosition(5, 10);
        _controller.Show(items, anchor);

        Assert.Single(_session.Overlays);
        Assert.Equal(anchor, _session.Overlays[0].AnchorPosition);
        Assert.Equal(OverlayPlacement.Below, _session.Overlays[0].Placement);
    }

    [Fact]
    public void Overlay_DoesNotDismissOnCursorMove()
    {
        var items = MakeItems("foo");
        _controller.Show(items, new DocumentPosition(1, 1));

        // The completion overlay should NOT auto-dismiss on cursor move
        // (the controller manages its own lifecycle)
        Assert.False(_session.Overlays[0].DismissOnCursorMove);
    }

    [Fact]
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
        Assert.Equal(3, overlay.Content.Count);
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
    }
}
