using Hex1b;
using Hex1b.Composition;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests.Composition;

public class CompositionContextTests
{
    [Fact]
    public async Task UseState_ReturnsSameInstanceAcrossReconciliations()
    {
        var node1 = await ReconcileAsync(new InspectStateCompositeWidget(), null);
        var captured1 = InspectStateCompositeWidget.LastSeenState;

        await ReconcileAsync(new InspectStateCompositeWidget(), node1);
        var captured2 = InspectStateCompositeWidget.LastSeenState;

        Assert.NotNull(captured1);
        Assert.Same(captured1, captured2);
    }

    [Fact]
    public async Task IsNew_TrueOnFirstFrameThenFalse()
    {
        var node1 = await ReconcileAsync(new IsNewObservingCompositeWidget(), null);
        Assert.True(IsNewObservingCompositeWidget.LastIsNew);

        await ReconcileAsync(new IsNewObservingCompositeWidget(), node1);
        Assert.False(IsNewObservingCompositeWidget.LastIsNew);
    }

    [Fact]
    public async Task Use_NoAncestorProvided_ReturnsNull()
    {
        var node = await ReconcileAsync(new UseFooCompositeWidget(), null);
        var composite = Assert.IsType<Hex1bCompositeNode>(node);
        var text = Assert.IsType<TextBlockNode>(composite.Child);
        Assert.Equal("Foo=<none>", text.Text);
    }

    [Fact]
    public async Task Require_NoAncestorProvided_BubblesUpInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ReconcileAsync(new RequireFooCompositeWidget(), null));
    }

    [Fact]
    public async Task Provide_FromAncestorComposite_FlowsToDescendantUse()
    {
        var node = await ReconcileAsync(new ProvideFooCompositeWidget("hello"), null);

        // Tree shape: ProvideFooCompositeWidget → UseFooCompositeWidget → TextBlock("Foo=hello")
        var outer = Assert.IsType<Hex1bCompositeNode>(node);
        var inner = Assert.IsType<Hex1bCompositeNode>(outer.Child);
        var text = Assert.IsType<TextBlockNode>(inner.Child);
        Assert.Equal("Foo=hello", text.Text);
    }

    [Fact]
    public async Task Provide_NestedSameType_InnerShadowsOuter()
    {
        var node = await ReconcileAsync(
            new ProvideFooCompositeWidget("outer", Inner: new ProvideFooCompositeWidget("inner")),
            null);

        // Tree: outer Provide("outer") → inner Provide("inner") → UseFooCompositeWidget → TextBlock("Foo=inner")
        var outer = Assert.IsType<Hex1bCompositeNode>(node);
        var middle = Assert.IsType<Hex1bCompositeNode>(outer.Child);
        var leaf = Assert.IsType<Hex1bCompositeNode>(middle.Child);
        var text = Assert.IsType<TextBlockNode>(leaf.Child);
        Assert.Equal("Foo=inner", text.Text);
    }

    [Fact]
    public async Task Provide_OnlyVisibleToDescendants_NotSiblings()
    {
        // VStack of two ProvideFoo peers; the second uses Use<FooContext>().
        // The first provides Foo=alpha, but the second sibling shouldn't see it because they
        // share an ancestor, not a parent/child relationship.
        // Note: outside of a Build method there's no CompositionContext, so we construct
        // the root VStack directly here.
        var widget = new VStackWidget(
        [
            new ProvideFooCompositeWidget("alpha"),
            new UseFooCompositeWidget(),
        ]);

        var rootShell = new RootShellNode();
        var ctx = ReconcileContext.CreateRoot();
        var stack = (VStackNode)(await ctx.ReconcileChildAsync(null, widget, rootShell))!;
        var second = Assert.IsType<Hex1bCompositeNode>(stack.Children[1]);
        var text = Assert.IsType<TextBlockNode>(second.Child);
        Assert.Equal("Foo=<none>", text.Text);
    }

    // --- Composite test fixtures ---

    private sealed record InspectStateCompositeWidget : Hex1bWidget
    {
        public static StateBox? LastSeenState { get; private set; }

        protected override Hex1bWidget Build(CompositionContext ctx)
        {
            LastSeenState = ctx.UseState(() => new StateBox());
            return ctx.Text("inspect");
        }

        internal sealed class StateBox { }
    }

    private sealed record IsNewObservingCompositeWidget : Hex1bWidget
    {
        public static bool LastIsNew { get; private set; }

        protected override Hex1bWidget Build(CompositionContext ctx)
        {
            LastIsNew = ctx.IsNew;
            return ctx.Text("ok");
        }
    }

    internal sealed class FooContext
    {
        public required string Value { get; init; }
    }

    private sealed record UseFooCompositeWidget : Hex1bWidget
    {
        protected override Hex1bWidget Build(CompositionContext ctx)
        {
            var foo = ctx.Use<FooContext>();
            return ctx.Text($"Foo={foo?.Value ?? "<none>"}");
        }
    }

    private sealed record RequireFooCompositeWidget : Hex1bWidget
    {
        protected override Hex1bWidget Build(CompositionContext ctx)
        {
            var foo = ctx.Require<FooContext>();
            return ctx.Text($"Foo={foo.Value}");
        }
    }

    private sealed record ProvideFooCompositeWidget(string Value, Hex1bWidget? Inner = null) : Hex1bWidget
    {
        protected override Hex1bWidget Build(CompositionContext ctx)
        {
            ctx.Provide(new FooContext { Value = Value });
            return Inner ?? (Hex1bWidget)new UseFooCompositeWidget();
        }
    }

    // --- Helpers ---

    private static async Task<Hex1bNode> ReconcileAsync(Hex1bWidget widget, Hex1bNode? existing)
    {
        var context = ReconcileContext.CreateRoot();
        var rootShell = new RootShellNode();
        return (await context.ReconcileChildAsync(existing, widget, rootShell))!;
    }

    private sealed class RootShellNode : Hex1bNode
    {
        protected override Size MeasureCore(Constraints constraints) => Size.Zero;
        public override void Render(Hex1bRenderContext context) { }
    }
}
