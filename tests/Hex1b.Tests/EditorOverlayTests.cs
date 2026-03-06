using Hex1b.Automation;
using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the editor overlay system — push/dismiss, dismiss-on-cursor-move,
/// coordinate mapping, and lifecycle management.
/// </summary>
public class EditorOverlayTests
{
    /// <summary>
    /// Decoration provider that records Activate/Deactivate calls and
    /// can push/dismiss overlays via the session.
    /// </summary>
    private sealed class OverlayTestProvider : ITextDecorationProvider
    {
        public IEditorSession? Session { get; private set; }
        public bool WasActivated { get; private set; }
        public bool WasDeactivated { get; private set; }

        public void Activate(IEditorSession session)
        {
            Session = session;
            WasActivated = true;
        }

        public void Deactivate()
        {
            Session = null;
            WasDeactivated = true;
        }

        public IReadOnlyList<TextDecorationSpan> GetDecorations(int startLine, int endLine, IHex1bDocument document)
            => [];
    }

    private static (EditorNode node, Hex1bAppWorkloadAdapter workload, Hex1bRenderContext context) CreateEditor(
        string text, int width = 40, int height = 10)
    {
        var doc = new Hex1bDocument(text);
        var state = new EditorState(doc);
        var node = new EditorNode { State = state, IsFocused = true };

        var theme = Hex1bThemes.Default;
        var workload = new Hex1bAppWorkloadAdapter();
        var context = new Hex1bRenderContext(workload, theme);

        node.Measure(new Constraints(0, width, 0, height));
        node.Arrange(new Rect(0, 0, width, height));

        return (node, workload, context);
    }

    [Fact]
    public void Activate_CalledWhenProviderSetViaReconcile()
    {
        // Arrange
        var provider = new OverlayTestProvider();
        var doc = new Hex1bDocument("hello");
        var state = new EditorState(doc);
        var node = new EditorNode { State = state, IsFocused = true };

        // Act — simulate what EditorWidget.ReconcileAsync does
        node.UpdateDecorationProviders([provider]);

        // Assert
        Assert.True(provider.WasActivated);
        Assert.NotNull(provider.Session);
    }

    [Fact]
    public void Deactivate_CalledWhenProviderRemoved()
    {
        // Arrange
        var provider = new OverlayTestProvider();
        var (node, _, _) = CreateEditor("hello");
        node.UpdateDecorationProviders([provider]);

        // Act — remove the provider
        node.UpdateDecorationProviders(null);

        // Assert
        Assert.True(provider.WasDeactivated);
    }

    [Fact]
    public void PushOverlay_AddsToActiveOverlays()
    {
        // Arrange
        var provider = new OverlayTestProvider();
        var (node, _, _) = CreateEditor("hello world");
        node.UpdateDecorationProviders([provider]);

        var overlay = new EditorOverlay(
            Id: "test-overlay",
            AnchorPosition: new DocumentPosition(1, 1),
            Placement: OverlayPlacement.Below,
            Content: [new OverlayLine("test content")]);

        // Act
        provider.Session!.PushOverlay(overlay);

        // Assert
        Assert.Single(provider.Session.ActiveOverlays);
        Assert.Equal("test-overlay", provider.Session.ActiveOverlays[0].Id);
    }

    [Fact]
    public void PushOverlay_ReplacesSameId()
    {
        // Arrange
        var provider = new OverlayTestProvider();
        var (node, _, _) = CreateEditor("hello world");
        node.UpdateDecorationProviders([provider]);

        var overlay1 = new EditorOverlay("test", new DocumentPosition(1, 1),
            OverlayPlacement.Below, [new OverlayLine("first")]);
        var overlay2 = new EditorOverlay("test", new DocumentPosition(1, 5),
            OverlayPlacement.Above, [new OverlayLine("second")]);

        // Act
        provider.Session!.PushOverlay(overlay1);
        provider.Session.PushOverlay(overlay2);

        // Assert
        Assert.Single(provider.Session.ActiveOverlays);
        Assert.Equal("second", provider.Session.ActiveOverlays[0].Content[0].Text);
        Assert.Equal(OverlayPlacement.Above, provider.Session.ActiveOverlays[0].Placement);
    }

    [Fact]
    public void DismissOverlay_RemovesById()
    {
        // Arrange
        var provider = new OverlayTestProvider();
        var (node, _, _) = CreateEditor("hello world");
        node.UpdateDecorationProviders([provider]);

        provider.Session!.PushOverlay(new EditorOverlay("a", new DocumentPosition(1, 1),
            OverlayPlacement.Below, [new OverlayLine("alpha")]));
        provider.Session.PushOverlay(new EditorOverlay("b", new DocumentPosition(1, 5),
            OverlayPlacement.Below, [new OverlayLine("beta")]));

        // Act
        provider.Session.DismissOverlay("a");

        // Assert
        Assert.Single(provider.Session.ActiveOverlays);
        Assert.Equal("b", provider.Session.ActiveOverlays[0].Id);
    }

    [Fact]
    public void DismissOnCursorMove_RemovesOverlayWhenCursorMoves()
    {
        // Arrange
        var provider = new OverlayTestProvider();
        var (node, _, _) = CreateEditor("hello world");
        node.UpdateDecorationProviders([provider]);

        provider.Session!.PushOverlay(new EditorOverlay("hover", new DocumentPosition(1, 1),
            OverlayPlacement.Below, [new OverlayLine("hover info")],
            DismissOnCursorMove: true));

        Assert.Single(provider.Session.ActiveOverlays);

        // Act — simulate cursor move
        node.NotifyCursorChanged();

        // Assert
        Assert.Empty(provider.Session.ActiveOverlays);
    }

    [Fact]
    public void DismissOnCursorMove_False_PreservesOverlayWhenCursorMoves()
    {
        // Arrange
        var provider = new OverlayTestProvider();
        var (node, _, _) = CreateEditor("hello world");
        node.UpdateDecorationProviders([provider]);

        provider.Session!.PushOverlay(new EditorOverlay("sticky", new DocumentPosition(1, 1),
            OverlayPlacement.Below, [new OverlayLine("persistent")],
            DismissOnCursorMove: false));

        // Act
        node.NotifyCursorChanged();

        // Assert — overlay should survive
        Assert.Single(provider.Session.ActiveOverlays);
        Assert.Equal("sticky", provider.Session.ActiveOverlays[0].Id);
    }

    [Fact]
    public void Render_WithOverlay_DoesNotCrash()
    {
        // Arrange — ensure rendering with an active overlay doesn't throw
        var provider = new OverlayTestProvider();
        var (node, workload, context) = CreateEditor("hello world\nsecond line\nthird line");
        node.UpdateDecorationProviders([provider]);

        provider.Session!.PushOverlay(new EditorOverlay("info", new DocumentPosition(1, 1),
            OverlayPlacement.Below,
            [
                new OverlayLine("Hover Title", Hex1bColor.FromRgb(255, 255, 0)),
                new OverlayLine("Description text"),
            ]));

        // Act & Assert — should not throw
        node.Render(context);
    }

    [Fact]
    public void MultipleProviders_IndependentOverlays()
    {
        // Arrange
        var provider1 = new OverlayTestProvider();
        var provider2 = new OverlayTestProvider();
        var (node, _, _) = CreateEditor("hello world");
        node.UpdateDecorationProviders([provider1, provider2]);

        // Act — each provider pushes its own overlay
        provider1.Session!.PushOverlay(new EditorOverlay("p1-overlay", new DocumentPosition(1, 1),
            OverlayPlacement.Below, [new OverlayLine("from provider 1")]));
        provider2.Session!.PushOverlay(new EditorOverlay("p2-overlay", new DocumentPosition(1, 5),
            OverlayPlacement.Above, [new OverlayLine("from provider 2")]));

        // Assert — both overlays exist on the same session
        Assert.Equal(2, provider1.Session.ActiveOverlays.Count);
        Assert.Equal(2, provider2.Session.ActiveOverlays.Count);
    }
}
