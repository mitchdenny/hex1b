# Hex1b rendering performance — baseline (2026-05-26)

> Captured on Apple Silicon (`Darwin 25.5.0 arm64`), .NET SDK 10.0.300,
> `Release` build, using `samples/PerfDemo` with the new `--busy` mode.
>
> Repro:
> ```bash
> dotnet run -c Release --project samples/PerfDemo -- --busy --frames 300 --warmup 50
> ```

## Workload

A representative "busy" UX:

- Outer `HSplitter`: sidebar (Border + 20-item VStack, one row mutates per frame)
  on the left; main pane on the right.
- Main pane: outer `Border` wrapping a `VSplitter` of a Dashboard (Progress bar
  + 8×12 numeric grid that mutates every cell every frame) and a Log
  (20-line scrolling list — every visible row changes every frame).
- 160 × 50 cells, frame-rate-limit 1 ms, surface rendering with the discard
  workload adapter.

The scene is deliberately tuned so that the per-frame diff produces ~200
changed cells / ~330 ANSI tokens / ~1.2 KB output — non-trivial but not
unrealistic for a real app.

## Headline numbers (no caching, no pooling — i.e. defaults)

| metric                              | mean    | p50     | p95     | p99     | max     |
| ----------------------------------- | ------- | ------- | ------- | ------- | ------- |
| `hex1b.frame.duration` (ms)         | 12.22   | 12.29   | 14.87   | 15.30   | 15.60   |
| `hex1b.frame.build.duration` (ms)   | 0.011   | 0.006   | 0.022   | 0.040   | 1.104   |
| `hex1b.frame.reconcile.duration`    | 0.050   | 0.031   | 0.098   | 0.113   | 2.554   |
| `hex1b.frame.render.duration`       | 12.10   | 12.15   | 14.67   | 15.24   | 15.31   |
| `hex1b.surface.diff.duration`       | 0.095   | 0.070   | 0.111   | 0.639   | 1.054   |
| `hex1b.surface.tokens.duration`     | 0.013   | 0.007   | 0.028   | 0.045   | 0.053   |
| `hex1b.surface.serialize.duration`  | 0.007   | 0.003   | 0.017   | 0.035   | 0.036   |

| accounting | per frame |
| ---------- | --------- |
| allocations | **7.45 MB** |
| Gen0 / Gen1 / Gen2 over 300 frames | **267 / 171 / 121** |
| measure calls per frame (~162 nodes) | **~668** (~4× per node) |

### Interpretation

1. **Render phase is 99% of frame time** (12.10 / 12.22 ms).
2. **The instrumented sub-phases of render (diff + tokens + serialize) total
   ~0.12 ms** — i.e. less than 1% of the frame. The other ~12 ms is in
   `node.Render` (cells being drawn onto the surface) and to a lesser
   extent measure/arrange.
3. **Allocations are the elephant in the room**: 7.45 MB per frame and
   **121 Gen2 collections in 300 frames** (≈40% of frames trigger a Gen2 on
   this machine). This is what makes the UI feel "crappy" even on fast
   hardware — Gen2 collections produce visible stalls.
4. The 4× over-measuring (~668 measure calls for 162 nodes) is real but
   small compared to the allocation cost.

## A/B: caching and pooling

300-frame busy run, same machine, identical workload:

| variant                 | frame mean (ms) | bytes/frame | Gen0/Gen1/Gen2 |
| ----------------------- | --------------: | ----------: | -------------: |
| **new defaults (pool on)** | **10.72**    | **3,462,716** | **124 / 32 / 0** |
| `--no-pool` (legacy default) | 12.16       | 7,436,100   | 279 / 192 / 138 |
| `--cache` (caching on)  | 12.31           | 7,323,975   | 244 / 180 / 111 |

### What this means

- **`EnableSurfacePooling` is now the default** as of this branch — the
  full Hex1b test suite (7902 tests across 5 projects) passes unchanged,
  and the original commit message only described it as "opt-in" with no
  recorded correctness reason. Measured against the previous default:
  - **-53% allocations per frame**
  - **138 → 0 Gen2 GCs** over 300 frames
  - **-12% mean frame time**
- `EnableRenderCaching` does nothing here because every visible widget
  changes every frame (the scrolling log). It's still useful in scenes
  with large static subtrees — `samples/PerfDemo` (non-busy mode) is the
  case to validate that.

## CPU hotspots (managed inclusive time, ~300 frames sampled)

From `dotnet-trace --profile dotnet-sampled-thread-time` (Apple Silicon)
converted to speedscope. Managed self-time per frame is below the sampling
floor (≤10 ms ticks vs ~12 ms total per frame), so we read **inclusive**
time:

| % wall | inclusive ms | function |
| -----: | ----------: | -------- |
| 73.96  | 26,898 | `Surfaces.SurfaceRenderContext.RenderChild` (root of the render tree) |
| 19.01  |  6,914 | `SplitterNode.Render` |
| 18.70  |  6,802 | `Nodes.LayoutNode.Render` |
| 18.32  |  6,663 | `Nodes.BorderNode.Render` |
| 14.83  |  5,393 | `VStackNode.Render` |
| 13.64  |  4,960 | `Hex1bApp.RenderFrameWithSurface` |
| 13.36  |  4,858 | `ZStackNode.Render` |
|  8.81  |  3,202 | `Hex1bApp.RenderFrameAsync` |
|  8.70  |  3,162 | `Thread.PollGC` *(GC pressure signal)* |
|  8.49  |  3,086 | `Buffer.BulkMoveWithWriteBarrier` *(surface composite/array copies)* |

The most expensive *leaf* operation on the render path is
**`Buffer.BulkMoveWithWriteBarrier`** at ~8.5%, which is the bulk-copy
behind `Surface.Composite` / `SurfaceCell[]` clones. The next layer up is
the per-cell write path through `SurfaceRenderContext.WriteToSurface` /
`SliceByDisplayWidthWithAnsi` / `DisplayWidth.GetGraphemeAtIndex` /
`TextSegmentationUtility.GetLengthOfFirstExtendedGraphemeCluster` — i.e.
**grapheme analysis runs on every single cell write**, which is likely a
significant fraction of the per-node render time.

Raw speedscope JSON is saved to
`~/.copilot/session-state/<session>/files/baseline-busy300.speedscope.json`
for inspection in https://speedscope.app .

## Ranked optimisation opportunities

| # | hypothesis | est. impact | risk |
|---|------------|-------------|------|
| 1 | ✅ **DONE — `EnableSurfacePooling` default flipped to `true`.** Full test suite still green (7902 tests, 0 failures). Measured -53% allocations, -12% frame, 0 Gen2 GCs vs previous default. | shipped | — |
| 2 | ASCII fast path for any remaining hot grapheme calls (`DisplayWidth.GetGraphemeAtIndex` / `TextSegmentationUtility.GetLengthOfFirstExtendedGraphemeCluster` still appear in the busy trace despite an existing fast-path in `GetNextGrapheme` — likely a second call site). | high | low |
| 3 | Reduce measure-pass amplification. ~4× per node on splitter-heavy trees. Likely two-pass measure inside `SplitterNode`; verify and short-circuit when constraints are tight. | medium | medium |
| 4 | Pool / reuse the `ChangedCell` list and other per-frame collections inside `SurfaceComparer`. Currently allocates a fresh `List<ChangedCell>` and sorts it every frame. | medium | low |
| 5 | Per-node `Render` allocations (Splitter / Border / VStack / ZStack). Investigate `string.Format`, `string` interpolation, and any `LINQ`/`ToList()` inside these — the inclusive-time signal is loud but cell-write cost may be the bulk. Need per-call allocation instrumentation. | medium | low |
| 6 | `Buffer.BulkMoveWithWriteBarrier` at 8.5% suggests `SurfaceCell[]` is being cloned. Investigate `Surface.Composite` / `Surface.Clone`; if cells are value-type-ish and bigger than 16 bytes, copy cost can be reduced via in-place writes or smaller cell representation. | medium | medium |

Numbers 2–6 should each get a BenchmarkDotNet case in
`benchmarks/Hex1b.Benchmarks/` and an A/B run via this same `--busy`
workload before/after.

## How to reproduce / re-measure

```bash
# Build once
dotnet build -c Release samples/PerfDemo/PerfDemo.csproj

# Headline busy run (prints metrics summary at the end)
dotnet run -c Release --project samples/PerfDemo --no-build -- \
    --busy --frames 300 --warmup 50

# Enable per-node histograms (tagged by node metric path)
dotnet run -c Release --project samples/PerfDemo --no-build -- \
    --busy --per-node --frames 300 --warmup 50

# Compare cache / pool variants
dotnet run -c Release --project samples/PerfDemo --no-build -- --busy --cache
dotnet run -c Release --project samples/PerfDemo --no-build -- --busy --no-pool

# CPU profile (macOS / Linux compatible profile)
dotnet-trace collect --format speedscope --profile dotnet-sampled-thread-time \
  --duration 00:00:00:30 \
  -- $HOME/.dotnet/dotnet samples/PerfDemo/bin/Release/net10.0/PerfDemo.dll \
        --busy --frames 300 --warmup 50 --no-summary
# Then open the .speedscope.json file in https://speedscope.app
```

### New flags added to `PerfDemo`

| flag | default | meaning |
|------|---------|---------|
| `--busy` | off | Use the realistic "busy" widget tree instead of the static panel |
| `--per-node` | off | Enable per-node histograms (extra meter instruments) |
| `--no-summary` | summary on | Suppress the end-of-run metric summary |
| `--no-pool` | pool on | Disable surface pooling (matches the pre-default-flip behaviour for A/B) |
| `--cache` | off | Enable widget-tree render caching |
| `--busy-sidebar N` | 20 | Sidebar item count |
| `--busy-log N` | 20 | Scrolling log line count |
| `--busy-grid-rows N` | 8 | Dashboard grid rows |
| `--busy-grid-cols N` | 12 | Dashboard grid columns |
| `--width N` | 160 (busy) | Terminal width |
| `--height N` | 50 (busy) | Terminal height |

## Interactive repro: `samples/PerfStressDemo`

`PerfDemo` measures with the output discarded. `PerfStressDemo` is the
companion that runs as a *real* TUI so the regression is visible to the
naked eye (and to a `dotnet-trace` run against the live process).

```bash
dotnet run -c Release --project samples/PerfStressDemo                # defaults: pool on, ~30 fps target
dotnet run -c Release --project samples/PerfStressDemo -- --no-pool   # A/B: pool off
```

* `PgUp` / `PgDn` switch between stress pages, `Q` quits.
* The status bar shows live FPS and accumulated GC counts (Gen0 / Gen1 /
  Gen2), so the SurfacePool win is unmistakable: with the pool on Gen2
  stays at 0; with `--no-pool` it climbs steadily.

Pages currently shipped:

1. **Ripple over noise** — full-screen white random ASCII inside an
   `EffectPanel` painting a continuously expanding circular ripple. This
   is the workload that originally motivated the perf investigation.
