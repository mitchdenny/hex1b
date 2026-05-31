using System.Diagnostics;
using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;
using PerfStressDemo;

namespace BlazorPerfStressDemo;

/// <summary>
/// Blazor-WASM-hosted version of PerfStressDemo. Mirrors
/// <c>samples/PerfStressDemo/Program.cs</c> but:
///   * scoped to the first two pages (RippleOverNoise + PondRipple) — those
///     are the ones that are pure render-cost workloads suitable for
///     comparing native-terminal vs WASM perf;
///   * wired to <see cref="BlazorPresentationAdapter"/> instead of stdout;
///   * uses defaults instead of CLI flags (browsers don't take argv).
/// </summary>
public static class PerfStressRunner
{
    public static async Task RunAsync(int initialCols, int initialRows)
    {
        const bool enablePool = true;
        const bool enableCache = true;
        const int targetFps = 30;

        int redrawIntervalMs = Math.Max(1, 1000 / targetFps);

        var pages = new IStressPage[]
        {
            new RippleOverNoisePage(),
            new PondRipplePage(),
        };
        int currentPageIndex = 0;

        var clock = Stopwatch.StartNew();
        long frameCount = 0;
        double lastFpsSampleSeconds = 0;
        long lastFpsSampleFrames = 0;
        double currentFps = 0;
        int baselineGen0 = GC.CollectionCount(0);
        int baselineGen1 = GC.CollectionCount(1);
        int baselineGen2 = GC.CollectionCount(2);

        Hex1bApp? appRef = null;
        var adapter = new BlazorPresentationAdapter(initialCols, initialRows);

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPresentation(adapter)
            .WithMouse()
            .WithHex1bApp(
                o =>
                {
                    o.EnableSurfacePooling = enablePool;
                    o.EnableRenderCaching = enableCache;
                    o.FrameRateLimitMs = redrawIntervalMs;
                },
                app =>
                {
                    appRef = app;
                    return ctx => BuildRoot(ctx);
                })
            .Build();

        await terminal.RunAsync();
        return;

        Hex1bWidget BuildRoot(RootContext root)
        {
            frameCount++;

            var nowSeconds = clock.Elapsed.TotalSeconds;
            var dt = nowSeconds - lastFpsSampleSeconds;
            if (dt >= 0.5)
            {
                currentFps = (frameCount - lastFpsSampleFrames) / dt;
                lastFpsSampleFrames = frameCount;
                lastFpsSampleSeconds = nowSeconds;
            }

            var page = pages[currentPageIndex];
            var stressCtx = new StressContext(root, nowSeconds, redrawIntervalMs);
            var pageWidget = page.Build(stressCtx);

            var gen0 = GC.CollectionCount(0) - baselineGen0;
            var gen1 = GC.CollectionCount(1) - baselineGen1;
            var gen2 = GC.CollectionCount(2) - baselineGen2;

            var pageLabel = $"Page {currentPageIndex + 1}/{pages.Length}: {page.Name}";
            var perfLabel = $"{currentFps,5:0.0} fps  GC[0/1/2]={gen0}/{gen1}/{gen2}";

            var rootWidget = root.VStack(v => new Hex1bWidget[]
            {
                pageWidget.Fill(),
                v.InfoBar(s => new IInfoBarChild[]
                {
                    s.Section(pageLabel).FillWidth(),
                    s.Divider(" │ "),
                    s.Section(perfLabel),
                    s.Divider(" │ "),
                    s.Section($"ripple={RippleOverNoisePage.LevelsLabel}  pond={PondRipplePage.PresetLabel}"),
                    s.Divider(" │ "),
                    s.Section("PgUp/PgDn switch  L levels  V viscosity  Q quit"),
                }),
            });

            if (!page.IsIdle)
                rootWidget = rootWidget.RedrawAfter(redrawIntervalMs);

            return rootWidget
                .InputBindings(bindings =>
                {
                    bindings.Key(Hex1bKey.PageDown).Global().Action(_ =>
                    {
                        currentPageIndex = (currentPageIndex + 1) % pages.Length;
                    }, "Next page");

                    bindings.Key(Hex1bKey.PageUp).Global().Action(_ =>
                    {
                        currentPageIndex = (currentPageIndex - 1 + pages.Length) % pages.Length;
                    }, "Previous page");

                    bindings.Key(Hex1bKey.Q).Global().Action(_ => appRef?.RequestStop(), "Quit");

                    bindings.Key(Hex1bKey.L).Global().Action(_ =>
                    {
                        RippleOverNoisePage.Levels = RippleOverNoisePage.Levels switch
                        {
                            >= 256 => 16,
                            16 => 8,
                            8 => 4,
                            4 => 2,
                            _ => 256,
                        };
                    }, "Cycle ripple levels");

                    bindings.Key(Hex1bKey.V).Global().Action(_ =>
                    {
                        PondRipplePage.CyclePreset();
                    }, "Cycle pond viscosity");
                });
        }
    }
}
