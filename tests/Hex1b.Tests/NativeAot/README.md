# Native AOT scenarios

Native AOT scenarios run inside the normal **managed `Hex1b.Tests` MSTest
runner**. Each test embeds its native program as a raw source string. A helper
writes a separate temporary console project against the current Hex1b project,
publishes it, and starts its native executable directly. The runner itself is
not Native AOT-published, and there are no checked-in consumer projects.

The normal multi-platform and daily CI test workflows set
`HEX1B_RUN_NATIVE_AOT_TESTS=1`, so native scenarios run alongside existing tests.
Locally, the costly native scenarios are disabled by default with an
explicit MSTest condition/skip message. Set the same switch to opt in; merely
filtering by `NativeAot` does not enable them. Fast parser and infrastructure
tests run normally without this switch.
If a filter selects only disabled scenarios, MTP reports zero executed tests
(exit code 8); enable the switch before running a native-only filter.

## Prerequisites

Use a .NET 10 SDK with Native AOT support and an x64 or arm64 test-runner process.
The suite targets the host OS and **process** architecture; it does not perform
cross-compilation. SDK commands execute from the repository root, respecting its
`global.json` without introducing an SDK pin.

- **Windows:** Visual Studio 2022 or Build Tools with **Desktop development with
  C++**, the matching MSVC x64/arm64 toolchain, and a Windows SDK.
- **Linux:** a C compiler, linker, `make`, and Native AOT's development libraries.
  On Ubuntu/Debian, install `build-essential`, `clang`, and `zlib1g-dev`;
  other distributions need equivalent packages.
- **macOS:** Xcode command-line tools (`xcode-select --install`).
- Restore access to NuGet feeds is needed for MSTest, the library dependencies,
  and the SDK's Native AOT compiler/runtime packs. `git` and `dotnet` must be on
  `PATH`, and the checkout must retain its Git metadata.

When enabled, missing prerequisites, unsupported hosts, publish errors,
missing/invalid maps, runtime crashes, nonzero exits, missing success markers, and stalls **fail** the
test. They are not skipped. Publish has a ten-minute timeout, executable execution
has a thirty-second timeout, and native behavior checks have their own bounded
waits. Test cancellation or timeouts kill only the test-owned process tree and
retain command/stdout/stderr logs.

## Running locally

From the repository root, normal local tests require no extra switch. To opt
into native scenarios on a properly configured machine (PowerShell):

```powershell
$env:HEX1B_RUN_NATIVE_AOT_TESTS = '1'
dotnet test --project tests\Hex1b.Tests\Hex1b.Tests.csproj -c Release -- --no-progress --filter "TestCategory=NativeAot"
```

Run just the existing-terminal consumer:

```powershell
$env:HEX1B_RUN_NATIVE_AOT_TESTS = '1'
dotnet test --project tests\Hex1b.Tests\Hex1b.Tests.csproj -c Release -- --no-progress --filter "FullyQualifiedName~NativeAotScenarioTests.ExistingTerminalTape"
```

Run only fast map-reader tests, without Native AOT publishing:

```powershell
dotnet test --project tests\Hex1b.Tests\Hex1b.Tests.csproj -c Release -- --no-progress --filter "FullyQualifiedName~NativeMapTests"
```

`TestCategory=NativeAot` selects all costly scenarios, including the console
measurement (which publishes multiple matched consumers);
`TestCategory=Infrastructure` selects process/publish diagnostics tests, including
test-owned OS-shell stall fixtures for timeout/cancellation cleanup.
To disable native scenarios again in PowerShell:

```powershell
Remove-Item Env:HEX1B_RUN_NATIVE_AOT_TESTS
```

On Unix, use `/` instead of `\` in command-line project paths:

```bash
HEX1B_RUN_NATIVE_AOT_TESTS=1 dotnet test --project tests/Hex1b.Tests/Hex1b.Tests.csproj -c Release -- --no-progress --filter 'TestCategory=NativeAot'
```

The costly class is `DoNotParallelize`, despite the repository's method-parallel
MSTest default. Each selected consumer is published once per invocation, with
serialized reference-project builds. Avoid running multiple native suites
simultaneously in the **same checkout**, because library reference projects use
their normal shared output paths.

## Scenarios and retention contracts

| Consumer | Behavior verified | Required emitted code | Forbidden emitted code |
|---|---|---|---|
| `DirectTerminal` | Raw terminal construction, automated input, screen echo, workload disposal | Terminal | Tape player, terminal builder, child-process implementation |
| `ExistingTerminalTape` | Type/screen-Wait playback, final snapshot, continued terminal use after result disposal, caller cleanup | Terminal and existing-terminal playback | Terminal builder, child-process implementation, `ConfigureOwnedBuilder`, created-terminal playback |
| `CreatedTerminalTape` | Builder factory called once, Type/screen-Wait playback, final snapshot and owned workload cleanup | Terminal, created-terminal playback, terminal builder | None: other creation-related code may legitimately remain |
| `HeadlessConsoleBaseline` | Real text and bar-chart rendering, snapshot dimensions/content/colors and SVG export | Terminal, builder, app, chart, console construction and adapter | None: records main's existing retention |
| `HeadlessConsoleCopiedSourcesBaseline` | Same program and snapshots with unmodified construction files copied into experimental compile inputs | Same as baseline | None: controls for the source-replacement apparatus |
| `HeadlessConsoleAppFallbackRemoved` | Same program and byte-identical SVGs with only the app fallback removed experimentally | Same as baseline: the builder still roots console code | None |
| `HeadlessConsoleBothConstructionPathsRemoved` | Same program and byte-identical SVGs with both console construction paths removed experimentally | Terminal, builder, app, chart | Console adapter, console construction, Windows/Unix console driver |
| `InteractiveConsole` / `InteractiveConsoleAfterExperiment` | Default builder and standalone app render in a real PTY; parent sends Ctrl+C only after each visible marker; both exit normally | Terminal, builder, app, console construction and adapter | None |

The original three consumers use the same small in-memory echo workload source,
not a real shell or process. Console scenarios use the app workload. The
interactive control runs in a test-owned PTY (Windows proxy/ConPTY, Unix PTY),
not the runner's console. Consumers contain no scenario dispatcher, reflection, MSTest,
or assertion-framework dependencies. Each emits its own `NATIVE_AOT_OK:<name>`
marker only after verifying behavior; an exception/nonzero exit is a failure.

The generated **consumer-scoped** `Directory.Build.props` imports root props and
sets `PublishAot=true`, `OptimizationPreference=Size`, and
`IlcGenerateMapFile=true`. These are deliberately not global command-line
properties: global `PublishAot` propagates into the repository's netstandard
analyzer project and fails with `NETSDK1207`. Root analyzers remain enabled.
Likewise, only the generated consumer props set intermediate, build, and publish
paths; no global `BaseIntermediateOutputPath` is applied to library/native-helper
project references. XML paths are escaped using standard XML APIs. Each project
has its own entry point; no shared dispatcher or scenario mode is introduced.

Normal library native asset generation and copying remain enabled. The suite
does not disable PTY helpers or delete runtime assets to reduce measured size.
The inventory reports whatever the normal project-reference publish actually
includes. A helper being built does not necessarily mean it is copied into the
consumer's publish directory.

## Maps, sizes, and evidence

The runner requires exactly one compiler `*.map.xml` with an `ObjectNodes` root
and valid nonempty `MethodCode` entries. It parses XML using the standard XML
reader, not text searches. Retention checks match narrowly scoped **declaring**
Hex1b families, including generated state-machine/closure methods and nested
native helpers. Metadata entries and generic signatures mentioning those types
do not establish emitted implementations. Missing positive controls fail rather
than making absence checks vacuously pass. Unsupported compiler map formats fail
explicitly and need a reviewed parser update.

Each invocation preserves evidence under the existing ignored path:

```text
TestResults/NativeAot/<UTC-time-and-unique-run-id>/<scenario>/
  report.json
  revision.log, git-status.log, sdk.log
  publish.log, publish.binlog, runtime.log
  source/<scenario>.csproj, Directory.Build.props, Program.cs, EchoWorkload.cs
  obj/.../native/<scenario>.map.xml
  bin/...
  publish/...
```

Reports, logs, and generated sources/projects are attached with
`TestContext.AddResultFile`; the JSON report also appears in TestContext
diagnostics. Both CI workflows upload `TestResults` including native evidence.
Reports record the scenario, revision
and dirty state, SDK/compiler package, host RID/TFM/configuration/publish
settings, executable bytes/SHA-256, the untrimmed library input hash and assembly
identity (with project/package identity), total MethodCode count/length,
required/forbidden family counts/lengths, runtime outcome, and evidence paths.
The full publish inventory records bytes and hashes, distinguishing the main
executable from symbols, documentation, native helpers, and runtime support.
Failed runs retain partial reports and command diagnostics too.

The console comparison additionally records the consumer source hash, explicit
experimental source hashes, and a `console-comparison.json` beside the baseline
report. Each experiment keeps the same consumer source, assembly name and success
marker. Text/chart SVGs are preserved beside each report and compared byte for
byte. The comparison lists removed declaring methods and lengths by console
family; a generic signature mentioning a console type is not counted.

Executable sizes are **observations, not byte budgets**. There are no fixed or
configurable size limits, historical build database, or automatic baseline
updates. To claim an optimization, compare the **same scenario** with matched
SDK/compiler, RID, configuration and publish settings across revisions, using
the reports and library input hashes. Cross-scenario differences alone do not
prove an optimization. Dirty checkouts, debug symbols, support assets, and
compiler changes need to be accounted for separately.

## Headless console isolation experiment (#708)

Run the independently filterable measurement:

```powershell
$env:HEX1B_RUN_NATIVE_AOT_TESTS = '1'
dotnet test --project tests\Hex1b.Tests\Hex1b.Tests.csproj -c Release -- --no-progress --filter "FullyQualifiedName~HeadlessConsoleTests.HeadlessTextAndChart"
```

This is **test-only measurement**, not a new supported construction path.
Production library files, public signatures, interactive defaults and surface
pooling are unchanged (pooling remains enabled with the default 8 surfaces per
bucket and 120 idle frames; emitted pool code is a positive control). The test copies `Hex1bApp.cs` and
`Hex1bTerminalBuilder.cs` under the ignored evidence directory. A generated,
library-only MSBuild target substitutes these compile inputs before
`AssignTargetPaths`, so incremental compile dependency hashes see the real
inputs. Normal analyzers, native-helper generation and publish assets stay
enabled. The generated consumer excludes the copied library sources from its
own compile items. No global output-directory overrides or ILC substitutions
are used.

The app-only experiment replaces the constructor's adapter-or-console block
with a requirement for an already-supplied adapter. The both-path experiment
also replaces the builder's default console factory with a throwing factory.
Those guards must never be reached by this headless consumer. They deliberately
would not support interactive callers, so **do not use these replacements in
production**. The builder's direct console `WithReflow` type-check/call is left
intact, letting Native AOT decide whether it remains reachable.

Unmodified main is measured first, followed by a no-op copied-source control,
app-only removal and both-path removal. The copy control must have identical
executable size and MethodCode totals to main. Finally, a native interactive
control is built without replacement inputs: it must render both default
construction paths, respond to Ctrl+C and use the exact original untrimmed
library hash. This also guards against incremental-build contamination.

### Windows observation

Measured on source revision `2fbc702660c2de046ebb77127ec0efe1b93c5ad4`
(merged #746), with test-only working-tree changes, SDK **10.0.403**,
`Microsoft.DotNet.ILCompiler/10.0.14`, `win-x64`, `net10.0`, Release,
self-contained Native AOT, Size optimization and map emission. All four use the
same headless consumer source and assembly name, project-reference library
`Hex1b, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null`, and normal
native-helper settings. Per-input SHA-256 identities, full maps, binlogs and
inventories are in the reports.

| Compile inputs | EXE bytes | Saved bytes vs main | Saved percent | MethodCode count / bytes |
|---|---:|---:|---:|---:|
| Unmodified main | 4,825,600 | 0 | 0% | 12,781 / 2,307,009 |
| Unmodified copied sources | 4,825,600 | 0 | 0% | 12,781 / 2,307,009 |
| App fallback removed only | 4,825,600 | 0 | 0% | 12,781 / 2,306,834 |
| Both construction paths removed | 4,680,192 | 145,408 | 3.0132626% | 12,407 / 2,216,958 |

App-only removal shrinks app code by 175 mapped bytes but removes no console
family methods and crosses no executable-size boundary. Both-path removal
eliminates these declaring MethodCode families, including generated code:

| Family | Main count / bytes | Both removed count / bytes | Removed count / bytes |
|---|---:|---:|---:|
| `ConsolePresentationAdapter` | 42 / 17,875 | 0 / 0 | 42 / 17,875 |
| `WindowsConsoleDriver` | 39 / 22,731 | 0 / 0 | 39 / 22,731 |
| Unix console driver/interop | 0 / 0 | 0 / 0 | 0 / 0 |
| `System.Console` | 80 / 9,223 | 74 / 8,143 | 6 / 1,080 |

`System.Console` is not fully removable: the consumer prints its verification
marker and other reachable library code can use it. The retained source-level
`ConsolePresentationAdapter` reflow dependency does not leave any adapter
MethodCode in the both-path experiment. The 145,408-byte EXE difference is not
the sum of these method lengths: other reachable code, data, metadata and
binary alignment also change.

Full publish inventory (no assets removed):

| Asset | Main bytes | Copied control bytes | App-only bytes | Both-path bytes |
|---|---:|---:|---:|---:|
| `HeadlessConsole.exe` (executable) | 4,825,600 | 4,825,600 | 4,825,600 | 4,680,192 |
| `HeadlessConsole.pdb` (symbols) | 21,590,016 | 21,598,208 | 21,598,208 | 21,164,032 |
| `Hex1b.xml` (documentation) | 3,137,542 | 3,137,542 | 3,137,542 | 3,137,542 |

There were no other runtime/native-helper assets in these publish directories;
normal Windows helper builds were still enabled. PDB/source-path differences
are separate from executable savings. Both 100x24 snapshots verified text,
chart title, labels, values, bar glyphs and color, and exported identical SVGs
in all four cases. Both native interactive controls rendered both prompts and
exited with code zero after actual Ctrl+C input.

This is an isolated estimate of the size opportunity, **not evidence that the
existing headless API naturally trims console**, nor an exact prediction for a
production refactor. The mutable builder initializes a console factory before
`WithHeadless` replaces it; moving the fallback into `Build` would still leave
a runtime branch. The app's public constructor also legitimately supports
missing-adapter console ownership. No clean, small internal-only change that
eliminates both roots while preserving those behaviors was established here,
and app-only removal has no measured EXE benefit. This evidence does not
justify reshaping the public API on its own.

Issue #708's original application used Hex1b 0.118.0 and SDK/runtime
10.0.401/10.0.12; its 850,432-byte combined Kusto/Hex1b saving is **not**
comparable to or attributable from this current-main result. This experiment
does not quantify that older version, Kusto's application, or a constructor-body
ILC substitution. Linux/macOS execution and savings have not been verified.

## Adding a scenario

1. Add a test method to `NativeAotScenarioTests` with a raw string containing a
   complete native program. `ScenarioRunner.RunAsync` generates its project,
   consumer props, and echo workload source inside the run's evidence directory.
   Do not add checked-in consumer projects or a shared mode-switching entry point.
2. Exercise only the intended public API in that source, check real
   input/output/lifetime behavior with bounded waits, and print a distinct success
   marker at the end.
3. Add a `Scenario` with narrowly scoped required/forbidden declaring-code
   families. Add representative map-fragment tests for new matching rules,
   including generated code, metadata-only mentions and generic signatures.
4. Run parser tests and the independently filterable scenario on a host with
   Native AOT prerequisites and `HEX1B_RUN_NATIVE_AOT_TESTS=1`.
   Review and retain its report; update this table and document any limitations.

Windows is the initially verified runtime platform for these scenarios.
Linux/macOS are supported execution targets but have not been runtime-verified
as part of this initial implementation.
