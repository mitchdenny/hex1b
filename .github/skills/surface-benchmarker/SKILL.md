---
name: surface-benchmarker
description: Guidelines for running and interpreting Surface API performance benchmarks. Use when modifying code in src/Hex1b/Surfaces/ to ensure performance is not regressed.
---

# Surface Benchmarker Skill

This skill provides guidelines for AI agents to run and interpret performance benchmarks for the Hex1b Surface API. **Run benchmarks whenever you modify code in `src/Hex1b/Surfaces/`** to ensure performance is not regressed.

## Quick Reference

| Action | Command |
|--------|---------|
| Run all Surface benchmarks | `dotnet run -c Release --project benchmarks/Hex1b.Benchmarks -- --filter "Surface*"` |
| Run specific benchmark | `dotnet run -c Release --project benchmarks/Hex1b.Benchmarks -- --filter "*WriteText*"` |
| Quick dry-run (sanity check) | `dotnet run -c Release --project benchmarks/Hex1b.Benchmarks -- --filter "Surface*" --job dry` |

---

## When to Run Benchmarks

**ALWAYS run benchmarks after modifying:**

| File/Area | Critical Benchmarks |
|-----------|---------------------|
| `SurfaceCell.cs` | All (cells are used everywhere) |
| `Surface.cs` | `CreateSurface_*`, `WriteText_*`, `Fill_*`, `Clone_*` |
| `CompositeSurface.cs` | `CompositeSurface_*` |
| `SurfaceComparer.cs` | `Compare_*`, `ToTokens_*`, `ToAnsiString_*` |
| `SurfaceDiff.cs` | `Compare_*` |
| `ComputeContext.cs` | `CompositeSurface_*` (computed cells use this) |

---

## Benchmark Harness Architecture

### Project Structure

```
benchmarks/Hex1b.Benchmarks/
├── Hex1b.Benchmarks.csproj    # Console app with BenchmarkDotNet
├── Program.cs                  # Entry point
└── SurfaceBenchmarks.cs        # Surface API benchmarks
```

### How BenchmarkDotNet Works

BenchmarkDotNet is a .NET performance benchmarking library that:

1. **Warms up** the JIT by running the benchmark multiple times before measuring
2. **Runs multiple iterations** to get statistically significant results
3. **Reports mean, median, standard deviation** for each benchmark
4. **Tracks memory allocations** (when `[MemoryDiagnoser]` is enabled)

### Benchmark Lifecycle

```
┌─────────────────────────────────────────────────────────────────┐
│ 1. GlobalSetup                                                  │
│    - Runs once before all benchmarks                            │
│    - Creates pre-allocated surfaces, diffs, etc.                │
│    - Sets up shared test data                                   │
└─────────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│ 2. For each [Benchmark] method:                                 │
│    a. Warmup phase (JIT compilation, cache warming)             │
│    b. Pilot phase (determine optimal iteration count)           │
│    c. Actual measurements (multiple iterations)                 │
│    d. Results aggregation                                       │
└─────────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│ 3. Report Generation                                            │
│    - Console output with statistics                             │
│    - Optional: HTML, CSV, JSON exports                          │
└─────────────────────────────────────────────────────────────────┘
```

### Key Benchmark Attributes

| Attribute | Purpose |
|-----------|---------|
| `[MemoryDiagnoser]` | Reports memory allocations (Gen0/1/2 collections, allocated bytes) |
| `[Benchmark]` | Marks a method as a benchmark |
| `[GlobalSetup]` | Runs once before all benchmarks to set up shared state |
| `[Params(80, 160, 320)]` | Parameterizes benchmarks with multiple values |

---

## Benchmark Categories

### Surface Creation (`CreateSurface_*`)

Measures allocation and initialization cost for surfaces of various sizes.

```csharp
[Benchmark]
public Surface CreateSurface_Small() => new Surface(80, 24);    // Standard terminal
public Surface CreateSurface_Medium() => new Surface(160, 48);  // Large terminal
public Surface CreateSurface_Large() => new Surface(320, 96);   // Very large
public Surface CreateSurface_4K() => new Surface(480, 135);     // 4K equivalent
```

**What to watch for:**
- Memory allocation should scale linearly with cell count
- No unexpected allocations from internal data structures

### Text Writing (`WriteText_*`)

Measures grapheme parsing and wide character handling.

```csharp
[Benchmark]
public void WriteText_Short();      // "Hello"
public void WriteText_Medium();     // Standard sentence
public void WriteText_Long();       // Long paragraph
public void WriteText_WideChars();  // Chinese characters (2 cells each)
public void WriteText_FillScreen(); // 24 lines of text
```

**What to watch for:**
- Wide characters should not be drastically slower than ASCII
- `FillScreen` should scale linearly

### Fill Operations (`Fill_*`)

Measures bulk cell assignment.

```csharp
[Benchmark]
public void Fill_SmallRect();   // 20x10 region
public void Fill_FullScreen();  // 80x24
public void Fill_LargeScreen(); // 320x96
```

**What to watch for:**
- Should approach memory bandwidth limits for large fills
- Minimal overhead per cell

### Compositing (`Composite_*`, `CompositeSurface_*`)

Measures layer merging and transparency resolution.

```csharp
[Benchmark]
public void Composite_SmallOntoSmall();         // 20x10 onto 80x24
public void Composite_MediumOntoLarge();        // 80x24 onto 320x96
public Surface CompositeSurface_Flatten_3Layers();  // Resolve 3 layers
public SurfaceCell CompositeSurface_GetCell_Resolved();  // Single cell lookup
public void CompositeSurface_GetAllCells();     // 80x24 = 1920 lookups
```

**What to watch for:**
- `Flatten` should be O(layers × cells)
- Single cell lookup should be fast for interactive rendering

### Diff Comparison (`Compare_*`)

Measures change detection.

```csharp
[Benchmark]
public SurfaceDiff Compare_FullDiff();    // 100% of cells changed
public SurfaceDiff Compare_SparseDiff();  // ~10% of cells changed
public SurfaceDiff Compare_NoDiff();      // 0% changed
public SurfaceDiff CompareToEmpty();      // Initial render
```

**What to watch for:**
- `NoDiff` should be very fast (early exit when possible)
- Memory allocation should be proportional to changed cell count

### Token Generation (`ToTokens_*`, `ToAnsiString_*`)

Measures ANSI escape sequence generation.

```csharp
[Benchmark]
public IReadOnlyList<AnsiToken> ToTokens_FullDiff();  // Generate tokens
public string ToAnsiString_FullDiff();                // Tokens → string
public string ToAnsiString_SparseDiff();              // Fewer changes
```

**What to watch for:**
- String building should be efficient
- SGR optimization should reduce output size

---

## Running Benchmarks

### Full Benchmark Suite

```bash
cd /home/midenn/Code/hex1b
dotnet run -c Release --project benchmarks/Hex1b.Benchmarks -- --filter "Surface*"
```

**Expected runtime:** 5-15 minutes depending on hardware.

### Quick Validation (Dry Run)

```bash
dotnet run -c Release --project benchmarks/Hex1b.Benchmarks -- --filter "Surface*" --job dry
```

This runs fewer iterations to quickly verify benchmarks work without waiting for full statistical accuracy.

### Specific Benchmark Groups

```bash
# Only WriteText benchmarks
dotnet run -c Release --project benchmarks/Hex1b.Benchmarks -- --filter "*WriteText*"

# Only Diff/Token generation
dotnet run -c Release --project benchmarks/Hex1b.Benchmarks -- --filter "*Compare*|*Token*|*Ansi*"

# Single benchmark
dotnet run -c Release --project benchmarks/Hex1b.Benchmarks -- --filter "*Fill_FullScreen*"
```

---

## Interpreting Results

### Reading the Output

```
| Method                | Mean       | Error    | StdDev   | Gen0   | Allocated |
|-----------------------|------------|----------|----------|--------|-----------|
| CreateSurface_Small   | 12.34 μs   | 0.15 μs  | 0.14 μs  | 1.2345 | 15.36 KB  |
| CreateSurface_Large   | 98.76 μs   | 1.23 μs  | 1.15 μs  | 9.8765 | 122.88 KB |
```

| Column | Meaning |
|--------|---------|
| **Mean** | Average execution time |
| **Error** | Half of 99.9% confidence interval |
| **StdDev** | Standard deviation (lower = more consistent) |
| **Gen0** | Gen0 garbage collections per 1000 operations |
| **Allocated** | Bytes allocated per operation |

### Performance Expectations

| Benchmark | Expected Range | Alert If |
|-----------|---------------|----------|
| `CreateSurface_Small` (80×24) | 5-20 μs | > 50 μs |
| `CreateSurface_4K` (480×135) | 50-200 μs | > 500 μs |
| `WriteText_Short` | 0.5-2 μs | > 5 μs |
| `WriteText_WideChars` | 1-5 μs | > 10 μs |
| `Fill_FullScreen` | 2-10 μs | > 25 μs |
| `Compare_NoDiff` | 5-20 μs | > 50 μs |
| `Compare_SparseDiff` | 10-50 μs | > 100 μs |
| `ToAnsiString_SparseDiff` | 20-100 μs | > 250 μs |

**Note:** These are rough guidelines. Actual values depend on hardware.

### Comparing Before/After

When making changes:

1. **Before changes:** Run benchmarks and save output
2. **Make changes**
3. **After changes:** Run benchmarks again
4. **Compare:** Look for significant regressions (>20% slower)

```bash
# Save before results
dotnet run -c Release --project benchmarks/Hex1b.Benchmarks -- \
    --filter "Surface*" --exporters json > before.json

# Make changes...

# Compare (manual inspection)
dotnet run -c Release --project benchmarks/Hex1b.Benchmarks -- \
    --filter "Surface*" --exporters json > after.json
```

---

## Common Performance Issues

### Issue 1: Excessive Allocations

**Symptom:** High `Allocated` column, many Gen0 collections.

**Common causes:**
- Creating new arrays/lists inside hot loops
- Boxing value types (e.g., storing `SurfaceCell` in `object`)
- String concatenation without `StringBuilder`

**Fix strategies:**
- Use `ArrayPool<T>.Shared` for temporary buffers
- Use `Span<T>` and stack allocation for small buffers
- Pre-allocate and reuse collections

### Issue 2: Poor Cache Locality

**Symptom:** Large surfaces are disproportionately slower than small ones.

**Common causes:**
- Column-major access pattern on row-major data
- Jumping around in memory instead of sequential access

**Fix strategies:**
- Access cells in row-major order (for y, then for x)
- Process contiguous memory regions when possible

### Issue 3: Redundant Work

**Symptom:** Operations are slower than expected for the amount of data.

**Common causes:**
- Recalculating values that could be cached
- Not short-circuiting when result is known
- Unnecessary defensive copies

**Fix strategies:**
- Cache computed values (e.g., display width for text)
- Early exit when no changes detected
- Use `readonly` and `in` parameters to avoid copies

---

## Adding New Benchmarks

When adding new functionality to the Surface API, add corresponding benchmarks:

```csharp
[Benchmark]
public void NewFeature_TypicalCase()
{
    // Benchmark the common case
    _surface.NewMethod(typicalInput);
}

[Benchmark]
public void NewFeature_WorstCase()
{
    // Benchmark the worst case for regression detection
    _surface.NewMethod(worstCaseInput);
}
```

### Guidelines for New Benchmarks

1. **Use pre-allocated data** in `[GlobalSetup]` - don't measure setup time
2. **Return or use the result** - prevent dead code elimination
3. **Benchmark realistic scenarios** - not just micro-benchmarks
4. **Include edge cases** - empty inputs, large inputs, worst-case patterns

---

## CI Integration (Future)

Benchmarks can be integrated into CI to catch regressions:

```yaml
# .github/workflows/benchmarks.yml (example)
- name: Run benchmarks
  run: |
    dotnet run -c Release --project benchmarks/Hex1b.Benchmarks -- \
      --filter "Surface*" --exporters json
    
- name: Compare with baseline
  run: |
    # Compare against stored baseline, fail if regression > 20%
```

---

## Checklist for Surface API Changes

Before merging changes to `src/Hex1b/Surfaces/`:

- [ ] Run relevant benchmarks before and after changes
- [ ] No benchmark regressed by more than 20%
- [ ] Memory allocations did not significantly increase
- [ ] Add benchmarks for any new public methods
- [ ] Document any intentional performance tradeoffs
