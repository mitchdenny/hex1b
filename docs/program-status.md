# Program status (OSC 7501 prototype)

Hex1b implements revision 0.4 of the [Program Status
Protocol](https://www.superlogical.com/rex/docs/build/program-status), dated
2026-10-10. A workload reports its own semantic state through normal terminal
output. Hex1b stores the current records independently of screen contents.
This prototype does not integrate Aspire or prescribe notification/inbox policy.

## Reporting from a TUI

Reporting is an application service, not a widget. `Hex1bApp.ProgramStatus`,
`RootContext.ProgramStatus` and event/input callback
`InputBindingActionContext.ProgramStatus` expose the same reporter.

```csharp
using Hex1b;
using Hex1b.Widgets;

await using var app = new Hex1bApp(ctx => ctx.VStack(v =>
[
    v.Button("Build").OnClick(async e =>
    {
        var reporter = e.Context.ProgramStatus;
        reporter.Report(new(TerminalProgramStatusState.Working)
        {
            App = "build-demo",
            Message = "Compiling",
            Progress = 0
        });

        await Task.Delay(500, e.Context.CancellationToken);

        reporter.Report(new(TerminalProgramStatusState.Done)
        {
            App = "build-demo",
            Message = "Build ready"
        });
    }),
    v.Button("Clear status").OnClick(e => e.Context.ProgramStatus.Clear()),
    v.Button("Quit").OnClick(e => e.Context.RequestStop())
]));

await app.RunAsync();
```

Reports are emitted as OSC 7501 through the app's workload output pipeline;
they do not mutate a particular observer terminal directly. The same output
can be consumed locally, headlessly, or through HMP1/HWT1. Invalid API arguments
fail before emitting a partial report. Reporting does not require a redraw and
does not infer completion from callback return values.

Report from input/event callbacks or application code, not a node's rendering
callback. Reporting or copying to the clipboard reentrantly during a frame
throws `InvalidOperationException` rather than blocking the rendering pipeline.
Use the asynchronous reporter methods when waiting for output capacity should
not block the calling thread.

Each report replaces its record completely. Repeat `App`, `Title`, `Message`
and other desired fields on every update. The report's omitted ID addresses the
root. A non-root ID such as `deploy/east` names an independent nested record.
`Clear("deploy")` removes that record and its descendants even if the parent
record itself does not exist; `Clear()` removes the complete inventory.

## Observing immutable state

The following is a consumer snippet for an existing terminal:

```csharp
var status = terminal.ProgramStatus;
var root = status.Find("");
var east = status.Find("deploy/east");
var descendants = status.GetDescendants("deploy");

terminal.ProgramStatusChanged += current =>
{
    foreach (var record in current.Records)
        Console.WriteLine($"{record.Id}: {record.State} / {record.Message}");
};

using var screen = terminal.CreateSnapshot();
var atomicallyCapturedStatus = screen.ProgramStatus;
```

The status-only snapshot is immutable and does not require disposal. A complete
terminal snapshot captures program status under the same state lock as its other
properties. Older snapshots retain their values after later reports, clearing,
screen changes and terminal disposal.

Records are in least-recently-updated order. An empty ID identifies the root;
`Find` returns no record when absent. `GetDescendants` excludes the addressed
record and respects slash boundaries (`deploy` does not contain `deployment`).
Missing parents are not manufactured. `App` is what the record reported;
`EffectiveApp` resolves its own or nearest existing ancestor's application ID.
Neither field authenticates a process.

| State | Meaning |
|---|---|
| `Idle` | Waiting for the next instruction. |
| `Working` | Running; nullable progress means indeterminate. |
| `Blocked` | Requires user action; optional kind is permission, question or auth. |
| `Done` | Finished work is ready to inspect. |
| `Error` | Work failed and stopped. |

Parent and child states remain independent. There is no aggregate state,
heartbeat, approval operation or automatic user acknowledgement.

## Lifetime and limits

RIS clears all records. Soft reset and primary/alternate-screen switching
preserve them. A new OSC 133 prompt or actual local workload exit removes
working/blocked records; done/error records survive. A remote transport
disconnect is not proof of program exit and retains the last known inventory.

Hex1b supports 256 records and evicts the least recently updated record when
creating another at capacity. Parsing enforces the specification's sequence,
key, ID, application, encoded-text and decoded-text limits. Oversized incomplete
7501 reports are discarded without retaining unbounded prefixes. Invalid
reports never partially update or evict records.

Messages and titles are base64 UTF-8 on the OSC wire and decoded plain text in
the API. Control characters and invalid Unicode are rejected. Hosts must not
interpret messages as markup and should disarm bidi overrides/invisible
formatting when showing them outside the terminal grid. Every record is
self-reported and untrusted.

OSC 9;4 progress and OSC 133 shell state retain their existing independent
APIs. This prototype does not map old progress reports into program-status
records.

## Network and browser presentation

OSC 7501 capability queries follow the terminal's existing query-ownership rules.
Hex1b answers for local workloads with headless, HWT1/browser, or HMP1 producer
presentations. It does not synthesize a reply when the presentation sets
`AnswersProtocolQueriesDirectly` (such as raw console passthrough), or when the
workload sets `HandlesProtocolQueries` (such as an HMP1 replica). The outer terminal
or upstream producer then owns the response. Report parsing and snapshot observation
remain enabled even when Hex1b does not own replies.

HMP1 negotiates a program-status extension and transfers an authoritative
checkpoint for attachment/reconnection. Live reports and lifecycle changes
preserve ordered replica state. Peers without the extension continue using
the existing handshake.

HWT1 frames include the complete bounded inventory, captured with terminal
state. The browser exposes immutable `terminal.programStatus`, `find(id)`,
`getDescendants(id)` and `onProgramStatusChange(snapshot)`. Callbacks deliver
the first presented inventory before mount resolves and then distinct changes.
Frames may coalesce intermediate reports; this is not a lossless event stream.
Disconnect/disposal retains the latest presented snapshot.

See [browser API documentation](../src/web-terminal/README.md#program-status-osc-7501),
[HMP1](muxer-protocol.md) and [HWT1](web-terminal-protocol.md) for transfer details.
The [WebTerminalDemo scenario](../samples/WebTerminalDemo/README.md#try-osc-7501-program-status)
provides a per-view Status panel and a real Hex1bApp callback-driven workload,
including a late-attached HMP1 relay.

For a local PTY tester, run
`dotnet run --project samples/OSC7501Demo`. The
[OSC7501Demo](../samples/OSC7501Demo/README.md) lets you choose a detected shell
and inspect its live report hierarchy alongside the embedded terminal, with
complete details for the selected record.
