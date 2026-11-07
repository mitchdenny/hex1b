# Native AOT scenarios

`Hex1b.Aot.Tests` is an **opt-in managed MSTest runner**. It publishes three
independent console consumers against the current Hex1b project, then starts
each native executable directly. Neither the ordinary `Hex1b.Tests` workflow
nor existing CI targets reference or run this suite. The runner itself is not
Native AOT-published.

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

Missing prerequisites, unsupported hosts, publish errors, missing/invalid maps,
runtime crashes, nonzero exits, missing success markers, and stalls **fail** the
test. They are not skipped. Publish has a ten-minute timeout, executable execution
has a thirty-second timeout, and native behavior checks have their own bounded
waits. Test cancellation or timeouts kill only the test-owned process tree and
retain command/stdout/stderr logs.

## Running locally

From the repository root (PowerShell):

```powershell
dotnet test --project tests\Hex1b.Aot.Tests\Hex1b.Aot.Tests.csproj -c Release -- --no-progress
```

Run just the existing-terminal consumer:

```powershell
dotnet test --project tests\Hex1b.Aot.Tests\Hex1b.Aot.Tests.csproj -c Release -- --no-progress --filter "FullyQualifiedName~ExistingTerminalTape"
```

Run only fast map-reader tests, without Native AOT publishing:

```powershell
dotnet test --project tests\Hex1b.Aot.Tests\Hex1b.Aot.Tests.csproj -c Release -- --no-progress --filter "FullyQualifiedName~NativeMapTests"
```

`TestCategory=NativeAot` selects all three costly scenarios;
`TestCategory=Infrastructure` selects process/publish diagnostics tests, including
test-owned OS-shell stall fixtures for timeout/cancellation cleanup.
On Unix, use `/` instead of `\` in command-line project paths.
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

All consumers use the same small source-linked in-memory echo workload, not a
real shell or process. They contain no scenario dispatcher, reflection, MSTest,
or assertion-framework dependencies. Each emits its own `NATIVE_AOT_OK:<name>`
marker only after verifying behavior; an exception/nonzero exit is a failure.

The common **consumer-scoped** `Directory.Build.props` imports root props and
sets `PublishAot=true`, `OptimizationPreference=Size`, and
`IlcGenerateMapFile=true`. These are deliberately not global command-line
properties: global `PublishAot` propagates into the repository's netstandard
analyzer project and fails with `NETSDK1207`. Root analyzers remain enabled.
Likewise, only consumers interpret `AotScenarioArtifacts` to set intermediate,
build, and publish paths; no global `BaseIntermediateOutputPath` is applied to
library/native-helper project references.

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
  obj/.../native/<scenario>.map.xml
  bin/...
  publish/...
```

Reports and logs are attached with `TestContext.AddResultFile`; the JSON report
also appears in TestContext diagnostics. Reports record the scenario, revision
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

1. Create a separate folder and SDK console project beside the three consumers,
   with its own `Program.cs`. Common props provide the project reference and
   source-linked echo workload. Do not add a shared mode-switching entry point.
2. Exercise only the intended public API, check real input/output/lifetime
   behavior with bounded waits, and print a distinct success marker at the end.
3. Add a `Scenario` with narrowly scoped required/forbidden declaring-code
   families. Add representative map-fragment tests for new matching rules,
   including generated code, metadata-only mentions and generic signatures.
4. Add one independently filterable method to `NativeAotScenarioTests`, then
   run parser tests and that scenario on a host with Native AOT prerequisites.
   Review and retain its report; update this table and document any limitations.

Windows is the initially verified runtime platform for these scenarios.
Linux/macOS are supported execution targets but have not been runtime-verified
as part of this initial implementation.
