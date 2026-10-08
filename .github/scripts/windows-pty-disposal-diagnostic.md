# Windows PTY disposal diagnostic

This opt-in probe investigates issue #655 without enabling the ignored regression
in normal CI. It preserves the original five-iteration, 200 ms / 100 ms scenario,
uses the default proxy backend, and adds flushed lifecycle and transport logs.
It is diagnostic instrumentation, not a verified fix.

Run the existing Deploy workflow on the diagnostic branch:

```sh
gh workflow run build-deploy.yml --ref <diagnostic-branch> \
  -f windows-pty-diagnostics=true
```

This selects only the Windows diagnostic job; normal builds and publishing are
skipped. The reusable workflow builds the existing test executable and helper,
then invokes the exact diagnostic test filter with a child-only environment
marker. Neither setting alone enables the scenario in a normal full-suite run.
The original regression remains ignored.

Before the real probe, the workflow verifies dump/stack collection using an
explicit diagnostic-only pause while the PTY helper is alive and a three-second
watchdog. These artifacts are under `watchdog-check`; they demonstrate the
capture mechanism, not the original hang. The normal probes are under
`reproduction`, and never enable the deliberate pause.

The PowerShell watchdog runs ten separate probe processes. Each process has
120 seconds to finish. On a stall it records the process tree, captures full
ProcDump dumps of the parent and live descendants, and extracts managed stacks
from the test/helper dumps using dotnet-dump. Diagnostic tool processes themselves
have 60-second deadlines. Cleanup targets specific recorded PIDs and rechecks
creation times; it never kills processes by name. Capture errors are preserved,
and a watchdog timeout fails the job even if dump collection fails.

Download `windows-pty-disposal-diagnostics` from the workflow run. Each attempt
contains lifecycle markers, client/helper/protocol traces, redirected test output,
and MSTest diagnostics. Timed-out attempts also contain process dumps, managed
stack output, and any capture errors. Full dumps may contain process environment
data; artifacts expire after seven days and should not be shared publicly.

The last marker distinguishes a blocked RunAsync invocation, synchronous disposal
prefix, or disposal await. A successful probe does not prove the full Windows
suite is safe or establish a root cause. Keep the Ignore until an evidence-backed
fix passes both repeated isolated and full-suite Windows coverage.
