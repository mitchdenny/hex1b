# Native AOT scenarios

Native AOT scenarios run inside the normal **managed `Hex1b.Tests` MSTest
runner**. Each test embeds its native program as a raw source string. A helper
writes a separate temporary console project against the current Hex1b project,
publishes it, and starts its native executable directly. The runner itself is
not Native AOT-published, and there are no checked-in consumer projects.

The normal multi-platform and daily CI test workflows set
`HEX1B_RUN_NATIVE_AOT_TESTS=1`, so native scenarios run alongside existing tests.
Locally, the three costly native scenarios are disabled by default with an
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

`TestCategory=NativeAot` selects all three costly scenarios;
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

All generated consumers use the same small in-memory echo workload source, not a
real shell or process. They contain no scenario dispatcher, reflection, MSTest,
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

Executable sizes are **observations, not byte budgets**. There are no fixed or
configurable size limits, historical build database, or automatic baseline
updates. To claim an optimization, compare the **same scenario** with matched
SDK/compiler, RID, configuration and publish settings across revisions, using
the reports and library input hashes. Cross-scenario differences alone do not
prove an optimization. Dirty checkouts, debug symbols, support assets, and
compiler changes need to be accounted for separately.

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
