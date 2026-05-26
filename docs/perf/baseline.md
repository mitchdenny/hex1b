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

| variant                | frame mean (ms) | bytes/frame | Gen0/Gen1/Gen2 |
| ---------------------- | --------------: | ----------: | -------------: |
| **defaults**           | 12.22           | 7,455,729   | 267 / 171 / 121 |
| `EnableRenderCaching`  | 12.31           | 7,323,975   | 244 / 180 / 111 |
| `EnableSurfacePooling` | **10.86**       | **3,495,117** | **125 / 39 / 0** |

### What this means

- **`EnableSurfacePooling` is a massive win on this workload** and ships
  *off by default*:
  - **-53% allocations per frame**
  - **121 → 0 Gen2 GCs**
  - **-11% mean frame time**
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
| 1 | **Flip `EnableSurfacePooling` default to `true`** (or document loudly). Measured: -53% allocations, -11% frame, 0 Gen2 GCs. Need to confirm there's no correctness issue keeping it off. | very high | low if the only reason it's off is "didn't get around to it"; investigate before flipping |
| 2 | Cache / fast-path grapheme analysis for ASCII writes. `DisplayWidth.GetGraphemeAtIndex` + `TextSegmentationUtility.GetLengthOfFirstExtendedGraphemeCluster` are called for every cell — but the overwhelming majority of cells are ASCII (width 1, single byte). An ASCII fast path would skip the unicode segmentation entirely. | high | low |
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
dotnet run -c Release --project samples/PerfDemo --no-build -- --busy --pool

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
| `--busy-sidebar N` | 20 | Sidebar item count |
| `--busy-log N` | 20 | Scrolling log line count |
| `--busy-grid-rows N` | 8 | Dashboard grid rows |
| `--busy-grid-cols N` | 12 | Dashboard grid columns |
| `--width N` | 160 (busy) | Terminal width |
| `--height N` | 50 (busy) | Terminal height |
