using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Terminal.Testing;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Unit tests for ThemePanelNode layout, rendering, and theme application.
/// </summary>
public class ThemePanelNodeTests
{
    private static Hex1bRenderContext CreateContext(IHex1bAppTerminalWorkloadAdapter workload, Hex1bTheme? theme = null)
    {
        return new Hex1bRenderContext(workload, theme);
    }

    #region Measure Tests

    [Fact]
    public void Measure_ReturnsChildSize()
    {
        var child = new TextBlockNode { Text = "Hello World" };
        var node = new ThemePanelNode 
        { 
            Child = child,
            ThemeMutator = t => t 
        };

        var size = node.Measure(Constraints.Unbounded);

        // ThemePanel doesn't add any size - just passes through child size
        Assert.Equal(11, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_WithNoChild_ReturnsZero()
    {
        var node = new ThemePanelNode { Child = null };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
    }

    [Fact]
    public void Measure_RespectsConstraints()
    {
        var child = new TextBlockNode { Text = "This is a long text that should be constrained" };
        var node = new ThemePanelNode 
        { 
            Child = child,
            ThemeMutator = t => t
        };

        var size = node.Measure(new Constraints(0, 10, 0, 5));

        Assert.True(size.Width <= 10);
        Assert.True(size.Height <= 5);
    }

    [Fact]
    public void Measure_WithVStackChild_ReturnsCorrectSize()
    {
        var vstack = new VStackNode
        {
            Children = new List<Hex1bNode>
            {
                new TextBlockNode { Text = "Line 1" },
                new TextBlockNode { Text = "Line 2" },
                new TextBlockNode { Text = "Line 3" }
            }
        };
        var node = new ThemePanelNode { Child = vstack, ThemeMutator = t => t };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(6, size.Width); // "Line X".Length
        Assert.Equal(3, size.Height);
    }

    #endregion

    #region Arrange Tests

    [Fact]
    public void Arrange_ChildGetsFullBounds()
    {
        var child = new TextBlockNode { Text = "Test" };
        var node = new ThemePanelNode { Child = child, ThemeMutator = t => t };
        var bounds = new Rect(5, 3, 20, 10);

        node.Measure(Constraints.Tight(20, 10));
        node.Arrange(bounds);

        // Child should have exact same bounds as ThemePanel
        Assert.Equal(bounds, child.Bounds);
    }

    [Fact]
    public void Arrange_SetsBounds()
    {
        var node = new ThemePanelNode 
        { 
            Child = new TextBlockNode { Text = "Test" },
            ThemeMutator = t => t 
        };
        var bounds = new Rect(0, 0, 20, 5);

        node.Arrange(bounds);

        Assert.Equal(bounds, node.Bounds);
    }

    [Fact]
    public void Arrange_WithNoChild_DoesNotThrow()
    {
        var node = new ThemePanelNode { Child = null };
        var bounds = new Rect(0, 0, 20, 5);

        var exception = Record.Exception(() => node.Arrange(bounds));

        Assert.Null(exception);
        Assert.Equal(bounds, node.Bounds);
    }

    #endregion

    #region Render Tests

    [Fact]
    public void Render_RendersChildContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var context = CreateContext(workload);

        var node = new ThemePanelNode
        {
            Child = new TextBlockNode { Text = "ThemePanel Content" },
            ThemeMutator = t => t
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 20, 5));
        node.Render(context);

        Assert.Contains("ThemePanel Content", terminal.CreateSnapshot().GetScreenText());
    }

    [Fact]
    public void Render_AppliesThemeMutation()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 30, 5);
        var baseTheme = new Hex1bTheme("Base");
        var context = CreateContext(workload, baseTheme);

        var mutatedThemeCaptured = false;
        var button = new ButtonNode { Label = "Test", IsFocused = true };

        var node = new ThemePanelNode
        {
            Child = button,
            ThemeMutator = theme =>
            {
                mutatedThemeCaptured = true;
                return theme
                    .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(255, 0, 0));
            }
        };

        node.Measure(Constraints.Tight(30, 5));
        node.Arrange(new Rect(0, 0, 30, 5));
        node.Render(context);

        Assert.True(mutatedThemeCaptured, "ThemeMutator should be called during render");
        Assert.True(terminal.CreateSnapshot().HasBackgroundColor(Hex1bColor.FromRgb(255, 0, 0)),
            "Button should have red background from mutated theme");
    }

    [Fact]
    public void Render_RestoresOriginalTheme()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 30, 5);
        var originalTheme = new Hex1bTheme("Original")
            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(0, 255, 0));
        var context = CreateContext(workload, originalTheme);

        var node = new ThemePanelNode
        {
            Child = new TextBlockNode { Text = "Test" },
            ThemeMutator = theme => theme
                .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(255, 0, 0))
        };

        node.Measure(Constraints.Tight(30, 5));
        node.Arrange(new Rect(0, 0, 30, 5));
        node.Render(context);

        // After render, context.Theme should be restored to original
        Assert.Same(originalTheme, context.Theme);
    }

    [Fact]
    public void Render_WithNullMutator_RendersNormally()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var context = CreateContext(workload);

        var node = new ThemePanelNode
        {
            Child = new TextBlockNode { Text = "No Mutator" },
            ThemeMutator = null
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 20, 5));
        node.Render(context);

        Assert.Contains("No Mutator", terminal.CreateSnapshot().GetScreenText());
    }

    [Fact]
    public void Render_WithNoChild_DoesNotThrow()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var context = CreateContext(workload);

        var node = new ThemePanelNode { Child = null, ThemeMutator = t => t };

        var exception = Record.Exception(() => node.Render(context));

        Assert.Null(exception);
    }

    [Fact]
    public void Render_CachedTheme_IsUsed()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 30, 5);
        var context = CreateContext(workload);

        var cachedTheme = new Hex1bTheme("Cached")
            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(0, 0, 255));

        var button = new ButtonNode { Label = "Cached", IsFocused = true };
        var node = new ThemePanelNode
        {
            Child = button,
            ThemeMutator = _ => cachedTheme // Always return cached theme
        };

        node.Measure(Constraints.Tight(30, 5));
        node.Arrange(new Rect(0, 0, 30, 5));
        node.Render(context);

        Assert.True(terminal.CreateSnapshot().HasBackgroundColor(Hex1bColor.FromRgb(0, 0, 255)),
            "Button should use cached theme's blue background");
    }

    #endregion

    #region Focus Tests

    [Fact]
    public void GetFocusableNodes_ReturnsFocusableChildren()
    {
        var button = new ButtonNode { Label = "Click" };
        var node = new ThemePanelNode { Child = button, ThemeMutator = t => t };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Single(focusables);
        Assert.Contains(button, focusables);
    }

    [Fact]
    public void GetFocusableNodes_WithNonFocusableChild_ReturnsEmpty()
    {
        var textBlock = new TextBlockNode { Text = "Not focusable" };
        var node = new ThemePanelNode { Child = textBlock, ThemeMutator = t => t };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Empty(focusables);
    }

    [Fact]
    public void GetFocusableNodes_WithNoChild_ReturnsEmpty()
    {
        var node = new ThemePanelNode { Child = null };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Empty(focusables);
    }

    [Fact]
    public void GetFocusableNodes_WithNestedContainers_FindsAllFocusables()
    {
        var textBox = new TextBoxNode { State = new TextBoxState() };
        var button = new ButtonNode { Label = "OK" };
        var vstack = new VStackNode
        {
            Children = new List<Hex1bNode> { textBox, button }
        };
        var node = new ThemePanelNode { Child = vstack, ThemeMutator = t => t };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Equal(2, focusables.Count);
        Assert.Contains(textBox, focusables);
        Assert.Contains(button, focusables);
    }

    [Fact]
    public void IsFocusable_ReturnsFalse()
    {
        var node = new ThemePanelNode();

        Assert.False(node.IsFocusable);
    }

    #endregion

    #region GetChildren Tests

    [Fact]
    public void GetChildren_ReturnsChild()
    {
        var child = new TextBlockNode { Text = "Child" };
        var node = new ThemePanelNode { Child = child };

        var children = node.GetChildren().ToList();

        Assert.Single(children);
        Assert.Same(child, children[0]);
    }

    [Fact]
    public void GetChildren_WithNoChild_ReturnsEmpty()
    {
        var node = new ThemePanelNode { Child = null };

        var children = node.GetChildren().ToList();

        Assert.Empty(children);
    }

    #endregion

    #region Input Handling Tests

    [Fact]
    public async Task HandleInput_PassesToChild()
    {
        var clicked = false;
        var button = new ButtonNode
        {
            Label = "Click",
            IsFocused = true,
            ClickAction = _ => { clicked = true; return Task.CompletedTask; }
        };
        var node = new ThemePanelNode { Child = button, ThemeMutator = t => t };

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        focusRing.EnsureFocus();
        var routerState = new InputRouterState();

        // Use InputRouter to route input to the focused child
        var result = await InputRouter.RouteInputAsync(
            node, 
            new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None), 
            focusRing, 
            routerState, 
            null, 
            TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.True(clicked);
    }

    [Fact]
    public void HandleInput_WithNoChild_ReturnsFalse()
    {
        var node = new ThemePanelNode { Child = null };

        var result = node.HandleInput(new Hex1bKeyEvent(Hex1bKey.A, 'A', Hex1bModifiers.None));

        Assert.Equal(InputResult.NotHandled, result);
    }

    #endregion

    #region Reconciliation Tests

    [Fact]
    public void Reconcile_CreatesNewNode_WhenNoExisting()
    {
        var widget = new ThemePanelWidget(t => t, new TextBlockWidget("Test"));
        var context = ReconcileContext.CreateRoot();

        var node = widget.Reconcile(null, context);

        Assert.IsType<ThemePanelNode>(node);
        var themePanelNode = (ThemePanelNode)node;
        Assert.NotNull(themePanelNode.Child);
        Assert.IsType<TextBlockNode>(themePanelNode.Child);
    }

    [Fact]
    public void Reconcile_ReusesExistingNode()
    {
        var existingNode = new ThemePanelNode 
        { 
            Child = new TextBlockNode { Text = "Old" },
            ThemeMutator = t => t
        };
        var widget = new ThemePanelWidget(t => t.Clone(), new TextBlockWidget("New"));
        var context = ReconcileContext.CreateRoot();

        var node = widget.Reconcile(existingNode, context);

        Assert.Same(existingNode, node);
    }

    [Fact]
    public void Reconcile_UpdatesThemeMutator()
    {
        var existingNode = new ThemePanelNode 
        { 
            Child = new TextBlockNode { Text = "Test" },
            ThemeMutator = t => t
        };
        Func<Hex1bTheme, Hex1bTheme> newMutator = t => t.Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Red);
        var widget = new ThemePanelWidget(newMutator, new TextBlockWidget("Test"));
        var context = ReconcileContext.CreateRoot();

        widget.Reconcile(existingNode, context);

        Assert.Same(newMutator, existingNode.ThemeMutator);
    }

    [Fact]
    public void Reconcile_UpdatesChild()
    {
        var existingNode = new ThemePanelNode 
        { 
            Child = new TextBlockNode { Text = "Old" },
            ThemeMutator = t => t
        };
        var widget = new ThemePanelWidget(t => t, new TextBlockWidget("New"));
        var context = ReconcileContext.CreateRoot();

        widget.Reconcile(existingNode, context);

        Assert.IsType<TextBlockNode>(existingNode.Child);
        Assert.Equal("New", ((TextBlockNode)existingNode.Child!).Text);
    }

    #endregion

    #region Nested ThemePanel Tests

    [Fact]
    public void Render_NestedThemePanels_ApplyThemesCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 10);
        var context = CreateContext(workload);

        var innerButton = new ButtonNode { Label = "Inner", IsFocused = true };
        var innerPanel = new ThemePanelNode
        {
            Child = innerButton,
            ThemeMutator = theme => theme
                .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(255, 0, 0))
        };

        var outerButton = new ButtonNode { Label = "Outer" };
        var outerVStack = new VStackNode
        {
            Children = new List<Hex1bNode> { outerButton, innerPanel }
        };

        var outerPanel = new ThemePanelNode
        {
            Child = outerVStack,
            ThemeMutator = theme => theme
                .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(0, 0, 255))
        };

        outerPanel.Measure(Constraints.Tight(50, 10));
        outerPanel.Arrange(new Rect(0, 0, 50, 10));
        outerPanel.Render(context);

        // Inner button should have red (from inner panel), not blue (from outer)
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.HasBackgroundColor(Hex1bColor.FromRgb(255, 0, 0)),
            "Inner button should have red background from inner ThemePanel");
    }

    #endregion
}
