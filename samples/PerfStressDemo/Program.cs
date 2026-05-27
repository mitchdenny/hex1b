using System.Diagnostics;
using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;
using PerfStressDemo;

// ─────────────────────────────────────────────────────────────────────────
// PerfStressDemo
// ─────────────────────────────────────────────────────────────────────────
// A multi-page TUI sample whose only purpose is to push the renderer hard
// so we can visually inspect how it copes. PgUp/PgDn navigate between
// stress pages. Each page targets a different render hotspot.
//
// Page 1 is the "ripple over noise" repro that originally motivated the
// rendering-perf investigation: a full screen of random characters with
// an EffectPanel painting a continuous ripple wave. The status bar shows
// live frame rate and Gen2 collection count so the GC pressure win from
// SurfacePool is visible to the naked eye.
//
// CLI flags:
//     --no-pool         Disable the surface pool (A/B against default).
//     --target-fps <N>  Redraw cadence in frames per second (default 30).
//     --no-cache        Disable render caching.
//     --ripple-levels <N>  Quantise the ripple gradient into N greyscale
//                          bands (default 256 = smooth true-color). Lower
//                          values dramatically reduce bytes/frame.
// ─────────────────────────────────────────────────────────────────────────

bool enablePool = true;
bool enableCache = true;
int targetFps = 30;

for (var i = 0; i < args.Length; i++)
{
    var arg = args[i];
    switch (arg)
    {
        case "--no-pool":
            enablePool = false;
            break;
        case "--no-cache":
            enableCache = false;
            break;
        case "--target-fps" when i + 1 < args.Length && int.TryParse(args[i + 1], out var fps) && fps > 0:
            targetFps = fps;
            i++;
            break;
        case "--ripple-levels" when i + 1 < args.Length && int.TryParse(args[i + 1], out var lvl) && lvl > 0:
            RippleOverNoisePage.Levels = lvl;
            i++;
            break;
        case "-h":
        case "--help":
            Console.WriteLine("PerfStressDemo — multi-page TUI render stress harness");
            Console.WriteLine();
            Console.WriteLine("  --no-pool            Disable surface pooling");
            Console.WriteLine("  --no-cache           Disable render caching");
            Console.WriteLine("  --target-fps <N>     Target frame rate (default 30)");
            Console.WriteLine("  --ripple-levels <N>  Quantise ripple to N greyscale bands (default 256)");
            Console.WriteLine("  PgUp / PgDn          Switch between stress pages");
            Console.WriteLine("  L                    Cycle ripple quantisation 256→16→8→4→2→256");
            Console.WriteLine("  V                    Cycle pond viscosity (water→light→medium→thick→glob)");
            Console.WriteLine("  Click / Scroll       Whirlpool: left=on/move, right=off, scroll=strength");
            Console.WriteLine("  Q                    Quit");
            return;
    }
}

var pages = new IStressPage[]
{
    new RippleOverNoisePage(),
    new PondRipplePage(),
    new WhirlpoolPage(),
};

int currentPageIndex = 0;
int redrawIntervalMs = Math.Max(1, 1000 / targetFps);

// FPS / GC counters refreshed lazily — sample on every Build call.
var clock = Stopwatch.StartNew();
long frameCount = 0;
double lastFpsSampleSeconds = 0;
long lastFpsSampleFrames = 0;
double currentFps = 0;
int baselineGen2 = GC.CollectionCount(2);
int baselineGen1 = GC.CollectionCount(1);
int baselineGen0 = GC.CollectionCount(0);

Hex1bApp? appRef = null;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bAppOptions(o =>
    {
        o.EnableSurfacePooling = enablePool;
        o.EnableRenderCaching = enableCache;
        // Enforce the target frame rate at the framework's animation timer
        // level. This MUST be set before Hex1bApp construction (the timer's
        // minimum interval is captured in the ctor), so we set it via
        // WithHex1bAppOptions which runs before WithHex1bApp's configure.
        o.FrameRateLimitMs = redrawIntervalMs;
    })
    .WithHex1bApp((app, options) =>
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

    // Sample FPS once per ~500ms so the number is readable.
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

    var gen2 = GC.CollectionCount(2) - baselineGen2;
    var gen1 = GC.CollectionCount(1) - baselineGen1;
    var gen0 = GC.CollectionCount(0) - baselineGen0;

    var pageLabel = $"Page {currentPageIndex + 1}/{pages.Length}: {page.Name}";
    var perfLabel = $"{currentFps,5:0.0} fps  GC[0/1/2]={gen0}/{gen1}/{gen2}";
    var poolLabel = enablePool ? "pool=on" : "pool=OFF";
    var cacheLabel = enableCache ? "cache=on" : "cache=OFF";

    var rootWidget = root.VStack(v => new Hex1bWidget[]
    {
        // The page itself fills all remaining vertical space.
        pageWidget.Fill(),

        // Status bar at the bottom.
        v.InfoBar(s => new IInfoBarChild[]
        {
            s.Section(pageLabel).FillWidth(),
            s.Divider(" │ "),
            s.Section(perfLabel),
            s.Divider(" │ "),
            s.Section($"{poolLabel}  {cacheLabel}  ripple={RippleOverNoisePage.LevelsLabel}  pond={PondRipplePage.PresetLabel}  whirl={(WhirlpoolPage.WellActive ? "on" : "off")}@{WhirlpoolPage.CurrentStrength:0.0}"),
            s.Divider(" │ "),
            s.Section("PgUp/PgDn  L levels  V viscosity  Click/Scroll whirlpool  Q quit"),
        }),
    });

    // Only drive continuous animation when the active page has something
    // to animate. When the page reports IsIdle (e.g. pond fully settled,
    // no mouse activity), drop RedrawAfter so the framework sleeps until
    // a real input event arrives — this is what actually drops CPU to
    // ~zero between interactions, not pausing the inactive page (which is
    // already paused — its Build isn't called when it's not selected).
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
            // Cycle: 256 (smooth) → 16 → 8 → 4 → 2 → 256
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
