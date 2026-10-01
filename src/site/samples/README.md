# Standalone documentation samples

**Current totals:** this directory's catalog contains **156 documentation samples:
149 recordable + seven source-only**. The site also includes the separately owned
graphics-spike sample, giving **157 site samples: 150 recordings + seven source-only**.

Each catalog entry has its own independent .NET project, `Program.cs`, README,
MIT license, and build-output ignore rules. Every project targets .NET 10 and pins
the published `Hex1b` NuGet package to **0.166.0**. They do not depend on repository
projects, the website backend, or a running development server.

Run a recordable project from its directory. For source-only entries, first read
that project's prerequisites: they may start services or commands and must not be
auto-run.

```sh
dotnet run
```

## Coverage and provenance

`catalog.json` is the runnable sample manifest consumed by the static site:

- 127 complete programs displayed by `CodeBlock`.
- 12 complete programs imported from raw `.cs` snippets.
- Seven complete, self-contained C# application fences.
- Three backend-only demos: layout, theming, and navigator.
- Seven additional source-only projects for complete external-asset, hosting,
  Docker, and headless-workload examples.

`page` is relative to the preserved documentation content root. `codeKey` is the
JavaScript binding, or `fence-N` for the one-based
index among **C# fences only** in that page. TerminalDemo-only entries have an empty
`codeKey`. `example` preserves the original live demo identifier, when present.
`project` is relative to `src/site`. `columns`/`rows` default to 100×30.
The QR-code demos use 45 or 60 rows so the complete code and controls remain visible.

Optional `readyText` is a stable initial UI label, preferably a visible footer,
at the catalog dimensions. Samples without a reliable textual marker omit it
(for example graphics-only surfaces and local shells). Optional `actions` use the
generic recording-driver format; interactive entries use a single `{ "key": "Tab" }`
to illustrate focus navigation without activating handlers. Shell/embedded-terminal
and flow entries explicitly set `actions: []` to override driver default inputs.
No sample hooks, shell commands, confirmation responses, or arbitrary Enter events are used.
Readiness markers are verified against initial PTY output at their catalog dimensions.

`building-clis-spinner` is the only recordable program that finishes autonomously:
it completes after three one-second simulated work stages and a 300 ms final update. Its
`capturePolicy: "until-exit"` tells the driver to capture through normal completion
and drain the final output instead of requiring the process to remain alive for
an interaction scenario. It uses default first-content readiness rather than a
transient progress label or the completion message. The other 148 recordable
programs remain input-driven. No artificial keep-alive is added to any sample.

## Source-only projects

Entries with `sourceOnly: true` are compiled, shown in Monaco, and exported as
cloneable Git repositories, but are **never executed by site builds or smoke
tests**. Their required `prerequisites` string is shown instead of playback
controls. This is intentional source browsing, not an unavailable-demo placeholder.

| Project | Prerequisites |
| --- | --- |
| `terminal-emulator-fence-2` | A buildable working directory; the child build uses a headless presentation |
| `terminal-emulator-fence-3` | Docker daemon/CLI and the default .NET SDK container image |
| `terminal-emulator-fence-4` | Docker, Ubuntu image, explicitly chosen host-data mount and working directory |
| `terminal-emulator-fence-5` | Docker and an externally supplied Dockerfile/build context |
| `using-the-emulator-fence-1` | ASP.NET Core, bash, and a compatible browser terminal client |
| `using-the-emulator-fence-2` | ASP.NET Core, browser client, and the documented SSH/Docker workloads |
| `figlet-custom-font` | A separately supplied, appropriately licensed FIGlet font |

Each README documents its exact prerequisites and side effects before showing how
to run it manually. The two hosting projects use `Microsoft.NET.Sdk.Web`; all
others use `Microsoft.NET.Sdk`. Every project still pins only Hex1b 0.166.0.
No font, browser client, Dockerfile, container image, or service is silently
downloaded or provisioned as part of compilation.

`inventory.json` also records every deliberately inline API/configuration/testing
snippet, raw import, source-only example, and unused full program
declaration. No wrapper is invented for partial expressions or pseudocode.
Generated API member pages under `reference/` are excluded, matching that folder's
`.gitignore`; the authored `reference/index.md` and `reference/cli.md` remain in scope.
Regenerating API documentation therefore does not change the authored-demo inventory.
All rendered complete authored demos are covered. The 413 partial snippets stay
inline or, for unused imports, unrendered; six unused full script declarations
also remain unrendered. Two complete raw imports were already retained as
cloneable, sample-index-only projects despite having no contextual component:
`terminal-basic-basic-snippet` and `toggle-switch-multi-multi-snippet`. They are
included in the 156-project total, not additional samples. Original documentation
and snippets are preserved verbatim.

Most legacy complete programs use older API signatures. Each project's README and
catalog `notes` explain its necessary adaptations; the original Markdown and raw
snippets are unchanged. The incomplete embedded-terminal CodeBlock is replaced
with the complete backend implementation, retaining restart and cleanup behavior
without logger or website-host dependencies.

## Maintenance

These programs are tracked source, not disposable site-build output. Site builds
must never regenerate or overwrite them. Migration helpers are explicit authoring
tools and use only Python's standard library:

```sh
# Inspect the legacy documentation inventory without writing files.
python3 src/site/samples/migrate.py

# Bootstrap only missing source files. Existing Program.cs files are preserved.
python3 src/site/samples/migrate.py --write-new

# Explicitly apply supported old-to-pinned-API adaptations, recording notes.
python3 src/site/samples/adapt.py

# Check inventory coverage, package pins, project independence and provenance.
python3 -m unittest discover -s src/site/samples -p 'test_*.py'

# Build all isolated copies with at most three concurrent dotnet processes.
python3 src/site/samples/validate.py

# Or build a targeted subset.
python3 src/site/samples/validate.py button-basic terminal-basic

# On macOS/Linux, check startup in a PTY without sending sample actions.
# Source-only entries are always skipped, even when explicitly selected.
python3 src/site/samples/smoke.py

# Verify configured readiness labels appear in initial PTY output (no actions).
python3 src/site/samples/smoke.py --verify-ready
```

Previously unclassified application fences cause inventory validation to fail
instead of silently becoming inline snippets. Classify them deliberately, then
compile and startup-check any new standalone project.

Validation copies only each standalone project's files into ignored `.validation/`
directories and disables both `Directory.Build.props` and `Directory.Build.targets`
discovery. This deliberately excludes repository-local analyzers, project
references, shared compile files, and implicit configuration. The retained logs
and `results.json` are local validation artifacts, not published sample content.

The parent site pipeline owns recording and static Git export. Capture a baseline
after readiness, then apply any safe focus-navigation actions. Shell examples
require `bash` on PATH and explicitly disable scripted input. All projects retain
the default `Sample` assembly name, so the prepared catalog's DLL path is always
`<sample-directory>/bin/Debug/net10.0/Sample.dll`.
