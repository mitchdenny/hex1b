using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Tests.NativeAot;

[TestClass]
[DoNotParallelize]
[TestCategory("NativeAot")]
[NativeAotEnabled]
public sealed class SurfacePoolingScenarioTests
{
    private static readonly CodeFamily Pool = new("surface pool", @"^Hex1b_Hex1b_Surfaces_SurfacePool_");
    private static readonly CodeFamily App = new("app", @"^Hex1b_Hex1b_Hex1bApp_");
    private static readonly CodeFamily SurfaceRenderer = new("surface renderer", @"^Hex1b_Hex1b_Surfaces_SurfaceRenderContext_");
    private static readonly CodeFamily SurfaceNode = new("surface widget", @"^Hex1b_Hex1b_Nodes_SurfaceNode_");
    private static readonly CodeFamily EffectNode = new("effect panel", @"^Hex1b_Hex1b_Nodes_EffectPanelNode_");
    private static readonly CodeFamily ChartNode = new("bar chart", @"^Hex1b_Hex1b_Nodes_BarChartNode");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public Task UnpooledOnly_NativePublish_EliminatesPoolAndRenders()
        => RunAsync("UnpooledOnly", UnpooledSetup, "[-1]", requiresPool: false);

    [TestMethod]
    public Task RuntimeDisabled_NativePublish_RetainsPoolButDoesNotReuse()
        => RunAsync("RuntimeDisabled", RuntimeSetup, "[0]", requiresPool: true);

    [TestMethod]
    public Task RuntimeConfigurable_NativePublish_PreservesDefaultTrueAndFalse()
        => RunAsync("RuntimeConfigurable", RuntimeSetup, "[2, 1, 0]", requiresPool: true);

    [TestMethod]
    public Task MixedConstruction_NativePublish_RetainsPoolForOrdinaryApp()
        => RunAsync("MixedConstruction", """
            if (mode < 0)
            {
                SETUP_UNPOOLED
            }
            else
            {
                SETUP_RUNTIME
            }
            """.Replace("SETUP_UNPOOLED", UnpooledSetup, StringComparison.Ordinal)
               .Replace("SETUP_RUNTIME", RuntimeSetup, StringComparison.Ordinal),
            "[-1, 2]", requiresPool: true);

    private Task RunAsync(string name, string setup, string modes, bool requiresPool)
    {
        CodeFamily[] rendering = [Scenario.Terminal, Scenario.Builder, App, SurfaceRenderer, SurfaceNode, EffectNode, ChartNode];
        var scenario = new Scenario(name, requiresPool ? [.. rendering, Pool] : rendering, requiresPool ? [] : [Pool]);
        return ScenarioRunner.RunAsync(scenario, Source(setup, modes, name), TestContext, TestContext.CancellationToken);
    }

    private const string UnpooledSetup = """
        builder = asynchronous
            ? builder.WithUnpooledHex1bApp(configureOptions, configureAsync)
            : builder.WithUnpooledHex1bApp(configureOptions, configureApp);
        """;

    private const string RuntimeSetup = """
        builder = asynchronous
            ? builder.WithHex1bApp(configureOptions, configureAsync)
            : builder.WithHex1bApp(configureOptions, configureApp);
        """;

    private static string Source(string setup, string modes, string name) => $$"""
        using Hex1b;
        using Hex1b.Automation;
        using Hex1b.Charts;
        using Hex1b.Input;
        using Hex1b.Surfaces;
        using Hex1b.Theming;
        using Hex1b.Widgets;

        int[] modes = {{modes}};
        foreach (var mode in modes)
            foreach (var caching in new[] { false, true })
                foreach (var asynchronous in new[] { false, true })
                    await RenderAsync(mode, caching, asynchronous);
        Console.WriteLine("NATIVE_AOT_OK:{{name}}");

        static async Task RenderAsync(int mode, bool caching, bool asynchronous)
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var phase = 1;
            var configured = 0;
            Hex1bApp? capturedApp = null;
            Surface? firstDraw = null;
            Surface? firstEffect = null;
            bool? reusedDraw = null;
            bool? reusedEffect = null;
            var blue = Hex1bColor.FromRgb(12, 34, 56);
            var red = Hex1bColor.FromRgb(200, 10, 20);

            Action<Hex1bAppOptions> configureOptions = options =>
            {
                options.EnableRenderCaching = caching;
                options.FrameRateLimitMs = 1;
                if (mode is 0 or 1)
                    options.EnableSurfacePooling = mode == 1;
                if (mode < 0 && options.EnableSurfacePooling)
                    throw new InvalidOperationException("Unpooled options must start false.");
            };
            Func<Hex1bApp, Func<RootContext, Hex1bWidget>> configureApp = app =>
            {
                configured++;
                capturedApp = app;
                return ctx => ctx.VStack(v => [
                    v.Text("text" + phase).FixedHeight(1),
                    v.BarChart(new ChartItem("A", 4), new ChartItem("B", 2)).Range(0, 4).FixedHeight(3),
                    v.Surface(layers => [
                        layers.Layer(surface =>
                        {
                            if (phase == 1)
                                firstDraw = surface;
                            else
                                reusedDraw = ReferenceEquals(firstDraw, surface);
                            surface.WriteText(0, 0, "draw" + phase, red);
                        })
                    ]).FixedHeight(1),
                    v.EffectPanel(v.Text("effect" + phase).FixedHeight(2), surface =>
                    {
                        if (phase == 1)
                            firstEffect = surface;
                        else
                            reusedEffect = ReferenceEquals(firstEffect, surface);
                        for (var y = 0; y < surface.Height; y++)
                            for (var x = 0; x < surface.Width; x++)
                                surface[x, y] = surface[x, y] with { Background = blue };
                    }).FixedHeight(2)
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
            var builder = Hex1bTerminal.CreateBuilder().WithHeadless().WithDimensions(20, 8);
            {{setup}}
            await using var terminal = builder.Build();
            var run = terminal.RunAsync(cancellation.Token);
            try
            {
                using var first = await new Hex1bTerminalInputSequenceBuilder()
                    .WaitUntil(s => Complete(s, 1, blue, red), TimeSpan.FromSeconds(5), "complete first frame")
                    .Build().ApplyAsync(terminal, cancellation.Token);
                if (firstDraw is null || firstEffect is null)
                    throw new InvalidOperationException("Temporary surface callbacks did not run.");
                using var second = await new Hex1bTerminalInputSequenceBuilder()
                    .Key(Hex1bKey.U)
                    .WaitUntil(s => Complete(s, 2, blue, red), TimeSpan.FromSeconds(5), "complete second frame")
                    .Build().ApplyAsync(terminal, cancellation.Token);
                var expectReuse = mode > 0;
                if (configured != 1 || reusedDraw != expectReuse || reusedEffect != expectReuse)
                    throw new InvalidOperationException($"Pooling mismatch: mode={mode}, caching={caching}, async={asynchronous}, draw={reusedDraw}, effect={reusedEffect}, configured={configured}.");
                Console.WriteLine($"VERIFIED:mode={mode},caching={caching},async={asynchronous},reuse={expectReuse}");
            }
            finally
            {
                capturedApp?.RequestStop();
                cancellation.CancelAfter(TimeSpan.FromSeconds(2));
                await run;
            }
        }

        static bool Complete(Hex1bTerminalSnapshot snapshot, int phase, Hex1bColor blue, Hex1bColor red)
        {
            if (!snapshot.ContainsText("text" + phase) || !snapshot.ContainsText("draw" + phase) ||
                !snapshot.ContainsText("effect" + phase) ||
                snapshot.GetCell(0, 1).Character != "A" || snapshot.GetCell(0, 3).Character != "B" ||
                snapshot.GetCell(0, 4).Foreground?.Equals(red) != true ||
                snapshot.GetCell(0, 5).Background?.Equals(blue) != true ||
                snapshot.GetCell(19, 6).Background?.Equals(blue) != true)
                return false;
            for (var x = 2; x < 20; x++)
                if (snapshot.GetCell(x, 1).Character != "█" ||
                    snapshot.GetCell(x, 3).Character != (x < 11 ? "█" : " "))
                    return false;
            return true;
        }
        """;
}
