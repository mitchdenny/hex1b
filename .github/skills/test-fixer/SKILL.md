---
name: test-fixer
description: Agent for diagnosing and fixing flaky terminal UI tests in the Hex1b test suite. Use when tests pass locally but fail in CI, or when tests exhibit timing-sensitive behavior.
---

# Test Fixer Skill

This skill provides guidelines for AI agents to diagnose and fix flaky tests in the Hex1b TUI library test suite. These tests use `Hex1bTerminalInputSequenceBuilder` to simulate user interactions with terminal applications.

## Quick Reference: Common Flaky Test Patterns

| Pattern | Symptom | Fix |
|---------|---------|-----|
| [Snapshot After Exit](#pattern-1-snapshot-captured-after-app-exit) | Test passes locally, fails on Linux CI | Move `WaitUntil` before `Capture`, ensure `Capture` is before exit |
| [Missing WaitUntil](#pattern-2-missing-waituntil-before-assertion) | Intermittent assertion failures | Add `WaitUntil` for expected state before `Capture` |
| [Race with Ctrl+C](#pattern-3-race-condition-with-ctrlc) | Snapshot missing expected content | Add `WaitUntil` between last action and `Capture` |
| [Task.WhenAny Race](#pattern-4-taskwhenany-race-condition) | Test sometimes times out | Replace with proper `WaitUntil` or increase timeout |
| [Test Interference](#pattern-5-test-interference) | Pass isolated, fail in suite | Check for shared state, file locks, or parallel execution issues |
| [Platform-Specific](#pattern-6-platform-specific-failures) | Fails consistently on Windows/Linux | Add platform skip trait or fix platform-specific code |
| [Task.Delay for Async Events](#pattern-7-taskdelay-for-async-events) | Flaky on slower CI runners | Replace `Task.Delay` with `TaskCompletionSource` signal |

---

## Pattern 1: Snapshot Captured After App Exit

### ⚠️ This is the most common flaky test issue

#### Symptoms
- Test passes consistently on Windows
- Test fails on Linux CI (GitHub Actions ubuntu-latest)
- Assertion fails with content that "should" be there
- Error messages like `Assert.True() Failure` or `An item should be selected with indicator`

#### Root Cause

The `ApplyWithCaptureAsync` method returns `terminal.CreateSnapshot()` **after all steps complete**, not at the point where `.Capture()` is called. When the sequence includes `Ctrl+C` to exit the app, the terminal buffer may be cleared before the final snapshot is taken.

**Platform difference**: Windows terminal buffers persist longer after app exit; Linux clears them more aggressively.

#### Example: Broken Test

```csharp
// ❌ BROKEN: Snapshot is taken AFTER Ctrl+C exits the app
var snapshot = await new Hex1bTerminalInputSequenceBuilder()
    .WaitUntil(s => s.ContainsText("Counter: 3"), TimeSpan.FromSeconds(2))
    .Capture("final")           // This saves SVG but doesn't capture for return!
    .Ctrl().Key(Hex1bKey.C)     // App exits, terminal buffer may be cleared
    .Build()
    .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
// snapshot is from AFTER Ctrl+C, not from Capture step!
Assert.True(snapshot.ContainsText("Counter: 3")); // May fail on Linux!
```

#### How It Works

1. `Hex1bTerminalInputSequenceBuilder` builds a sequence of `TestStep` objects
2. `ApplyWithCaptureAsync` executes all steps, then calls `terminal.CreateSnapshot()` at the end
3. `CaptureStep` only saves SVG/HTML files—it does NOT store the snapshot for return
4. When `Ctrl+C` triggers app exit, the terminal may clear its buffer before the final snapshot

#### Fix Strategy

**Option A: Ensure WaitUntil is immediately before Capture** (Recommended)

```csharp
// ✅ FIXED: WaitUntil confirms state, then Capture, then exit
var snapshot = await new Hex1bTerminalInputSequenceBuilder()
    .Key(Hex1bKey.A)
    .Key(Hex1bKey.B)
    .WaitUntil(s => s.ContainsText("Counter: 3"), TimeSpan.FromSeconds(2))
    .Capture("final")
    .Ctrl().Key(Hex1bKey.C)
    .Build()
    .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
await runTask;

// If the WaitUntil passed, we know the content was there at Capture time
// The assertion is now checking what WaitUntil already verified
Assert.True(snapshot.ContainsText("Counter: 3"));
```

**Option B: Don't use snapshot for content assertions**

```csharp
// ✅ ALTERNATIVE: Use render counters or other non-snapshot assertions
var runTask = app.RunAsync(TestContext.Current.CancellationToken);
await new Hex1bTerminalInputSequenceBuilder()
    .Key(Hex1bKey.A)
    .Key(Hex1bKey.B)
    .Capture("final")
    .Ctrl().Key(Hex1bKey.C)
    .Build()
    .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
await runTask;

// Assert on counters/state captured during execution, not snapshot content
Assert.Equal(1, staticRenderCount);
```

**Option C: Use WaitUntil as the assertion itself**

```csharp
// ✅ ALTERNATIVE: WaitUntil IS the assertion - if it passes, test passes
await new Hex1bTerminalInputSequenceBuilder()
    .WaitUntil(s => s.ContainsText("> Item 15"), TimeSpan.FromSeconds(2))
    .Ctrl().Key(Hex1bKey.C)
    .Build()
    .ApplyAsync(terminal, TestContext.Current.CancellationToken);
await runTask;
// No additional assertions needed - WaitUntil already verified the content
```

---

## Pattern 2: Missing WaitUntil Before Assertion

#### Symptoms
- Test sometimes fails, sometimes passes (even on same platform)
- Assertion checks for content that "should" appear after an action
- Content appears when running manually but not in fast automated tests

#### Root Cause

User input (key press, mouse click) triggers async rendering. Without `WaitUntil`, the snapshot may be captured before the render completes.

#### Example: Broken Test

```csharp
// ❌ BROKEN: No wait after Down() for selection to update
var snapshot = await new Hex1bTerminalInputSequenceBuilder()
    .WaitUntil(s => s.ContainsText("First"), TimeSpan.FromSeconds(2))
    .Down()  // Triggers async render
    .Capture("final")  // May capture before render completes!
    .Ctrl().Key(Hex1bKey.C)
    .Build()
    .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
Assert.True(snapshot.ContainsText("> Second")); // Flaky!
```

#### Fix

```csharp
// ✅ FIXED: Wait for expected state after action
var snapshot = await new Hex1bTerminalInputSequenceBuilder()
    .WaitUntil(s => s.ContainsText("First"), TimeSpan.FromSeconds(2))
    .Down()
    .WaitUntil(s => s.ContainsText("> Second"), TimeSpan.FromSeconds(2))  // Wait for render!
    .Capture("final")
    .Ctrl().Key(Hex1bKey.C)
    .Build()
    .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
Assert.True(snapshot.ContainsText("> Second")); // Reliable
```

---

## Pattern 3: Race Condition with Ctrl+C

#### Symptoms
- Test passes most of the time
- Occasionally fails with assertion that content is missing
- More common on slower CI runners

#### Root Cause

`Ctrl+C` can be processed before the previous action's render completes, especially if the action triggers complex layout recalculation.

#### Example: Broken Test

```csharp
// ❌ BROKEN: Scroll may not complete before Ctrl+C
var snapshot = await new Hex1bTerminalInputSequenceBuilder()
    .WaitUntil(s => s.ContainsText("Item 01"), TimeSpan.FromSeconds(2))
    .ScrollDown(5)
    .Capture("after_scroll")
    .Ctrl().Key(Hex1bKey.C)  // May interrupt scroll processing!
    .Build()
    .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
Assert.True(snapshot.ContainsText("> Item 06")); // Flaky!
```

#### Fix

```csharp
// ✅ FIXED: Verify scroll completed before exiting
var snapshot = await new Hex1bTerminalInputSequenceBuilder()
    .WaitUntil(s => s.ContainsText("Item 01"), TimeSpan.FromSeconds(2))
    .ScrollDown(5)
    .WaitUntil(s => s.ContainsText("> Item 06"), TimeSpan.FromSeconds(2))  // Verify scroll!
    .Capture("after_scroll")
    .Ctrl().Key(Hex1bKey.C)
    .Build()
    .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
Assert.True(snapshot.ContainsText("> Item 06")); // Reliable
```

---

## Test Infrastructure Overview

### Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `Hex1bTerminalInputSequenceBuilder` | `src/Hex1b/Automation/` | Fluent builder for test sequences |
| `Hex1bTerminalInputSequence` | `src/Hex1b/Automation/` | Executes steps and returns final snapshot |
| `CaptureStep` | `tests/Hex1b.Tests/CaptureStep.cs` | Saves SVG/HTML at capture point |
| `WaitUntilStep` | `src/Hex1b/Automation/WaitUntilStep.cs` | Polls until condition met or timeout |
| `TestSequenceExtensions` | `tests/Hex1b.Tests/TestSequenceExtensions.cs` | Adds `.Capture()` extension method |

### Understanding ApplyWithCaptureAsync

```csharp
// From Hex1bTerminalInputSequence.cs
public async Task<Hex1bTerminalSnapshot> ApplyAsync(Hex1bTerminal terminal, CancellationToken ct = default)
{
    foreach (var step in _steps)
    {
        ct.ThrowIfCancellationRequested();
        await step.ExecuteAsync(terminal, _options, ct);
    }
    return terminal.CreateSnapshot();  // ⚠️ Snapshot is ALWAYS at the end!
}
```

The `CaptureStep` only saves files—it does NOT affect what `ApplyAsync` returns:

```csharp
// From CaptureStep.cs
internal override Task ExecuteAsync(...)
{
    TestCaptureHelper.Capture(terminal, Name);  // Saves SVG/HTML only
    return Task.CompletedTask;
}
```

---

## Diagnostic Workflow

### Step 1: Identify the Failure Pattern

```bash
# Get CI logs
gh run view <run-id> --log-failed

# Look for test names and error messages
# Common patterns:
# - Assert.True() Failure
# - Expected: True / Actual: False
# - "An item should be selected with indicator"
```

### Step 2: Check Local vs CI Behavior

```powershell
# Run specific test locally
dotnet test tests/Hex1b.Tests --filter "FullyQualifiedName~<TestName>"

# Run multiple times to check for flakiness
for ($i = 1; $i -le 10; $i++) { 
    dotnet test tests/Hex1b.Tests --filter "FullyQualifiedName~<TestName>" --no-build 2>&1 | 
    Select-String -Pattern "(Passed|Failed)" 
}
```

### Step 3: Examine the Test Pattern

Look for these anti-patterns:

```csharp
// Anti-pattern 1: Snapshot assertion after Ctrl+C
.Capture("final")
.Ctrl().Key(Hex1bKey.C)
// ... later ...
Assert.True(snapshot.ContainsText("expected"));  // ❌

// Anti-pattern 2: Action without WaitUntil before Capture
.Down()
.Capture("final")  // ❌ No WaitUntil after Down()

// Anti-pattern 3: Complex action right before Ctrl+C
.ScrollDown(10)
.Capture("final")
.Ctrl().Key(Hex1bKey.C)  // ❌ No WaitUntil to verify scroll completed
```

### Step 4: Apply the Fix

1. Add `WaitUntil` after any action that changes state
2. Ensure `WaitUntil` checks for the exact condition being asserted
3. Place `WaitUntil` immediately before `Capture`
4. Consider if the assertion is even needed (WaitUntil already verified it)

---

## Safe Test Patterns

### Pattern A: Full Integration Test with Exit

```csharp
[Fact]
public async Task Navigation_DownArrow_SelectsNextItem()
{
    using var workload = new Hex1bAppWorkloadAdapter();
    using var terminal = Hex1bTerminal.CreateBuilder()
        .WithWorkload(workload)
        .WithHeadless()
        .WithDimensions(40, 10)
        .Build();
    
    using var app = new Hex1bApp(
        ctx => Task.FromResult<Hex1bWidget>(ctx.List(["First", "Second", "Third"])),
        new Hex1bAppOptions { WorkloadAdapter = workload }
    );

    var runTask = app.RunAsync(TestContext.Current.CancellationToken);
    
    var snapshot = await new Hex1bTerminalInputSequenceBuilder()
        .WaitUntil(s => s.ContainsText("> First"), TimeSpan.FromSeconds(2))  // Initial state
        .Down()
        .WaitUntil(s => s.ContainsText("> Second"), TimeSpan.FromSeconds(2)) // After action
        .Capture("after_down")
        .Ctrl().Key(Hex1bKey.C)
        .Build()
        .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
    
    await runTask;
    
    // This assertion is technically redundant since WaitUntil verified it
    Assert.True(snapshot.ContainsText("> Second"));
}
```

### Pattern B: Render Count Test (No Snapshot Assertions)

```csharp
[Fact]
public async Task StaticWidget_OnlyRendersOnce()
{
    using var workload = new Hex1bAppWorkloadAdapter();
    using var terminal = Hex1bTerminal.CreateBuilder()
        .WithWorkload(workload)
        .WithHeadless()
        .Build();
    
    var renderCount = 0;
    var staticWidget = new TestWidget().OnRender(_ => renderCount++);

    using var app = new Hex1bApp(
        ctx => Task.FromResult<Hex1bWidget>(staticWidget),
        new Hex1bAppOptions { WorkloadAdapter = workload }
    );

    var runTask = app.RunAsync(TestContext.Current.CancellationToken);
    
    await new Hex1bTerminalInputSequenceBuilder()
        .Key(Hex1bKey.A)
        .Key(Hex1bKey.B)
        .Capture("final")
        .Ctrl().Key(Hex1bKey.C)
        .Build()
        .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
    
    await runTask;
    
    // Assert on counter, not snapshot content
    Assert.Equal(1, renderCount);
}
```

### Pattern C: Unit Test Without App Exit

```csharp
[Fact]
public async Task Render_AllItems_AreVisible()
{
    using var workload = new Hex1bAppWorkloadAdapter();
    using var terminal = Hex1bTerminal.CreateBuilder()
        .WithWorkload(workload)
        .WithHeadless()
        .Build();
    
    var context = CreateContext(workload);
    var node = new ListNode { Items = ["Item 1", "Item 2", "Item 3"] };
    node.Arrange(new Rect(0, 0, 20, 5));
    node.Render(context);
    
    // No app, no Ctrl+C needed
    var snapshot = await new Hex1bTerminalInputSequenceBuilder()
        .WaitUntil(s => 
            s.ContainsText("Item 1") && 
            s.ContainsText("Item 2") && 
            s.ContainsText("Item 3"), 
            TimeSpan.FromSeconds(2))
        .Capture("final")
        .Build()
        .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
    
    Assert.True(snapshot.ContainsText("Item 1"));
    Assert.True(snapshot.ContainsText("Item 2"));
    Assert.True(snapshot.ContainsText("Item 3"));
}
```

---

## Pattern 4: Task.WhenAny Race Condition

#### Symptoms
- Test uses `Task.WhenAny` to check if app exited
- Test sometimes times out even though behavior is correct
- Inconsistent pass/fail across runs

#### Root Cause

Using `Task.WhenAny(runTask, Task.Delay(...))` creates a race condition. The delay may win even when the app is about to exit, or the exit may happen but not be detected in time.

#### Example: Broken Test

```csharp
// ❌ BROKEN: Race condition with Task.WhenAny
var runTask = app.RunAsync(TestContext.Current.CancellationToken);

await renderTest.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

await new Hex1bTerminalInputSequenceBuilder()
    .Key(Hex1bKey.C, Hex1bModifiers.Control)
    .Build()
    .ApplyAsync(terminal);

var completed = await Task.WhenAny(runTask, Task.Delay(2000));
Assert.True(completed == runTask, "Expected CTRL-C to exit");  // Flaky!
```

#### Fix

```csharp
// ✅ FIXED: Use WaitAsync with timeout instead of Task.WhenAny
var runTask = app.RunAsync(TestContext.Current.CancellationToken);

await new Hex1bTerminalInputSequenceBuilder()
    .WaitUntil(s => s.ContainsText("expected content"), TimeSpan.FromSeconds(2))
    .Ctrl().Key(Hex1bKey.C)
    .Build()
    .ApplyAsync(terminal, TestContext.Current.CancellationToken);

// Use WaitAsync - throws TimeoutException if it takes too long
await runTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
```

---

## Pattern 5: Test Interference

#### Symptoms
- Test passes when run in isolation: `dotnet test --filter "FullyQualifiedName~TestName"`
- Test fails when run with full suite
- Failures appear random or depend on test execution order

#### Root Cause

Tests may interfere with each other through:
1. **Shared static state** - Singletons, static fields
2. **File system conflicts** - Temp files with same names
3. **Port conflicts** - Multiple tests binding to same port
4. **Resource exhaustion** - Too many terminals/processes open

#### Diagnosis

```powershell
# Run test in isolation
dotnet test --filter "FullyQualifiedName~TestName" --no-build

# Run test with suspected interfering tests
dotnet test --filter "FullyQualifiedName~TestName|FullyQualifiedName~OtherTest" --no-build
```

#### Fix Strategies

1. **Use unique temp paths** - Include test name or GUID in temp file paths
2. **Properly dispose resources** - Ensure `using` statements on terminals/workloads
3. **Avoid static state** - Use instance fields or dependency injection
4. **Add xUnit collection** - Force sequential execution for conflicting tests

```csharp
// Use unique temp file names
var tempPath = Path.Combine(Path.GetTempPath(), $"hex1b_test_{Guid.NewGuid()}.cast");

// Or include test name
var tempPath = Path.Combine(Path.GetTempPath(), $"hex1b_{nameof(MyTestMethod)}.cast");
```

---

## Pattern 6: Platform-Specific Failures

#### Symptoms
- Test always fails on Windows but passes on Linux (or vice versa)
- Error mentions platform-specific features (PTY, console mode, etc.)
- Test timeout with empty terminal state on one platform

#### Common Platform Differences

| Feature | Windows | Linux |
|---------|---------|-------|
| PTY support | Limited (ConPTY) | Native |
| Terminal buffer cleanup | Delayed | Immediate |
| File locking | Strict | Flexible |
| Path separators | `\` | `/` |

#### Fix: Add Platform Skip Trait

```csharp
// Skip on Windows - PTY tests require Unix
[Fact]
[Trait("Category", "LinuxOnly")]
public async Task WithPtyProcess_ExecutesProcess()
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        // Skip on Windows - PTY not fully supported
        return;
    }
    // ... test code
}

// Or use Skip property
[Fact(Skip = "Requires Unix PTY support")]
public async Task PtyTest() { }

// Or conditional skip with xUnit
public class LinuxOnlyFactAttribute : FactAttribute
{
    public LinuxOnlyFactAttribute()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip = "This test requires Linux";
        }
    }
}
```

#### Tests Known to Require Linux

- `Hex1bTerminalBuilderTests.WithPtyProcess_*` - Require PTY
- `NanoExploratoryTests.*` - Require `nano` and PTY
- Tests using `WithPtyProcess()` builder method

---

## Pattern 7: Task.Delay for Async Events

#### Symptoms
- Test passes locally most of the time
- Test fails intermittently on CI, especially on slower runners
- Test waits a fixed time (e.g., `Task.Delay(100)`) for an async event like input binding firing
- Error message suggests the action never occurred (e.g., "Expected binding to fire")

#### Root Cause

Using `Task.Delay` to wait for async events like input bindings firing is unreliable. The fixed delay may not be long enough on slower CI runners, or may be unnecessarily long on fast machines.

**Example**: Input binding tests that send a key and wait for the binding callback to fire.

#### Example: Broken Test

```csharp
// ❌ BROKEN: Fixed delay may not be long enough on slow CI runners
var bindingFired = false;

using var app = new Hex1bApp(
    ctx =>
    {
        var vstack = new VStackWidget([ctx.Test()])
            .WithInputBindings(bindings =>
            {
                bindings.Shift().Key(key).Action(_ =>
                {
                    bindingFired = true;  // Sets flag when binding fires
                    return Task.CompletedTask;
                }, $"Test Shift+{key}");
            });

        return Task.FromResult<Hex1bWidget>(vstack);
    },
    new Hex1bAppOptions { WorkloadAdapter = workload }
);

// ... wait for render ...

await new Hex1bTerminalInputSequenceBuilder()
    .Shift().Key(key)
    .Build()
    .ApplyAsync(terminal, TestContext.Current.CancellationToken);

await Task.Delay(100);  // ❌ Fixed delay - may not be long enough!

Assert.True(bindingFired);  // Flaky!
```

#### Fix

Replace the boolean flag and `Task.Delay` with a `TaskCompletionSource` that signals when the event occurs:

```csharp
// ✅ FIXED: Use TaskCompletionSource to wait for the async event
var bindingFired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

using var app = new Hex1bApp(
    ctx =>
    {
        var vstack = new VStackWidget([ctx.Test()])
            .WithInputBindings(bindings =>
            {
                bindings.Shift().Key(key).Action(_ =>
                {
                    bindingFired.TrySetResult();  // Signal completion
                    return Task.CompletedTask;
                }, $"Test Shift+{key}");
            });

        return Task.FromResult<Hex1bWidget>(vstack);
    },
    new Hex1bAppOptions { WorkloadAdapter = workload }
);

// ... wait for render ...

await new Hex1bTerminalInputSequenceBuilder()
    .Shift().Key(key)
    .Build()
    .ApplyAsync(terminal, TestContext.Current.CancellationToken);

// Wait for the event with a timeout - will fail fast if binding doesn't fire
await bindingFired.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

// If we got here, the binding fired (the wait would have timed out otherwise)
Assert.True(bindingFired.Task.IsCompleted);  // Reliable
```

#### Key Points

1. **Use `TaskCompletionSource`** instead of a boolean flag
2. **Call `TrySetResult()`** in the event handler to signal completion
3. **Use `WaitAsync` with timeout** instead of `Task.Delay`
4. **Use `TaskCreationOptions.RunContinuationsAsynchronously`** to avoid potential deadlocks
5. The timeout should be generous (2+ seconds) to account for slow CI runners

---

## Checklist for Test Review

Before committing test changes, verify:

- [ ] Every action that changes UI state is followed by `WaitUntil`
- [ ] `WaitUntil` condition matches what will be asserted
- [ ] `Capture` is placed after `WaitUntil`, before `Ctrl+C`
- [ ] Snapshot assertions don't rely on post-exit terminal state
- [ ] Consider if assertion is redundant (WaitUntil already verified it)
- [ ] Test runs reliably 10+ times locally
- [ ] Test doesn't have timing dependencies (uses `WaitUntil`, not `Wait`)
- [ ] No `Task.WhenAny` race conditions - use `WaitAsync` instead
- [ ] No `Task.Delay` for async events - use `TaskCompletionSource` instead
- [ ] Test passes both in isolation and in full suite
- [ ] Platform-specific tests have appropriate skip traits
