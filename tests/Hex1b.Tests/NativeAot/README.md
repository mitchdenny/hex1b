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
| `HeadlessNormal` | Real text/chart rendering, dimensions/content/colors and SVG export through the console-default builder with `WithHeadless()` | Terminal, builder, app, chart, pool, console construction and adapter | None: positive console-retention control |
| `HeadlessSlim` | Identical text/chart source, current library inputs and byte-identical SVGs through the explicitly configured slim builder | Terminal, builder, app, chart, pool | Console adapter/construction, Windows/Unix console driver |
| `InteractiveConsole` | Default builder and explicitly configured slim builder render and stop in a real PTY | Terminal, builder, app, console adapter/construction | None |
| `ConsolePtySlim` / `ConsolePtyNormal` | Explicit console presentation and a real PTY child exchange readiness, input, output and exit successfully | Terminal, builder, child process, console adapter/construction | App, app options/workload, widgets, nodes, app render context, surface pool |
| `ConsolePtySlimWithApp` / `ConsolePtyNormalWithApp` | Same PTY exchange, preceded by an explicitly opted-in minimal console app that renders and stops | Same console/PTY code plus every forbidden app family above | None |

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

The current headless comparison records both reports in
`headless-comparison.json`, including consumer/library input hashes, executable
delta/percentage and declaring family counts/lengths. The same consumer source,
assembly name and success marker are used; only the consumer-local
`SLIM_BUILDER` define selects the construction path. Text/chart SVGs must match
byte for byte. There are no library source substitutions or historical-source
reconstruction in the current runner.

Run the current headless and interactive regression cases:

```powershell
$env:HEX1B_RUN_NATIVE_AOT_TESTS = '1'
dotnet test --project tests\Hex1b.Tests\Hex1b.Tests.csproj -c Release -- --no-progress --filter "FullyQualifiedName~HeadlessConsoleTests"
```

Executable sizes are **observations, not byte budgets**. There are no fixed or
configurable size limits, historical build database, or automatic baseline
updates. To claim an optimization, compare the **same scenario** with matched
SDK/compiler, RID, configuration and publish settings across revisions, using
the reports and library input hashes. Cross-scenario differences alone do not
prove an optimization. Dirty checkouts, debug symbols, support assets, and
compiler changes need to be accounted for separately.

## Headless console isolation experiment (#708)

This section, the boolean/slim prototypes and the actual-source comparison
below preserve **historical measurements**, not scenarios run by the current
suite. The exploratory reconstruction/source-substitution machinery has been
removed after selecting the production API. Use commit
`33c0a0f18661fd00131be573e25d02c17aba7bb8` to reproduce those historical
experiments; the commands within these historical sections apply to that
revision. Current-library interactive controls use the default builder and
explicitly configured slim builder.

Run the independently filterable measurement:

```powershell
$env:HEX1B_RUN_NATIVE_AOT_TESTS = '1'
dotnet test --project tests\Hex1b.Tests\Hex1b.Tests.csproj -c Release -- --no-progress --filter "FullyQualifiedName~HeadlessConsoleTests.HeadlessTextAndChart"
```

This is **test-only measurement**, not a new supported construction path.
In the original measurements, production library files, public signatures,
interactive defaults and surface pooling were unchanged (pooling remains enabled with the default 8 surfaces per
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
executable size and MethodCode totals to main. Current reruns reconstruct those inputs rather than reverting production
files. Native interactive controls before and after the historical experiment
use the actual library and must have the same untrimmed library hash. This
guards against incremental-build contamination.

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

### Boolean and slim-builder follow-up

Two additional test-only prototypes measure the suggested construction shapes:

```powershell
$env:HEX1B_RUN_NATIVE_AOT_TESTS = '1'
dotnet test --project tests\Hex1b.Tests\Hex1b.Tests.csproj -c Release -- --no-progress --filter "FullyQualifiedName~HeadlessConsoleTests.CreateBuilder_HeadlessTrue|FullyQualifiedName~HeadlessConsoleTests.CreateSlimBuilder_NativePublish"
```

The boolean prototype adds `CreateBuilder(bool headless)` forwarding to a
builder constructor that selects the initial factory with a runtime boolean.
Its native consumer passes literal `headless: true` and does not subsequently
call `WithHeadless()`. The parameterless builder delegates with `false`.

The slim prototype adds `CreateSlimBuilder()` forwarding to a separate
factory-supplied constructor. The normal parameterless constructor supplies
the console factory; the slim entry point supplies only a headless factory.
There is no boolean factory-selection branch in that shared constructor.
Headless defaults are an experimental choice here, not a finalized slim API.

Each prototype has its **own matched consumer source** across three variants:
a baseline wrapper using the old builder followed by `WithHeadless`, the actual
prototype with the normal app fallback, and the prototype with the app fallback
experimentally removed. The last variant isolates the builder's behavior; it
does not constitute a supported app-construction solution. No production source
or public API is changed by these tests.

Observed on revision `079f61e1d7729bc886ed1de12753853d674ff10c` with test-only
working-tree changes, using the same SDK 10.0.403 / ILCompiler 10.0.14,
Windows x64, Release/Size settings and unchanged pooling/native assets as above:

| Prototype / variant | EXE bytes | Saved vs its matched wrapper | MethodCode count / bytes |
|---|---:|---:|---:|
| Boolean: old-builder wrapper | 4,825,600 | 0 (0%) | 12,781 / 2,307,173 |
| Boolean: constructor selection | 4,825,600 | 0 (0%) | 12,780 / 2,307,135 |
| Boolean: constructor selection, app fallback removed | 4,825,600 | 0 (0%) | 12,780 / 2,306,960 |
| Slim: old-builder wrapper | 4,825,600 | 0 (0%) | 12,781 / 2,307,173 |
| Slim: separate construction | 4,825,600 | 0 (0%) | 12,780 / 2,306,991 |
| Slim: separate construction, app fallback removed | 4,680,192 | 145,408 (3.0132626%) | 12,406 / 2,216,960 |

**The tested boolean does not trim console.** Even without the app fallback,
both generated constructor factory methods remain emitted (78 and 90 bytes),
alongside the shared 278-byte constructor and all 42 adapter / 39 Windows-driver
methods. A literal `true` did not eliminate that console factory in this shape
and toolchain. This is not a claim about every possible compiler or forced
inlining configuration.

**The slim shape removes the builder root**, but the normal app fallback still
retains console. Once that fallback is removed in isolation, the slim map has
only the headless factory (78 bytes) and factory-supplied constructor
(153 bytes); adapter and Windows-driver declaring MethodCode both drop to zero.
The removed console families are the same 42 methods / 17,875 bytes and
39 methods / 22,731 bytes as the earlier experiment. `System.Console` again
drops from 80 / 9,223 to 74 / 8,143, rather than disappearing entirely.

All six inventories contain only `HeadlessConsole.exe`, `HeadlessConsole.pdb`
and `Hex1b.xml`. PDB size is 21,598,208 bytes except for the isolated slim result
(21,155,840); XML is always 3,137,542 bytes. Symbols are excluded from EXE
savings. Full consumer/library hashes, inventories, generated source, maps and
removed-method lists are in `headless-bool-comparison.json`,
`headless-slim-comparison.json` and their reports.

Every variant produces byte-identical verified text/chart SVGs within its
matched group. Real PTY controls exercise default interactive construction for
both prototypes and explicit `headless: false` for the boolean prototype, plus
standalone apps and restored unmodified construction. Every control renders
both prompts and exits normally after input-gated Ctrl+C. The complete native,
map and infrastructure suite passed 45 tests on Windows. Linux/macOS remain
unverified. A production slim path would still need adapter-supplied app
construction separated from the interactive fallback, with explicit behavior
for option overrides and ownership.

## Actual slim-builder and internal-app experiment

The source experiment adds `Hex1bTerminal.CreateSlimBuilder()` with **no
presentation or workload registration**. Callers must explicitly opt into both:

```csharp
await using var terminal = Hex1bTerminal.CreateSlimBuilder()
    .WithHeadless()
    .WithHex1bApp(ctx => ctx.Text("Hello"))
    .Build();
await terminal.RunAsync();
```

`CreateBuilder()` and the public parameterless builder constructor retain their
console defaults. The default constructor alone registers the console factory;
the shared factory-supplied constructor does not reference console construction.
Slim builders reject missing presentation rather than silently defaulting to
headless or console. Dimensions and ordinary terminal option defaults remain.

Both `Hex1bApp` constructors become internal, retain their existing argument
shape for friend-assembly tests, require a supplied workload adapter, and no
longer construct or own a console terminal. Existing adapter disposal behavior
is unchanged. The public app type, callback APIs, `Invalidate`, `RequestStop`
and public workload adapter remain available.

Historical actual-source measurement command (on the revision above):

```powershell
$env:HEX1B_RUN_NATIVE_AOT_TESTS = '1'
dotnet test --project tests\Hex1b.Tests\Hex1b.Tests.csproj -c Release -- --no-progress --filter "FullyQualifiedName~HeadlessConsoleTests.CreateSlimBuilder_CurrentLibrary"
```

All four variants compile the **same consumer source** (SHA-256
`FA7C3ECF52C0C7EFC3901E6435C89EF1B061ACFD30F2F039BAF894CD18695B9F`),
including explicit `WithHeadless()`. The pre-change and normal-builder controls
provide a `CreateSlimBuilder()` shim that forwards to their normal builder;
the final consumer uses the real API without any source overrides. A no-op
current-source copy control must have identical EXE bytes and MethodCode totals
to the unmodified actual library.

Compile replacements preserve **original source order** as well as running
before `AssignTargetPaths`. Appending a replaced partial-class file changes
field layout and hundreds of method lengths despite identical source. The
no-op control caught this; the order-preserving implementation gives identical
MethodCode counts and lengths. Earlier historical figures above predate this
apparatus correction and remain labeled with their original revisions.

Windows x64 observation, SDK **10.0.403**, ILCompiler **10.0.14**,
net10.0 Release/Size, self-contained Native AOT, on parent revision
`3fc4bbc8a61750274ef23bb09c6b60307d7b38a3` with this source experiment:

| Construction | EXE bytes | Saved vs pre-change | MethodCode count / bytes |
|---|---:|---:|---:|
| Pre-change app and builder, forwarding shim | 4,825,600 | 0 | 12,781 / 2,307,009 |
| Actual internal app, normal builder via shim | 4,826,112 | -512 (-0.0106101%) | 12,782 / 2,306,596 |
| Actual app and slim builder, copied sources | 4,680,192 | 145,408 (3.0132626%) | 12,406 / 2,216,584 |
| Actual app and slim builder, no overrides | 4,680,192 | 145,408 (3.0132626%) | 12,406 / 2,216,584 |

The app-only row includes the real builder refactor and validation, so it is
not the earlier isolated fallback-body-removal experiment. Its 512-byte
increase illustrates why mapped code length alone is not executable size.

The real slim path removes all 42 `ConsolePresentationAdapter` methods
(17,875 bytes) and all 39 `WindowsConsoleDriver` methods (22,731 bytes).
Unix families were absent in both Windows builds. `System.Console` falls
from 80 methods / 9,223 bytes to 74 / 8,143, removing 6 / 1,080 rather than all
console support. The builder still contains its direct adapter type-check and
`WithReflow` call, but no adapter MethodCode survives. No constant propagation
of mutable options is assumed. The actual headless consumer and actual
interactive control use the exact same untrimmed library hash; both default
console and explicitly opted-in slim console presentation render and stop
normally under a real PTY.

Full inventory, with symbols and documentation excluded from EXE savings:

| Asset | Pre-change | Actual app / normal builder | Actual copied slim | Actual slim |
|---|---:|---:|---:|---:|
| `HeadlessConsole.exe` | 4,825,600 | 4,826,112 | 4,680,192 | 4,680,192 |
| `HeadlessConsole.pdb` (symbols) | 21,598,208 | 21,598,208 | 21,164,032 | 21,155,840 |
| `Hex1b.xml` (documentation) | 3,137,654 | 3,138,875 | 3,138,875 | 3,138,875 |

These are the complete publish inventories; no native helpers or runtime
support assets were copied, and normal generation remained enabled. Pooling
is unchanged and emitted pool methods are required positive controls.
Both text/chart 100x24 snapshots verify rendering and have byte-identical SVGs
in every variant, with the same SVG hashes as the original experiment.
Reports, maps, full source/library identities, all removed methods, inventories,
and `slim-actual-comparison.json` are under ignored `TestResults/NativeAot`.
Linux/macOS and an Aspire application build have not been executed.

The final run retained evidence at
`TestResults/NativeAot/20261109T011031647Z-7d73ff7ac24d491482d4b387bd92150d`.
The shared assembly identity is
`Hex1b, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null`.
The actual headless and interactive library input SHA-256 is
`D691AFBF939141B9B8C20BD0B981A61F460563C1B575433BA36A809C80030825`;
the reconstructed baseline is
`7493DB335708F5CCA64052251FF095D4E30ECE940A75EB439CA943C49CAF389A`.
The complete native namespace plus managed builder, app lifecycle, scheduling,
flow, and textbox coverage passed **275 tests**, with no failures or skips.
The log is `TestResults/slim-actual-final-tests.log`.

### Production migration

The selected API now includes the caller migration. The ten non-friend
constructor call sites in the website, FloatAlignmentExplorer, PerfDemo,
WebTerminalDemo and AgenticPromptDemo.Tests use terminal builders. The terminal
owns the app's run/disposal lifecycle. Options remain configurable, and the
app-instance callback exposes the public app handle for invalidation and stop
requests. A configured workload adapter is used by both the app and terminal.

The 712 existing direct-construction calls in the friend test assembly need
no mass rewrite. New tests cover empty slim configuration, rendering,
inaccessible public app construction, missing adapters and configured workload
input/output. Public documentation and generator templates use builders.
No new friend grants, compatibility factories or feature switches hide the
constructor break; existing package validation settings are unchanged.

### Production validation (2026-11-09)

The simplified current-library headless comparison gives these matched
observations with SDK **10.0.403**, ILCompiler **10.0.14**, net10.0 Release/Size:

| Host RID | Normal/headless EXE | Slim/headless EXE | Saved bytes | Saved percent |
|---|---:|---:|---:|---:|
| win-x64 | 4,825,600 | 4,680,192 | 145,408 | 3.0132626% |
| linux-x64 | 5,485,568 | 5,343,952 | 141,616 | 2.5816105% |

These are within-host comparisons, not cross-OS attribution. Both use consumer
source SHA-256
`ED3D8260C74A907FC217ACE27E25F55E74506A024D053F40D076AA6464D0E646`.
Within each comparison, source, toolchain, library input and settings match;
only the consumer-local builder selection differs. The Windows library input is
`8761413E1F8EFFB1BC93CED8583723131598307B79CD86BFF6FC0426448EA325`;
the Linux input is
`046B99893D0C8485A22E5BAE4F6592AA1A80C6DC50FCA116D41B53BE4669120F`.
Both retain app/chart/pool positive controls, produce identical text/chart
SVGs, and pass real interactive console controls. Pooling is unchanged.

The slim maps remove all 42 adapter methods / 17,875 code bytes and all
39 Windows-driver methods / 22,731 bytes on Windows. Linux removes all
42 adapter methods / 18,699 bytes and all 23 Unix-driver/interop methods /
5,425 bytes. Neither slim map contains direct console-adapter MethodCode;
framework console code is not completely eliminated. These code lengths are
not the EXE size deltas.

Besides the EXEs above, the complete publish inventories contain:

| RID | File | Kind | Normal bytes | Slim bytes |
|---|---|---|---:|---:|
| win-x64 | `HeadlessConsole.pdb` | Symbols/compiler support | 21,590,016 | 21,155,840 |
| win-x64 | `Hex1b.xml` | Documentation | 3,139,534 | 3,139,534 |
| linux-x64 | `HeadlessConsole.dbg` | Symbols/compiler support | 9,128,976 | 8,914,368 |
| linux-x64 | `Hex1b.xml` | Documentation | 3,079,946 | 3,079,946 |
| linux-x64 | `libhex1binterop.so` | Native helper/support | 21,896 | 21,896 |

No support asset was removed to obtain the EXE reduction. Both hosts' maps,
per-method console-family inventories, hashes, snapshots and reports remain
under ignored `TestResults/NativeAot` and `TestResults/LinuxValidation`.

The Linux four-way console/PTY check also passes: both app-free builders are
4,914,112 bytes, and both app-enabled controls are 5,755,088 bytes. Omitting
the app avoids 840,976 bytes / 14.6127392%, but slim versus normal saves zero
in this composition. All seven app families are absent without the app and
present in the controls, with actual PTY input/output verified.

The Linux native namespace passes **48 tests** in an isolated SDK container.
Windows main-project coverage completes with 15,462 passed, 105 skipped and
one unrelated failure: its existing directory-symlink test requires a
Windows privilege unavailable to this process. The final targeted builder
and map-parser selection passes **97 tests**. The other three CI test
projects and AgenticPromptDemo tests pass.

An isolated public `microsoft/aspire` snapshot
`fbad38832128f625a6a28705236a55a38419acf3` builds its actual CLI against the
modified Hex1b DLL with zero warnings/errors; the rebuilt CLI's `--help`
also succeeds. Its required SDK 11 is installed only under ignored
`TestResults`, with no Hex1b SDK pin or global changes. This is a source/build
and startup compatibility check, not an interactive Aspire UI test.
The website and migrated samples build, PerfDemo completes measured frames,
and all five getting-started programs compile. Eight extracted documentation
tests and headless rendering/cancellation checks for all five custom website
examples pass (13 cases total). The terminal example reads the workload's
dimensions only after terminal construction has resized it, not in the eager
options callback. API generation plus the direct VitePress build succeeds;
the Aspire pipeline wrapper stalls during generator execution and was stopped
without changing AppHost code. macOS remains unverified.

## Console + PTY without the app infrastructure

`ConsolePtyTests.ConsoleAndPty_NativePublish_ExcludesAppAndMeasuresOptInCost`
proves that this composition does not retain app implementation code:

```csharp
Hex1bTerminal.CreateSlimBuilder()
    .WithPresentation(new ConsolePresentationAdapter())
    .WithPtyProcess(options => { /* configure child command */ })
```

Four consumers share identical `Program.cs`, assembly name, current library
inputs, RID and publish settings. Consumer-local `DEFAULT_BUILDER` selects
normal versus slim construction; consumer-local `APP_CONTROL` additionally
renders and stops a minimal text app before running the identical PTY exchange.
These symbols are generated only in the consumer project, not imported props,
global MSBuild properties or library references. There are no library source
substitutions or production changes for this comparison.

Every native program runs inside a test-owned real outer PTY with an inherited,
child-specific environment. Passing an empty environment previously broke the
nested Windows proxy PTY with Winsock error 10106; inheriting the environment
fixes that case without changing production PTY options. Environment values
are neither logged nor changed in the parent. The inner workload uses the
default Windows `RequireProxy` mode, not a direct-PTY workaround. Its shell
announces readiness, accepts `pty-input`, emits `pty-output:pty-input`, and
exits with code zero. Unix uses `/bin/sh` with echo disabled. App controls
must render `app-positive-control` before automated Ctrl+C. Cursor-free PTY
SVGs must be identical across all four consumers; app SVGs must match between
the two positive controls.

Windows x64 observation against the experimental library at
`2c5ef41c8a67e86d5cd997e5dbb1828ff64115d2`, SDK **10.0.403**,
ILCompiler **10.0.14**, net10.0 Release/Size, self-contained Native AOT:

| Consumer | EXE bytes | MethodCode count / bytes |
|---|---:|---:|
| Slim console + PTY | 5,555,712 | 15,161 / 2,661,586 |
| Normal console + PTY | 5,555,712 | 15,163 / 2,661,765 |
| Slim console + PTY + minimal app control | 6,295,040 | 17,374 / 3,084,784 |
| Normal console + PTY + minimal app control | 6,295,552 | 17,376 / 3,084,963 |

Omitting the optional app saves **739,328 bytes (11.7446116%)** with slim
construction, or **739,840 bytes (11.7517892%)** with normal construction.
Percentages use the corresponding with-app EXE as denominator. This measures
the incremental cost of this explicitly opted-in, rendered minimal app and
its control code, **not all possible widgets**, and not a saving from fixing
automatic app retention. Both app-free builders already trim the app.
Slim versus normal saves **zero EXE bytes** for console + PTY alone;
the with-app control differs by 512 bytes (0.0081327%).

Declaring-family evidence is required in both real app controls and forbidden
in both app-free consumers, including generated closures/state machines:

| App family | With-app methods / bytes | Without-app methods / bytes |
|---|---:|---:|
| `Hex1bApp` | 65 / 39,208 | 0 / 0 |
| `Hex1bAppOptions` | 1 / 61 | 0 / 0 |
| `Hex1bAppWorkloadAdapter` | 31 / 9,094 | 0 / 0 |
| Widgets | 158 / 28,726 | 0 / 0 |
| Nodes | 187 / 31,530 | 0 / 0 |
| `Hex1bRenderContext` | 8 / 324 | 0 / 0 |
| `SurfacePool` | 11 / 1,923 | 0 / 0 |
| **Total direct app families** | **461 / 110,866** | **0 / 0** |

The total map reduction is 2,213 methods / 423,198 code bytes for either
builder, including transitive dependencies. Method lengths are not EXE
sections or a byte-for-byte explanation of the 739 KB EXE delta.
Direct console dependencies deliberately remain: all four retain 42 adapter
methods / 17,670 bytes, 39 Windows-driver methods / 22,729 bytes, and 100
child-process-family methods / 39,846 bytes. Unix console-driver methods
are absent in these Windows maps. Parser tests distinguish app-declaring
methods from metadata or app types mentioned in generic signatures.

Complete publish inventory, excluding symbols/documentation from EXE savings:

| File | App-free (both builders) | With app (slim) | With app (normal) |
|---|---:|---:|---:|
| `ConsolePty.exe` | 5,555,712 | 6,295,040 | 6,295,552 |
| `ConsolePty.pdb` (symbols) | 24,834,048 | 28,135,424 | 28,135,424 |
| `Hex1b.xml` (documentation) | 3,138,875 | 3,138,875 | 3,138,875 |

**The EXE is not the whole Windows PTY deployment.** The default proxy resolves
the normally generated local helper from
`src/Hex1b/obj/windows-pty-shim/win-x64/native`. Its entire unchanged directory
is separately inventoried and hash-compared across consumers: 233 files /
77,988,138 bytes, including four `hex1bpty` helper files totaling 292,474
bytes and 229 runtime-support files totaling 77,695,664 bytes. This local
helper is a self-contained managed build (`hex1bpty.exe` is 151,552 bytes and
`hex1bpty.dll` is 82,432 bytes), not a newly native-published helper.
No helper/runtime files are deleted, substituted or counted as app savings.
The inventory is conservative, not a minimal-deployment claim.

Shared consumer source SHA-256:
`F326054D5232C4B1F0428F071DBDE6B13A7A1F905C4C7484B9063EA58386A158`.
Shared untrimmed library SHA-256:
`C60CC1A71275D0328A5B6A35CF5670DCF0DB3D6769D8B8FCB5959063CBB604FA`,
identity `Hex1b, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null`.
Pooling behavior and normal analyzer/helper builds remain unchanged.

Reports, maps, generated consumers, runtime logs, SVGs, full per-file
inventories/hashes and removed app methods are preserved under ignored
`TestResults/NativeAot`; the comparison is `ConsolePtySlim/pty-app-comparison.json`.
The final evidence run is
`20261109T013757052Z-dcc45ea8744b4a6aa8ae18151396add0`; all **56** tests in the
native namespace passed, with no failures or skips
(`TestResults/pty-app-full-native-tests.log`). Linux/macOS have not been
executed for this experiment. Reproduce with:

```powershell
$env:HEX1B_RUN_NATIVE_AOT_TESTS = '1'
dotnet test --project tests\Hex1b.Tests\Hex1b.Tests.csproj -c Release -- --no-progress --filter "FullyQualifiedName~NativeAot.ConsolePtyTests"
```

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
