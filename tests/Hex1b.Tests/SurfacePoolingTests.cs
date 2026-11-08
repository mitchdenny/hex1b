using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class SurfacePoolingTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SelectionPanel_NestedEffect_PropagatesAllocatorAndPreservesRendering(bool pooling)
    {
        var rendered = new List<Surface>();
        var text = new TextBlockNode { Text = "first" };
        var effect = new EffectPanelNode { Child = text, Effect = surface => rendered.Add(surface) };
        var selection = new SelectionPanelNode { Child = effect };
        selection.Measure(Constraints.Tight(20, 2));
        selection.Arrange(new Rect(0, 0, 20, 2));
        selection.EnterCopyMode();
        var target = new Surface(20, 2);
        var context = new SurfaceRenderContext(target)
        {
            CachingEnabled = false,
            SurfaceAllocator = pooling ? new SurfacePool(8, 120) : null
        };
        selection.Render(context);
        Assert.AreEqual("f", target[0, 0].Character);
        text.Text = "second";
        selection.Render(context);
        Assert.AreEqual("s", target[0, 0].Character);
        Assert.HasCount(2, rendered);
        Assert.AreEqual(pooling, ReferenceEquals(rendered[0], rendered[1]));
    }

    [TestMethod]
    [DataRow(-1, false, false)]
    [DataRow(-1, false, true)]
    [DataRow(-1, true, false)]
    [DataRow(-1, true, true)]
    [DataRow(0, false, false)]
    [DataRow(0, false, true)]
    [DataRow(0, true, false)]
    [DataRow(0, true, true)]
    [DataRow(1, false, false)]
    [DataRow(1, false, true)]
    [DataRow(1, true, false)]
    [DataRow(1, true, true)]
    [DataRow(2, false, false)]
    [DataRow(2, false, true)]
    [DataRow(2, true, false)]
    [DataRow(2, true, true)]
    public async Task Construction_TwoFrames_PreservesPoolingPolicy(int mode, bool caching, bool asynchronous)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(10));
        Hex1bApp? capturedApp = null;
        Surface? first = null;
        Surface? second = null;
        var phase = 1;
        var configurations = 0;
        Action<Hex1bAppOptions> configureOptions = options =>
        {
            options.EnableRenderCaching = caching;
            options.FrameRateLimitMs = 1;
            if (mode is 0 or 1)
                options.EnableSurfacePooling = mode == 1;
            Assert.AreEqual(mode >= 1, options.EnableSurfacePooling);
        };
        Func<Hex1bApp, Func<RootContext, Hex1bWidget>> configureApp = app =>
        {
            configurations++;
            capturedApp = app;
            return ctx => ctx.VStack(v => [
                v.Text("text" + phase).FixedHeight(1),
                v.Surface(layers => [
                    layers.Layer(surface =>
                    {
                        if (phase == 1)
                            first = surface;
                        else
                            second = surface;
                        surface.WriteText(0, 0, "draw" + phase);
                    })
                ]).FixedHeight(1)
            ]).InputBindings(bindings => bindings.Key(Hex1bKey.U).Action(_ =>
            {
                phase = 2;
                app.Invalidate();
            }));
        };
        Func<Hex1bApp, Func<RootContext, Task<Hex1bWidget>>> configureAsync = app =>
        {
            var render = configureApp(app);
            return async ctx =>
            {
                await Task.Yield();
                return render(ctx);
            };
        };
        var builder = Hex1bTerminal.CreateBuilder().WithHeadless().WithDimensions(20, 3);
        if (mode < 0)
            builder = asynchronous
                ? builder.WithUnpooledHex1bApp(configureOptions, configureAsync)
                : builder.WithUnpooledHex1bApp(configureOptions, configureApp);
        else
            builder = asynchronous
                ? builder.WithHex1bApp(configureOptions, configureAsync)
                : builder.WithHex1bApp(configureOptions, configureApp);

        await using var terminal = builder.Build();
        var run = terminal.RunAsync(cancellation.Token);
        try
        {
            using var firstSnapshot = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("text1") && s.ContainsText("draw1"),
                    TimeSpan.FromSeconds(5), "complete first frame")
                .Build().ApplyAsync(terminal, cancellation.Token);
            using var secondSnapshot = await new Hex1bTerminalInputSequenceBuilder()
                .Key(Hex1bKey.U)
                .WaitUntil(s => s.ContainsText("text2") && s.ContainsText("draw2"),
                    TimeSpan.FromSeconds(5), "complete second frame")
                .Build().ApplyAsync(terminal, cancellation.Token);
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreEqual(mode > 0, ReferenceEquals(first, second));
            Assert.AreEqual(1, configurations);
        }
        finally
        {
            capturedApp?.RequestStop();
            cancellation.CancelAfter(TimeSpan.FromSeconds(2));
            await run;
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WithUnpooledHex1bApp_EnablePooling_ThrowsAtBuild(bool asynchronous)
    {
        var builder = Hex1bTerminal.CreateBuilder().WithHeadless();
        if (asynchronous)
            builder.WithUnpooledHex1bApp(o => o.EnableSurfacePooling = true,
                _ => ctx => Task.FromResult<Hex1bWidget>(ctx.Text("invalid")));
        else
            builder.WithUnpooledHex1bApp(o => o.EnableSurfacePooling = true,
                _ => ctx => ctx.Text("invalid"));
        Assert.ThrowsExactly<InvalidOperationException>(() => builder.Build());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WithUnpooledHex1bApp_OptionsMutatedAfterBuild_ThrowsAtRun(bool asynchronous)
    {
        Hex1bAppOptions? capturedOptions = null;
        var builder = Hex1bTerminal.CreateBuilder().WithHeadless();
        if (asynchronous)
            builder.WithUnpooledHex1bApp(o => capturedOptions = o,
                _ => ctx => Task.FromResult<Hex1bWidget>(ctx.Text("invalid")));
        else
            builder.WithUnpooledHex1bApp(o => capturedOptions = o, _ => ctx => ctx.Text("invalid"));
        await using var terminal = builder.Build();
        Assert.IsNotNull(capturedOptions);
        capturedOptions.EnableSurfacePooling = true;
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => terminal.RunAsync(TestContext.Current.CancellationToken));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WithUnpooledHex1bApp_SimpleOverload_Renders(bool asynchronous)
    {
        var builder = Hex1bTerminal.CreateBuilder().WithHeadless().WithDimensions(20, 3);
        if (asynchronous)
            builder.WithUnpooledHex1bApp(async ctx =>
            {
                await Task.Yield();
                return ctx.Text("unpooled");
            });
        else
            builder.WithUnpooledHex1bApp(ctx => ctx.Text("unpooled"));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(10));
        await using var terminal = builder.Build();
        var run = terminal.RunAsync(cancellation.Token);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("unpooled"), TimeSpan.FromSeconds(5), "unpooled text")
            .Ctrl().Key(Hex1bKey.C)
            .Build().ApplyAsync(terminal, cancellation.Token);
        await run;
    }

    [TestMethod]
    public void WithUnpooledHex1bApp_NullCallbacks_Throw()
    {
        var builder = Hex1bTerminal.CreateBuilder();
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            builder.WithUnpooledHex1bApp((Func<RootContext, Hex1bWidget>)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            builder.WithUnpooledHex1bApp((Func<RootContext, Task<Hex1bWidget>>)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            builder.WithUnpooledHex1bApp(null!, _ => ctx => ctx.Text("invalid")));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            builder.WithUnpooledHex1bApp(null!, _ => ctx => Task.FromResult<Hex1bWidget>(ctx.Text("invalid"))));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            builder.WithUnpooledHex1bApp(_ => { }, (Func<Hex1bApp, Func<RootContext, Hex1bWidget>>)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            builder.WithUnpooledHex1bApp(_ => { }, (Func<Hex1bApp, Func<RootContext, Task<Hex1bWidget>>>)null!));
    }
}
