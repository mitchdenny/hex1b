# WebTerminalDemo

An experimental, server-authoritative browser renderer for `Hex1bTerminal`.
The mounted multi-head proof-of-concept displays shared terminals in floating,
draggable/resizable windows and thumbnails. This remains experimental, not an
xterm.js compatibility layer or a promise of a stable wire protocol. Its browser component
is the TypeScript package [`@hex1b/web-terminal`](../../src/web-terminal/README.md).

For the architecture, lessons learned, gaps, and proposed production milestones,
see [Web terminal design notes](../../docs/web-terminal.md).
The [HWT1 internal implementation notes](../../docs/web-terminal-protocol.md)
describe state transfer between the first-party server and browser client.
**HWT1 is not currently intended for third-party implementation.** Both ends
evolve together without wire-compatibility guarantees; keep their versions
paired and upgrade them together.

## Run

From the repository root, using Node.js 24 or newer for the frontend build:

```sh
npm ci --prefix src/web-terminal
npm ci --prefix samples/WebTerminalDemo
dotnet run --project samples/WebTerminalDemo -c Release
```

Open <http://localhost:5290> in a browser with WebGPU or WebGL2 and worker
`OffscreenCanvas` support. The .NET build compiles the package and the
TypeScript playground, then copies the package's complete `dist/` tree to
`wwwroot/web-terminal/`. The playground consumes the package by its npm name,
resolved by a browser import map. The generated assets are included in
`dotnet publish`; Node.js is not needed by the deployed ASP.NET host. Neither
WASM nor xterm.js is required.

If another run occupies 5290, stop that run or choose a separate port:

```sh
dotnet run --project samples/WebTerminalDemo -c Release -- --urls http://localhost:5291
```

The sample only accepts loopback clients/hosts and same-origin WebSocket and
mutating HTTP requests. It allows at most four shared terminal instances and
eight web views. **The shell scene runs a real local shell
with your privileges. Do not put this sample behind a reverse proxy or expose
it to other users.** Closing a view does not stop its terminal: use **End terminal**
to terminate the shared workload explicitly.

By default, the **Interactive shell** scene enables
`Hmp1PresentationAdapter.WithReflow(GhosttyReflowStrategy.Instance)` on its
shared producer. Narrowing wraps shell output and widening rejoins soft wraps;
hard newlines remain separate. Retained history participates, subject to the
sample's 1,000-physical-row scrollback limit. Alternate-screen applications
still use crop/redraw semantics; their saved main screen reflows on return.
Selections are invalidated by resize. Generated text/graphics scenes retain
crop behavior. The library's adapter defaults have not changed: consumers must
[opt in on their producer](../../docs/web-terminal.md#shell-reflow-configuration).

## Choose a reflow strategy

Use **New terminal reflow** before clicking **New terminal** to compare the
built-in strategies without editing sample code. **Default** preserves the
scene policy above. You can explicitly select Ghostty, VTE, Kitty, WezTerm,
Alacritty, Windows Terminal, Foot, iTerm2, xterm, or **None (crop)**.
The iTerm2 and xterm strategies currently use crop behavior in Hex1b.
Cropping erases a wide glyph split by the right edge rather than retaining an
unpaired lead cell that would wrap incorrectly during replay.
**Auto (server environment)** detects the server's terminal environment, not
the browser; use a named strategy for reproducible comparisons.

The selection is applied once to the shared HMP1 producer during creation.
Changing the picker or attaching another view does not change an existing
terminal. The **Existing terminal** list shows each instance's reflow policy,
with Default resolved to Ghostty or None.

Both **Direct HWT1** and **HMP1 relay -> HWT1** honor the selection. Each demo
relay replica inherits its producer's reflow provider, including graphics-anchor
mapping; only the primary peer can resize the shared terminal. HMP1 does not
negotiate a reflow strategy with arbitrary remote consumers, so other hosts that
build terminal replicas must configure matching producer and replica policies.

For a repeatable experiment, open `/?scene=shell&reflow=Vte`; the `reflow`
query parameter uses the option values shown below and requests a new terminal
unless `empty=1` is also supplied. The HTTP creation API accepts the same choice:

```json
{"scene":"shell","columns":80,"rows":24,"reflowStrategy":"Vte"}
```

Valid values are `Default`, `None`, `Auto`, `Alacritty`, `Foot`, `Ghostty`,
`ITerm2`, `Kitty`, `Vte`, `WezTerm`, `WindowsTerminal`, and `Xterm`.
Omitting `reflowStrategy` uses Default; unknown values are rejected with HTTP
400. Creation responses and `GET /api/terminals` include `reflowStrategy`.

## Play a scenario tape

Create an **Interactive shell** terminal, or select one under **Existing terminal**.
At an idle shell prompt, choose a **Scenario tape** and click **Play tape**. The
picker follows the existing terminal's scene, not the **Scene** selector used to
create new terminals. The initial catalog includes command typing, line editing
and history, and (on Unix) ANSI colors using `printf`. Generated text/graphics
scenes do not currently have tapes.

Playback uses [`TapePlayer`](../../docs/tape.md) against the existing server-side
producer. It does not start a new shell, reset the screen, or change the terminal
size. Every attached view sees the same output; playback does not require primary
ownership or even an attached view. Avoid manual input while a tape is running.
The bundled tapes recognize common prompt endings; custom prompts may require
adjusting their `Wait` expressions.

Only one tape can run per terminal. The controls show running, completed,
cancelled, or failed status, including source locations for playback errors.
**Stop tape** cancels automation but does not undo input, send Ctrl+C, or interrupt
a shell command. If cancellation leaves a partially typed line, clear it yourself
before replaying. Closing views leaves playback running; ending the terminal,
workload exit, or server shutdown cancels and drains it before disposing the
producer. A run is limited to two minutes.

Tapes live in `Tapes/<scene>/<id>.tape` and are registered in
`DemoTapeCatalog.cs` with names, descriptions, and scene/platform restrictions.
They are parsed at startup and copied to build/publish output. Add a file and a
catalog entry, then restart the sample to offer another tape. The HTTP API accepts
catalog IDs only, not arbitrary paths or uploaded scripts. The catalog's parser
removes `Source` and `Output` from its syntax dictionary, so tapes containing
those commands fail parsing. This integration does not create recording files.

`GET /api/terminals` includes each instance's `tapes` catalog and latest
`tapePlayback` status. `POST /api/terminals/{id}/tape` with
`{"tapeId":"hello"}` starts playback; `DELETE` on the same route requests
cancellation. Both return 202 when accepted and require the same-origin `Origin`
header. Unknown scene/tape combinations return 400, missing terminals return 404,
and overlapping starts or cancellation without an active tape return 409.

### Optional focused checks

From the repository root:

```sh
npm test --prefix src/web-terminal
```

These zero-dependency Node tests cover sizing modes, font configuration and metric normalization,
peer/primary metadata validation,
native/thumbnail framebuffer sizing, large-grid GPU caps, hidden/restored
surfaces, and logical-uniform updates. They use a stub GPU interface and do not
exercise actual GPU rendering, mounted DOM/input, `ResizeObserver`, or view
lifetime. They exercise the package's compiled ES modules using Node's built-in
test runner.

With the sample serving assets and an existing WebGPU-enabled Playwright CLI
browser session, run the persisted mounted-DOM fixture from the repository root:

```sh
playwright-cli run-code "$(< samples/WebTerminalDemo/tests/mount.browser.js)"
```

The fixture uses the current page's HTTP origin, or `http://localhost:5290`
when the current page has no HTTP origin. It creates and closes an isolated DPR2
context, loads the actual mounted component, and mocks **only the Worker**.
It exercises real DOM, `ResizeObserver`, keyboard/mouse routing, primary versus
secondary fitting, read-only input, pending-mount focus isolation, abort, and
disposal. It creates no WebSockets or terminal instances and does not validate
actual worker/GPU rendering or end-to-end HMP1 lifetime.

Additional fixtures use the same Playwright CLI invocation and current-page
origin:

| Fixture under `tests/` | Coverage |
|---|---|
| `gpu.browser.js` | Real WebGPU readback of a native 3x3 sprite, framebuffer resizing, and retained image/glyph resources. |
| `fonts.browser.js` | Real font-rendered borders at five raster scales, Nerd Font symbols, delayed worker font readiness, per-view font selection, and font-load failure cleanup. |
| `sizing.browser.js` | Auto font-size controls, fixed-grid presets, keyboard selection, resize authority, and retained sizing policy across primary handoff. |
| `floating.browser.js` | Real workers/WebSockets/HMP1, dragging, primary-only resize, takeover, detach/reattach, and independent instances. |
| `resize-handles.browser.js` | Eight-direction window resizing, proximity highlights, pointer capture/cancellation, size/origin limits, and primary versus secondary/fixed-grid sizing. |
| `lifecycle.browser.js` | Closure overlays, native close details before/after mounting, rejected upgrades, local initialization failures, explicit reconnect, per-view isolation, and owner completion through direct/relay transports. |
| `input.browser.js` | Real POSIX shell input, Backspace, history, paste, MouseTest, thumbnail coordinates, and window-chrome focus. Build `samples/MouseTest` in Release first. |
| `tapes.browser.js` | Scene-filtered tapes in an existing shell, shared-view output, retained identity/geometry, overlap rejection, cancellation, visible failures, and shutdown cleanup. |
| `hyperlinks.browser.js` | Real OSC 8 output through HWT1 and the worker, Ctrl/Cmd activation, safe new tabs, selection/capture isolation, read-only thumbnails, destination updates, and scrollback. |
| `history.browser.js` | Shared producer history, independent viewports, character/word/logical-line/block selection, held/released wheel scrolling, clipboard intent, capture override, read-only inspection, and eviction. Clipboard writes are intercepted rather than changing the user's clipboard. |
| `reflow.browser.js` | Real shell output and retained history through repeated shrink/grow cycles, hard/soft breaks, wide/combining text, primary-only resize, selection invalidation, and editing a pending shell command. |
| `reflow-options.browser.js` | Creation-time strategy selection through the playground and HTTP API, preserved defaults, shared-instance policy, and real shell crop versus reflow through direct and relay views. |
| `bindings.browser.js` | Per-view input overrides, named actions, Windows-style right-click copy/paste, clipboard failures/races, capture ownership, and native text/paste/IME paths. Clipboard access is mocked. |
| `selection-ui.browser.js` | Default, augmented, and replaced selection controls; host CSS, highlight parts, canvas alignment, focus/input isolation, action reuse, UI errors, and disposal. |
| `graphics.browser.js` | Sixel and KGP in mixed WebGPU/WebGL2 views, renderer controls/diagnostics, cached-image movement, and late attachment to silent server-driven animation. |
| `graphics-stream.browser.js` | Reviewed HWT1 capture replay through the actual decoder and WebGPU/WebGL2 renderer, with pixel readback and a screenshot-ready canvas. This is offline frame replay, not a live WebSocket/worker check. |
| `graphics-worker-stream.browser.js` | A reviewed full HWT1 frame through the mounted client, actual worker and GPU. Only WebSocket transport is replaced; the mounted canvas stays available for screenshots until navigation. |
| `proteinview-live.browser.js` | An explicitly supplied ProteinView binary/model through this sample's actual shell scene, socket, mounted client and worker. Checks changing image presentations, viewport pixels and bounded browser image ownership. |
| `relay.browser.js` | Direct/relay transport selection, mixed peers, input and resize authority, primary closure, fresh reconnect/navigation-return replicas, retained KGP movement, and silent animation. |
| `titles.browser.js` | Real POSIX shell title output through direct HWT1 and HMP1 relay, initial/late/reconnect notifications, safe header text and fallback, reset retention, duplicate/resync suppression, and disposal. |
| `activity.browser.js` | Host-owned progress/severity and shell-phase chrome, direct and relayed current state, paused late attachment, resync, and fresh reconnect. No shell hooks required. |
| `cloud-flicker.browser.js` | Real shell-launched Sixel/KGP cloud animations, sampling visible canvas pixels over at least 180 browser frames and ten received updates; twenty fresh KGP views (including thumbnails) must retain sprites and keep animating without pixel re-uploads or stray command text. Build `samples/SixelCloudDemo` and `samples/KgpCloudDemo` in Release first. |
| `nested-flicker.browser.js` | WindowingDemo's Bash terminal running KittySearch: hover animation must keep painting through unrelated parent redraws, with at least 180 sampled browser frames and five distinct image states. Build `samples/WindowingDemo` and `samples/KittySearch` in Release first. Requires Bash. |
| `nested-sixel.browser.js` | WindowingDemo's Bash terminal running both SixelCloudDemo modes: native Sixel presentation, overlapping motes, and synchronized frame persistence. Build `samples/WindowingDemo` and `samples/SixelCloudDemo` in Release first. Requires Bash. |

The full-stack fixtures create and delete their own terminal instances. Run them
against an isolated demo server with capacity available, not a production host.
Headless Chromium may need `--enable-unsafe-webgpu` in its test launch configuration.
The GPU fixtures exercise real browser graphics APIs rather than the Node tests'
stub interfaces.

`renderers.browser.js` compares real WebGPU/WebGL2 pixels, uploads, clipping,
layering, glyphs, resizing and resource retention. It allows native horizontal
edge-coverage differences only where geometry ends exactly on a pixel center.
It also mounts real workers with only their WebSocket transport mocked on an
ordinary HTTP origin, exercising HWT1 decoding, frame acknowledgements,
automatic fallback and forced selection without weakening the sample host's
loopback-only access policy.

The package's Node regressions and TypeScript builds run in CI. The browser
fixtures remain focused, explicitly invoked checks; they are not a claim of
broad HMP graphics/performance stability or a browser/device compatibility matrix.

### Opt-in raw graphics investigations

`tests/Hex1b.Tests/Diagnostics/` contains an internal duplex workload recorder and
exact-chunk replay adapter. These record original byte arrays at the PTY adapter
boundary, including terminal replies; Tape, Asciinema, and HWT1 frame recordings
are not substitutes for this transcript. A PTY can split/coalesce application
writes, and an input write completing does not prove application consumption.

The ordinary `ProteinViewGraphicsStreamTests` exercise fragmented raw input,
picker replies, raw and zlib-compressed RGBA, PNG, and Unicode virtual placements.
The opt-in comparison records compressed and otherwise equivalent uncompressed
streams and checks that both retain the same exact pixels:

```sh
HEX1B_GRAPHICS_EVIDENCE=/absolute/new/private/evidence-directory \
  dotnet test tests/Hex1b.Tests/Hex1b.Tests.csproj --no-progress \
  --filter "FullyQualifiedName~ProteinViewGraphicsStreamTests"
```

To capture the real application, first build a reviewed ProteinView checkout
locally. The investigation baseline is upstream commit
`9b9a0790bc78f1d2a7c0923905049d27c67c05e2`; the test never downloads or installs it.
Supply an absolute binary path and the bundled model:

```sh
HEX1B_PROTEINVIEW_EXECUTABLE=/absolute/ProteinView/target/release/proteinview \
HEX1B_PROTEINVIEW_MODEL=/absolute/ProteinView/examples/4HHB.pdb \
HEX1B_GRAPHICS_EVIDENCE=/absolute/new/private/application-evidence \
  dotnet test tests/Hex1b.Tests/Hex1b.Tests.csproj --no-progress \
  --filter "FullyQualifiedName~ProteinViewInvestigationTests"
```

This Unix-only runner launches the binary directly, not a shell. Each case uses
80x24 cells, a 20-second deadline, a 64 MiB/100,000-event capture budget, and
four-second sampling windows. Cases cover plain/Braille/FullHD, uppercase `M`,
safe observed terminal hints versus a separate normalized environment, and
HMP1 producer-backed versus direct HWT1 projection. It records binary/model
hashes, picker logs, byte transcripts and sampled HWT1 frames. It does not
reproduce arbitrary inherited environments or a native Ghostty session.
SSH/session identifier values are never copied; only their presence is retained.
Do not run under tmux, where upstream may change passthrough configuration.
Without the explicit environment variables these investigation cases are skipped.

Keep captures private and review them before sharing. The binary runs with your
privileges; supply only a reviewed executable and local input. An interrupted,
failed, or budget-exceeded transcript is not a successful reproduction.

For browser readback, use a loopback-only static test host serving the matching
built client at `/web-terminal/` and **only reviewed `.hwt` files** at `/evidence/`.
Do not expose raw transcripts, environment metadata, arbitrary files, or a
production host. Navigate to
`/?frame=rgba-control.hwt&backend=webgpu`, then run:

```sh
playwright-cli run-code "$(< samples/WebTerminalDemo/tests/graphics-stream.browser.js)"
```

Use `backend=webgl2` for that renderer. Repeat `frame` parameters in recorded
order when replaying deltas (for example, before/after `M`). The optional
`minimum` asserts a red-pixel lower bound for the synthetic red image. Without it,
the fixture reports pixel counts for investigation; zero images or pixels do not
constitute success. Real FullHD acceptance requires visible molecular output in
the viewport, not just an ACK, texture allocation, or colored header text.

For the mounted-worker check, navigate to a full-frame artifact with `?frame=...`
and invoke `graphics-worker-stream.browser.js` instead. It runs the real client
and worker with only the socket replaced, so it can distinguish renderer-only
fixture effects from actual client behavior. It does not establish live network
transport behavior. Navigate away or close the isolated browser to dispose it.

To inspect an already reviewed reduced raw-output file (without launching an
application), use:

```sh
HEX1B_GRAPHICS_STREAM=/absolute/reduced-output.bin \
HEX1B_GRAPHICS_REPLAY_OUTPUT=/absolute/new/private/replay-directory \
  dotnet test tests/Hex1b.Tests/Hex1b.Tests.csproj --no-progress \
  --filter "FullyQualifiedName~GraphicsStreamReplayTests"
```

The replay runner limits input to 8 MiB, rechunks to 997 bytes to exercise raw
framing, and appends a last-row processing marker. It saves retained-image hashes
and an HWT1 frame. Preserve the original capture and a transformation manifest
separately: replay rechunking and the marker are not original application bytes.

## ProteinView live and sustained validation

After starting this sample, the live fixture can run a reviewed, already-built
ProteinView executable through its normal shell scene. It does not download or
modify the application. Use an isolated browser and a task-owned sample process
with a clean POSIX shell (`SHELL=/bin/sh`); the fixture uses POSIX argument quoting.
Supply URL-encoded absolute paths where necessary:

```sh
playwright-cli -s=proteinview-validation open \
  'http://localhost:5290/health?executable=/absolute/ProteinView/target/release/proteinview&model=/absolute/ProteinView/examples/4HHB.pdb&backend=webgpu'
playwright-cli -s=proteinview-validation run-code \
  "$(< samples/WebTerminalDemo/tests/proteinview-live.browser.js)"
```

The fixture creates its own terminal, runs `--fullhd`, toggles auto-rotation and
waits for 50 observed image-upload/presentation changes. It reads actual
compositor pixels rather than a renderer-only mock. The result includes peak
texture/image/atlas counters. These browser counters are not the producer's
retained-memory counters. Use `backend=webgl2` for the other renderer.

On success the application stays paused for screenshots or reconnect checks.
Run the lifecycle fixture in that same browser to check FullHD/HD/Braille mode
switching and 20 reconnects alternating direct HWT1 and HMP1-replica views:

```sh
playwright-cli -s=proteinview-validation run-code \
  "$(< samples/WebTerminalDemo/tests/proteinview-lifecycle.browser.js)"
```

It checks one 800x400 RGBA texture after each reconnect and zero image textures
in text modes. These dimensions belong to the pinned application/model and
80x24 test geometry, not a general meaning of "FullHD". To save screenshots,
set `window.proteinViewEvidenceDirectory` to an existing private absolute
directory before invoking it. The fixture preserves the workload on completion
or failure for inspection.

The caller must dispose `window.proteinViewAcceptance.terminal` and end the
owned instance with `DELETE /api/terminals/{instanceId}` from the same origin,
then close the isolated browser. Closing the browser alone does not terminate
the sample's shared workload.

For bounded **server-side** actual-application ownership measurements, run the
opt-in sustained capture separately:

```sh
HEX1B_PROTEINVIEW_EXECUTABLE=/absolute/ProteinView/target/release/proteinview \
HEX1B_PROTEINVIEW_MODEL=/absolute/ProteinView/examples/4HHB.pdb \
HEX1B_GRAPHICS_SUSTAINED_EVIDENCE=/absolute/new/private/sustained-directory \
  dotnet test tests/Hex1b.Tests/Hex1b.Tests.csproj --no-progress \
  --filter "FullyQualifiedName~Capture_ExplicitSustainedRun"
```

This run requires 200 distinct accepted image generations within 60 seconds and
a 64 MiB/100,000-event raw duplex budget. It records physical encoded retention,
decoded-capacity reservations and acknowledged HWT1 frames independently.
It deliberately retains an initial snapshot and one caller-owned decoded array.
Reported allocation churn includes capture and projection overhead; it is not a
benchmark or a process-wide memory bound. Ordinary snapshot/image references
remain valid after live eviction or disposal, so retaining them extends their
lifetime outside the terminal's current-screen accounting.

Set `HEX1B_GRAPHICS_ACK_INTERVAL_MS=16` for a separate, explicitly paced HWT1
acknowledgement comparison. The default is immediate acknowledgement. Pacing
changes capture/projection frequency and allocation overhead; it does not
simulate or replace measurements from the actual browser connection.

### Compressed image ownership

Kitty zlib uploads are validated completely before publication. The terminal
retains their compressed backing in the existing `KgpImageData`, so snapshots
and non-web consumers see authoritative image data too. Each `Data` access on a
compressed image returns a fresh caller-owned array in its declared format
(RGB, RGBA, or PNG bytes). Read it once per operation; the terminal does not keep
a decoded-array cache. Existing uncompressed image behavior is unchanged.

The live-screen budget charges actual compressed storage plus a conservative
decoded-capacity reservation. That is not a process-memory limit: chunk assembly,
validation, transport projection and caller-owned arrays have separate transient
costs, and retaining old snapshots keeps their ordinary image references alive.
Snapshot disposal does not invalidate those references. HWT1 drops obsolete KGP
generations from its current projection cache while preserving resources still
used by current placements; already emitted frames own their payloads separately.

Malformed checksums, truncation, invalid output sizes and exceeded limits reject
the upload rather than publishing a partially decoded image. One complete zlib
member is interpreted; bounded trailing input is ignored but retained and charged,
not interpreted as another image.

## Shared instances and floating views

Drag a window's title bar to move it. Resize from any of its four edges or four
corners: each bar highlights as the pointer approaches the border, shows the
appropriate resize cursor, and stays highlighted while dragging. Top and left
handles keep the opposite edge fixed, stopping at the workspace origin; all
handles respect the window's 240×180 minimum and 3200×2200 maximum size.

- **New terminal** creates a persistent producer, initially 100×30, and mounts a
  view that explicitly requests primary.
- **Attach view** and **Thumbnail** join an existing instance as secondary, even
  when no primary is assigned. Views read the same producer state and retained
  history; each has its own viewport, selection, worker, canvas, and resource caches.
- **Take primary** requests HMP1 primary. The confirmed role controls resize,
  not input: all peers can type, paste, and send mouse input.
- In **Auto**, resize the primary window or use its footer's **- / +** controls
  to fit more or fewer cells. Text size ranges from 8 to 32, with 16 the default.
- Choose a footer resolution preset such as **80x24** or **120x40** to hold that
  grid and scale it to fit the window. Return to **Auto** to restore the previous
  text size. Secondary windows always follow the primary's grid and fit it to
  their own size; their sizing controls are disabled.
- **Resync** refreshes that view's current-state baseline.
- **Close view** detaches one view. Closing the primary preserves the last grid
  and leaves primary unassigned; a remaining view must explicitly take primary.
- **End terminal** deletes the shared producer and ends all attached views,
  leaving each window's reason overlay visible until **Close view**.

Instances persist with zero views until End terminal, workload exit, or server
shutdown. Retention is process-local, not durable storage or automatic reconnect.
The instance list and shared rate/batch/pause controls use `/api/terminals`
HTTP endpoints; a view connects to `/ws?instance={id}&name={displayName}`.
See the [host binding](../../docs/web-terminal-protocol.md#9-sample-websocket-host-binding-and-lifetime)
for the complete route and lifetime boundary.

On initial page load without a scene query, the playground attaches the first
existing instance without claiming primary, or creates a mixed instance if the
registry is empty. A supported `?scene=...` selects a newly created workload.
`?empty=1` suppresses automatic view creation/attachment for browser checks;
the normal controls remain available.

### Closed views and failure demonstrations

A closed view stays in its floating window with an overlay covering the terminal.
It summarizes the outcome and displays the native WebSocket code, reason, and
closing-handshake status supplied by `WebTerminalOptions.onClose`, including
closure before mounting finishes. Local initialization failures have an overlay
too, but no invented WebSocket status. Reasons are displayed as text, never HTML.
The terminal and its input/resize/takeover controls are disabled; the window can
still be moved, resized, or closed. The worker and renderer are disposed rather
than retained behind the overlay; final-screen preservation is not part of this
demonstration.

Each window has a **Failure** picker and **Trigger** button. These affect only
that connection, not its producer, other viewers, or scenario tapes:

| Condition | Browser observation |
|---|---|
| Graceful close | Code 1000 with `Demo: graceful view closure`. |
| Abrupt connection loss | Code 1006, no received close reason, incomplete handshake. |
| Policy violation | Code 1008 with `Demo: policy violation`. |
| Server error | Code 1011 with `Demo: server failure`. |

Use **New view failure** before **Attach view**, **Thumbnail**, or **New terminal**
to close or drop the connection before its first HWT frame, or reject its HTTP
WebSocket upgrade with 503. Browsers report rejected upgrades as 1006 without
exposing the HTTP response or a native reason; the overlay deliberately does
not claim it can distinguish that from other connection failures. These modes
work with both **Direct HWT1** and **HMP1 relay**.

There is no automatic reconnect. **Reconnect view** creates a fresh connection
inside the same window, retaining its transport but using the current font,
scale, and renderer settings. It bypasses the new-view failure picker and does
not take primary. A clean 1000 closure is not proof of producer completion.
To demonstrate actual completion, choose **End terminal**, or type `exit` in an
interactive shell. The sample sends application close code **4000** with the
owner-stop or workload-exit reason; these overlays disable reconnect. Workload
failures instead report 1011 with their sample-host failure reason.

Code 4000 is a **WebTerminalDemo-only host convention**, not an HWT protocol
meaning or library reconnect policy. Production hosts define their own
completion and retry policy around the public close callback.

The demo control endpoint is
`POST /api/terminals/{id}/views/{viewId}/failure` with
`{"mode":"close"}`, `"abort"`, `"policy"`, or `"server-error"`. The view GUID
comes from the `/ws` connection's optional `view` query parameter. The optional
`failure` query accepts `before-frame-close`, `before-frame-abort`, or
`reject-upgrade`; omitted/empty means normal attachment. The controls inherit
the sample's loopback and same-origin restrictions and are not public HWT
messages or production fault-injection APIs.

**New view renderer** chooses Auto (prefer WebGPU), WebGPU, or WebGL2 for newly
opened views. `?renderer=webgl2` selects WebGL2 on initial load; `auto` and
`webgpu` are also accepted. Existing views keep their backend until remounted.
The selected-view metrics show the active backend and any automatic fallback
reason. WebGPU requires HTTPS or localhost; the package can use WebGL2 on
ordinary HTTP, but this demo's loopback-only host policy remains unchanged.

### Progress and shell activity

Choose **Progress and shell activity**, or open `?scene=activity`, to run a
controlled, repeating demonstration at one step per second. It emits OSC 133
prompt/input/execution/completion markers and OSC 9;4 busy, determinate,
warning, error, and clear states. These are simulated shell markers: the scene
does not execute commands or install shell hooks.

The activity strip is owned by this sample, outside the mounted terminal.
It uses only `onProgressChange` and `onShellIntegrationChange`, with connection
status to suppress stale active chrome. The terminal's title remains independent.
Pause the producer while busy or showing a warning, select **HMP1 relay**,
and **Attach view**: the new view should immediately show current activity.
Close and reattach it, or press **Resync**, to exercise state restoration.
Resume to watch progress and the latest command result change together.

These are coalesced current-state notifications, not a history of every command.
For .NET consumers, `Hex1bTerminal.Progress`, `ShellIntegration`, and their
matching change events expose the same state, captured atomically by
`CreateSnapshot()`. An unknown shell phase does not mean idle; a nullable
exit status does not mean success.

### Optional HMP1 relay

**New view transport** defaults to **Direct HWT1**, which reads the shared
producer's state without replaying it into another terminal. Select **HMP1 relay
-> HWT1**, or open `?transport=hmp1`, to exercise the serialized path:

```text
Producer Hex1bTerminal -> HMP1 -> per-view Hex1bTerminal -> HWT1 -> browser
```

Each browser connection creates a fresh HMP1 client and terminal replica over
bounded in-memory duplex pipes. This uses the real HMP1 handshake, state/image
replay, live output, input, and primary/resize messages without requiring a
socket or another process. Closing the view disposes its peer and replica, not
the shared producer. Attaching again or reloading creates a new replica.
Initial screen replay preserves hard breaks and soft continuations, including
wide-character wrap padding, so already-wrapped output can reflow after attaching.

The selector applies to newly opened views, including thumbnails. Existing
views retain their transport; their title and selected-view metrics show it.
Direct and relay views can inspect the same instance side by side. The
WebSocket route accepts `transport=direct` (also the default when omitted) or
`transport=hmp1`; other values are rejected before accepting the connection.

All scenes and workload controls work in both modes. For retained-image replay,
start the Kitty graphics or Graphics animation scene, then attach a relay view
after pixels were uploaded, close it, and attach again. The shell scene can run
`KgpCloudDemo` to exercise upload-once sprites and synchronized placement
replacement. Relay views receive the live screen at attachment and accumulate
their own subsequent scrollback; they do not receive the producer's pre-existing
history. Direct views retain the existing shared-history behavior.

Run `tests/relay.browser.js` with the Playwright CLI invocation above for a
focused direct/relay lifecycle comparison. The existing `graphics.browser.js`,
`floating.browser.js`, and `cloud-flicker.browser.js` fixtures inherit
`transport=hmp1` from the current page URL, or default to direct mode:

```sh
playwright-cli goto 'http://localhost:5290/?empty=1&transport=hmp1'
playwright-cli run-code "$(< samples/WebTerminalDemo/tests/cloud-flicker.browser.js)"
```

## Mount in a sized element

[`@hex1b/web-terminal`](../../src/web-terminal/README.md) exports the
`WebTerminal` class. It does not depend on the playground's IDs, window manager,
or controls. The caller must provide a sized outer element; the mount appends
one wrapper with its own shadow root and does not take ownership of the container.

This HTML snippet assumes the sample's assets and HTTP endpoints are served
from the same origin. It creates a producer deliberately; attach an existing ID
instead when another view should share an existing terminal.

```html
<div id="example-terminal" style="width: 800px; height: 480px"></div>
<script type="module">
  import { WebTerminal } from "/web-terminal/index.js";

  const response = await fetch("/api/terminals", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ scene: "mixed" })
  });
  if (!response.ok) throw new Error(`Create terminal failed: ${response.status}`);
  const instance = await response.json();
  const lifetime = new AbortController();
  const view = await WebTerminal.mount(document.getElementById("example-terminal"), {
    url: `/ws?instance=${encodeURIComponent(instance.id)}&name=Embedded`,
    scale: "auto",
    readOnly: false,
    signal: lifetime.signal,
    label: "Embedded terminal input",
    onStatus: (message, level) => console.log(level, message)
  });
  view.requestPrimary(); // Explicit claim for this newly created instance.
  view.focus();
</script>
```

`mount` resolves after a **presented frame with a connected peer** (or standalone
primary), not merely an open socket or a pending HMP1 handshake.
`requestPrimary()` returns without waiting for a grant; `onRoleChange(peer)`
confirms the HMP1 result. Attaching an existing instance should normally omit
that call until the user chooses Take primary.

| Options / handle members | Current sample behavior |
|---|---|
| `url` | Required view WebSocket URL; the sample uses the page's origin. |
| `workerUrl` | Optional URL for the package's worker entry when your asset pipeline hosts it elsewhere. Preserve its relative module dependencies; defaults to the module-relative packaged worker. |
| `scale` | `"auto"` (default) snapshots DPR at mount and clamps it to 0.5..3; an explicit numeric raster-scale setting must also be 0.5..3. |
| `font` | Optional `{ family, faces?: [{ url, weight?, style? }] }`. Defaults to bundled Cascadia Mono NF; see font selection below. |
| `sizing` | Initial sizing policy: `{ mode: "auto", fontSize: 16 }` by default, or `{ mode: "fixed", columns, rows }`. Secondary views still follow the primary. |
| `readOnly` | Defaults false. Disables application input while retaining local history, selection, and copy; not server authorization, a role, or a prohibition on taking primary. |
| `signal`, `label` | Optional lifetime cancellation and input accessible label. An aborted signal disposes the view. |
| `onStatus(message, level)` | Status/error notification. |
| `onGeometry(geometry)` | Initial geometry, then changes to columns, rows, cell metrics, or mouse tracking; not a callback on every frame. |
| `onRoleChange(peer)` | Changes to `{ id, primaryId, isPrimary }`, reflecting HMP1 state. |
| `onSizingChange(sizing)` | Initial sizing policy, then changes made through `setSizing`. This reports local intent, not an authoritative grid acknowledgement. |
| `onViewportChange(viewport)` | Current text viewport, following/offset state, and pending navigation. Independent of producer resize. |
| `onSelectionChange(selection)` | Authoritative selection, pending/invalidation state, and clipboard-operation status. |
| `onSelectionUI(event)` | Synchronous, cancelable selection-UI event. Augment the default Copy UI or replace it with host-owned controls; see Selection UI below. |
| `onStats(stats, text)` | Per-view statistics and, when supplied, current screen text. |
| `inputBindings`, `onInput`, `actions` | Per-view bindings, synchronous interception, and named host actions; see Input customization below. |
| `onInputError(error)` | Errors from UI-dispatched actions or input-policy callbacks. Errors also appear in the view's inspection status. Public API calls reject/throw to their caller. |
| `focus()` | Focus this view's input, or its wrapper when input is disabled. |
| `requestPrimary()` | Request primary using the retained sizing policy: the font-sized fitted grid in Auto, or the configured fixed grid. Requires a visible container; no optimistic role change. |
| `resize(columns, rows)` | One-off primary-only request, 20..300 columns by 10..100 rows; no optimistic reflow or sizing-policy change. |
| `setSizing(sizing)` | Change the connected primary's Auto/font-size or fixed-grid policy. Grid requests are throttled and applied by the server. |
| `scrollLines(delta)` | Request a relative text scroll; positive values move toward live output. Does not require primary. |
| `scrollToLive()` | Return this viewport to live output without resizing or taking primary. |
| `clearSelection()` | Clear this view's selection. |
| `copySelection({ clear: false })` | User-initiated clipboard write; resolves to the copied text. Rejects unavailable, pending, changed, or evicted selections and clipboard failures. Optional `clear: true` clears only the same selection after a successful write, never a newer selection. |
| `paste(text)`, `pasteClipboard()` | Paste supplied text or read the system clipboard during a user gesture. Returns to live and uses server-side bracketed-paste encoding. Read-only/disconnected views reject these calls. Clipboard reads reject if selection, focus, buffer generation, or application input changes while awaiting permission/data. |
| `runAction(name, args)` | Invoke a built-in or registered host action; returns a promise. Used by controls and input bindings alike. |
| `refreshSelectionUI()` | Force another selection-UI notification after changing host-side rendering preferences. Ordinary state/layout changes notify automatically. |
| `inputBindings`, `inputContext` (getters) | Inspect the effective bindings and a snapshot of this view's routing context. |
| `resync()` | Request a new baseline for this connection. |
| `dispose()` | Idempotently disconnect and remove this view's wrapper; leave the container and shared terminal intact. |
| `geometry`, `sizing`, `peer`, `connected`, `stats`, `screenText`, `element` | Current view state and the owned wrapper element. `sizing` includes the retained Auto `fontSize` even while fixed. |
| `viewport` | Includes `available`, `followTail`/`following`, `offset` above live, `pending`, producer generation, buffer, and row identities. |
| `selection` | Includes `active`, `pending`, `status`, `mode`, `text`, visible highlight `ranges`, `copying`, and `copyError`. Wire `valid` maps to `active: true`; expiry is `status: "invalidated"`. |

### Input customization

The client has a small browser-local input/action layer, not a copy of
Hex1bApp's widget router. No JavaScript binding definitions or action names are
sent over HWT1. Import `TerminalAction`, `InputRoute`, and
`defaultInputBindings` from `@hex1b/web-terminal` alongside `WebTerminal`.

Resolution order is `onInput` interception, consumer bindings in array order,
then remaining default bindings. The first non-`continue` decision wins.
Each binding has a stable `id`, a synchronous `match(input, context)` predicate,
an optional synchronous `when(context, input)` predicate, and exactly one of
`action` or `route`. A consumer entry with a default ID replaces that default;
`{ id, remove: true }` removes it. Duplicate IDs, unknown removals/actions, and
invalid callback results fail explicitly. Configuration is captured at mount;
predicates can consult current context or host-owned preferences.

This mount-options example assumes the host has created `container` and obtained
the instance's WebSocket `url` as in the preceding example:

```js
import { WebTerminal, TerminalAction, InputRoute } from "/web-terminal/index.js";

const view = await WebTerminal.mount(container, {
  url,
  actions: {
    "host.inspect": (context, args, input) => {
      console.log(context.buffer, context.selection, input.point);
    }
  },
  inputBindings: [
    { id: "clipboard.copy-key", remove: true },
    {
      id: "host.copy",
      match: input => input.type === "key" && input.ctrl && input.shift &&
        !input.alt && !input.meta && input.key.toLowerCase() === "y",
      action: TerminalAction.CopySelection
    },
    {
      id: "host.alternate-context",
      match: input => input.type === "pointer" && input.button === "right",
      when: context => context.buffer === "alternate",
      action: "host.inspect"
    }
  ],
  onInput: input => input.type === "key" && input.key === "F5"
    ? InputRoute.Application : InputRoute.Continue,
  onInputError: error => console.error(error)
});
```

The alternate-screen override is additive: normal-screen right-click retains
the default copy/paste policy. Giving it `id: "clipboard.context-click"` instead
would replace that default entirely.

| Default binding ID | Behavior |
|---|---|
| `clipboard.copy-key` | Cmd+C or Ctrl+Shift+C invokes `copySelection`, retaining selection. |
| `clipboard.context-click` | A local right-click invokes `copyOrPaste`. Copies and clears a selection after success; without a selection, pastes the clipboard. Application capture wins unless Shift is held or the view is historical/read-only. |
| `browser.shortcuts` | Retains browser shortcut ownership, including native paste. Consumers can override events delivered by the browser, but cannot guarantee interception of OS/browser-reserved shortcuts. |
| `terminal.keys` | Forwards navigation/function/control keys that were not handled earlier. Ordinary printable keys use subsequent committed-text events instead. |

Unmatched pointer/wheel input continues through the existing selection,
scrolling, and application-mouse gesture handling. Replace it with a `consume`
binding to disable a gesture; `browser` leaves native browser handling alone,
and `application` explicitly bypasses local handling. Pointer ownership is
chosen on the initial press and retained through release/cancel; callbacks are
not rerun on move/up, and wheel overrides are not reevaluated during a held
gesture. This prevents unmatched application button releases.

`onInput(input, context)` may return `undefined`/`continue`, `consume`,
`application`, `browser`, or `{ action, args }`. `continue` tries later bindings;
`consume` performs no further handling. Binding `action` can be a registered
name or a callback `(context, args, input)`. Action callbacks may be async, but
matching/interception must finish synchronously so browser cancellation and
clipboard activation are decided during the event. Returning a route from an
action callback does not reroute an already-consumed event.

Built-in names are exported through `TerminalAction`: `CopySelection`,
`PasteClipboard`, `CopyOrPaste`, `ClearSelection`, `ScrollToLive`, and
`ScrollLines`. `ScrollLines` takes a signed number as `args`; `CopySelection`
accepts `{ clear: true }`. Host action names must not shadow built-ins.
Direct `runAction` calls have no originating input object.

Normalized input types are `key` (key/code/repeat and ctrl/alt/shift/meta),
`pointer` (initial press, button and cell point), `wheel` (deltaX/deltaY/deltaMode
and point), `text` (committed text), and `paste` (plain text from a browser paste
event). Pointer/wheel intents also include modifiers. Context contains
`terminal`, `selection`, `viewport`, `buffer` (`main`/`alternate`/null),
`mouseCaptured`, `historical`, `readOnly`, `connected`, and `peer`.

A `browser` key decision leaves subsequent native text/paste events intact;
it does not suppress the entire browser editing lifecycle. Use `consume` to
block the keystroke, or intercept `text`/`paste` to control committed input.

IME/dead-key/AltGraph keydown events bypass shortcut matching; committed text
is still interceptable. Native keyboard paste stays browser-owned until its
paste event supplies text; it does not require `clipboard.readText()`. Explicit
mouse paste does require browser clipboard-read access. Denial is reported,
not silently converted to typing or a successful paste. Meta has no HWT1
terminal-key encoding, so map it to a local action rather than the
`application` key route. Read-only remains enforced on all terminal input
paths, even custom bindings; it is still not server-side authorization.

Pastes whose serialized UTF-8 HWT1 command exceeds 64 KiB are rejected locally,
rather than disconnecting the view or silently truncating text. Large/multiline
paste confirmation UI is not implemented yet.

### Selection UI

`onSelectionChange` remains a state notification. `onSelectionUI` supplies an
owned overlay surface for visual treatment. It receives the same cancelable
`selectionui` DOM event available on `view.element`; its payload is
`event.detail`. Call `event.preventDefault()` to suppress only the built-in
Copy control for that update. Leave the event uncanceled to augment the
default. Selection highlighting, Return to live, and error feedback remain
independent.

| Detail field | Meaning |
|---|---|
| `overlay` | Stable light-DOM element, slotted above the fitted canvas. Host stylesheets and framework portals can reach its children without accessing the terminal's shadow DOM. |
| `selection`, `viewport`, `geometry` | Immutable snapshots of the view's selection, viewport, and authoritative terminal geometry. Selection includes `copying`, `pending`, and `copyError`. |
| `canvasSize` | Fitted canvas layout width/height in **overlay-local CSS pixels**, before ancestor transforms and independent of GPU/device pixels. |
| `rects` | Visible selection rectangles with `left`, `top`, `width`, and `height`, relative to `overlay` in CSS pixels. Offscreen selections may have no rectangles. |
| `connected`, `readOnly` | Whether the view is connected and whether application input is disabled. Read-only views still allow copying. |
| `runAction(name, args)` | The existing guarded action dispatcher. Invoke it from an actual user gesture; state/render notifications must not automatically write the clipboard. |
| `signal` | Aborts on view disposal or failed mounting. Use it for event listeners and framework unmount/cleanup. |

The view owns the layer's position, dimensions, pointer policy, and error
visibility; hosting code owns its children. The layer is pointer-transparent.
Opt interactive children into `pointer-events: auto`. Keyboard, pointer, and
wheel interaction with these controls does not become terminal input, and the
terminal's keybindings do not intercept editing inside custom controls.

Inside your mount code, using your `container` and WebSocket `url`:

```js
import { WebTerminal, TerminalAction } from "/web-terminal/index.js";

let copyButton;
const view = await WebTerminal.mount(container, {
  url,
  onSelectionUI(event) {
    event.preventDefault(); // Omit this to keep the stock Copy button as well.
    const { overlay, selection, connected, signal, runAction } = event.detail;
    if (!copyButton) {
      copyButton = document.createElement("button");
      copyButton.type = "button";
      copyButton.style.cssText = `
        position: absolute; right: 8px; top: 8px; pointer-events: auto;
        max-width: calc(100% - 16px); font: inherit; padding: 4px 8px;
        background: var(--cp-view-surface); color: var(--cp-view-text);
        border: 1px solid var(--cp-view-accent); border-radius: .625rem;
      `;
      copyButton.addEventListener("click", () => {
        runAction(TerminalAction.CopySelection).catch(error => console.error(error));
      }, { signal });
      signal.addEventListener("abort", () => copyButton.remove(), { once: true });
      overlay.append(copyButton);
    }
    copyButton.hidden = !selection.active;
    copyButton.disabled = !connected || selection.copying;
    copyButton.textContent = selection.copying ? "Copying..." : "Copy selected text";
  }
});
```

Create controls once and update their state; do not replace a focused subtree
on every notification. Events are coalesced after coherent geometry/history
updates and fire on meaningful selection, viewport, connection, or displayed
size changes, not merely a new frame revision. The overlay moves with the
canvas when its enclosing window moves, so its rectangles intentionally do
not contain page coordinates. `refreshSelectionUI()` forces a refresh if a
host preference changes without terminal state changing.

Registering a DOM listener after `mount()` may miss initial events; call
`refreshSelectionUI()` after registration if an immediate snapshot is needed.
Handlers must finish synchronously to decide default rendering. Asynchronous
actions still run from control callbacks. Errors in the `onSelectionUI` option
hide the failed custom layer, suppress default Copy, and appear in independent
status feedback/`onStatus`; a later successful refresh restores the layer.
Additional DOM listeners use normal browser event/error semantics.

For styling without replacing controls, the view exposes CSS parts:
`selection-highlight` for each highlighted span, `selection-highlights` for
their container, and `selection-copy-button` for the stock button. For example:

```css
.hex1b-terminal::part(selection-highlight) {
  opacity: .5;
}
```

### Font selection

The default is the unmodified **Cascadia Mono NF** variable WOFF2 from Microsoft's
Cascadia release `v2407.24`, also used by Aspire's embedded terminal. It includes
Nerd Font symbols without programming ligatures. The font, original OFL 1.1
license, and provenance/hash are shipped with the package under
`dist/fonts/cascadia-mono-nf/` and served here from
`wwwroot/web-terminal/fonts/cascadia-mono-nf/`. Keep the license and copyright
notice with the font in every distribution; the font is not relicensed
under Hex1b's code license.

Developers can choose another family per mount. For example, with your own
licensed font files served at these illustrative URLs:

```js
const view = await WebTerminal.mount(container, {
  url: `/ws?instance=${encodeURIComponent(instanceId)}`,
  font: {
    family: "My Terminal Font",
    faces: [
      { url: "/fonts/my-terminal-regular.woff2" },
      { url: "/fonts/my-terminal-bold.woff2", weight: "700" },
      { url: "/fonts/my-terminal-italic.woff2", style: "italic" }
    ]
  }
});
```

`family` is a single family name, not a CSS fallback list. Face URLs resolve
relative to the caller's page. `weight` and `style` are CSS FontFace descriptor
strings, defaulting to `"400"` and `"normal"`; a variable font can declare a
range such as `"200 700"`. Styles not supplied use the browser's font matching
and synthesis. Custom cross-origin assets need appropriate CORS headers.

For an installed family, omit `faces`; it is loaded using `local()` and fails
explicitly if unavailable. `{ font: { family: "monospace" } }` deliberately
selects the browser's generic monospace family. The playground's **New view
font** selector offers that comparison and applies only to subsequently opened
views. `view.stats.fontFamily` reports the selected family.

Fonts load into each rendering worker's own `FontFaceSet` before connecting its
WebSocket or rasterizing glyphs. Page-level `@font-face` rules do not populate
worker fonts. A configured font-load failure rejects the mount rather than
caching fallback glyphs or silently connecting with another font.

The selected font owns text, borders, block characters, and icons; no procedural
replacement glyphs are drawn. For each regular/bold/italic style, the renderer
measures the font's reference advance, ascent, and descent and maps that line box
to the authoritative 10x20 cells. All glyphs in that style use the same transform;
individual graphemes are not stretched to fill their span. This removes the old
hard-coded baseline and font/cell-size mismatch while preserving server-owned
widths, image geometry, and mouse coordinates. Custom fonts still determine
their own glyph coverage and whether their border outlines connect.

Font family/source selection is fixed for a view's lifetime. Remount to select
another family; each new view starts with a fresh glyph atlas. Text display size
can change live using the sizing policy below without reloading the font.

### Auto text size and fixed grids

```js
view.setSizing({ mode: "auto", fontSize: 12 }); // Smaller text, more cells.
view.setSizing({ mode: "fixed", columns: 120, rows: 40 }); // Hold this grid.
view.setSizing({ mode: "auto" }); // Return to the previous Auto font size.
```

These calls require the connected primary. A view retains its policy when it
loses primary, but stops issuing automatic resizes and follows the accepted
remote grid. Taking primary again uses the retained policy. Choosing a preset
does not resize the floating window; resizing a fixed-grid window does not
resize the producer.

`fontSize` is an integer from 8 to 32. It controls display-cell scale relative
to the original 16-size presentation: 12 uses 7.5x15 CSS-pixel cells instead of
10x20. The server still owns 10x20 logical-pixel cells, so images and mouse
coordinates remain consistent. Local requests retain their 20..300 by 10..100
grid bounds; when those bounds prevent the requested text size from fitting,
the surface scales down to remain visible. Fixed grids and secondary views
always contain-fit, without an Auto font-size cap.

The glyph atlas and maximum raster scale remain unchanged by these controls.
Increases beyond the configured raster resolution can magnify the existing
glyph pixels; these are display sizing controls, not live font-face replacement
or an unbounded higher-resolution raster cache.

### Cell fitting and lifetime

The inner surface keeps the authoritative `columns*10` by `rows*20` aspect ratio
and contain-fits the outer box. An Auto primary caps display scale at its
requested font size between whole-cell resize increments, but can scale down
during an in-flight resize or when limits prevent a fit. A fixed-grid primary
or secondary can scale up or down freely
while preserving the same aspect ratio. The primary's `ResizeObserver` watches the
**outer** box; producer geometry does not cause secondary resize echoes.
CSS fitting is independent of the configured raster scale and can shrink much
further than 0.5 for thumbnails. The framebuffer follows the fitted surface's
physical pixel size, capped by the requested scale and GPU texture-dimension
limit. Hidden surfaces use a 1×1 backing buffer. Oversized grids reduce canvas
resolution with a visible warning rather than disconnecting solely because
`grid*DPR` exceeds that canvas limit; the logical grid never changes.

This avoids a full-resolution framebuffer for a tiny thumbnail, but still
builds full-grid CPU geometry/quads and retains per-view models and caches.
Glyph rasterization/atlas scale stays fixed at its mount-time value; container
resize does not rebuild it. Local stats distinguish `rasterScale` (requested),
`backingScale` (actual, possibly below 0.5), and `backingWidth`/`backingHeight`.
Fitted viewport dimensions travel only from the main thread to its worker,
never to the server. Remount to select a new font family or automatic raster
scale; live family/DPR changes are not implemented.

Dispose with `view.dispose()` or `lifetime.abort()` when the host removes the
view. Terminate the producer separately with `DELETE /api/terminals/{id}`.
The package does not create server terminal instances and is not an
embedded-JavaScript .NET helper. See the
[publishing guide](../../docs/web-terminal-publishing.md) for manual bootstrap,
coordinated versions, preview packages, and trusted publishing.

## Workloads

| Scene | Purpose |
|-------|---------|
| Mixed | Text styles, Unicode, and visible Sixel/KGP graphics |
| Text | Adjustable-rate scrolling output; batch size controls lines per tick |
| Sixel | Repeated raster decoding and changing image resources |
| KGP | Upload once, then move placements without retransmitting pixels |
| Animation | Upload KGP frames, then let the server advance playback without more workload output |
| Shell | Keyboard, paste, resize, and real PTY programs |

Pause stops generated workload output, not the terminal's animation clock.
Rate and batch controls do not apply to the shell. These HTTP controls are shared
by every view of the instance; generated defaults are unpaused, rate 60, batch
100. HMP1 accepts grid requests only from its primary;
the browser never independently reflows text.

## Input boundary

The browser captures text/IME, paste, and named keys with modifiers. The worker
sends semantic JSON commands; Hex1b's `Hwt1PresentationAdapter` decodes them
and uses shared mode-aware input encoding, including bracketed-paste framing,
before calling `Hex1bTerminal.SendInputAsync`. Clicking the canvas retains focus on
the input textarea. The browser never deletes or echoes terminal cells locally.
When a workload requests mouse tracking, the canvas captures left/middle/right
clicks, dragging, hover movement, and vertical/horizontal wheel input. Pointer
coordinates come from the displayed canvas rectangle, not its high-DPI backing
buffer. Drag capture keeps releases attached to the terminal outside its bounds,
and losing window focus releases held buttons. Continuous motion is coalesced to
animation frames, with pending movement flushed before button and wheel events.
Wheel batches are bounded to 32 reports.

The server filters events using its current tracking mode (X10, normal,
button-motion, or any-motion) and encodes legacy, UTF-8, SGR, or urxvt reports.
Legacy coordinates beyond the one-byte limit are discarded rather than wrapped.
When tracking is off, vertical wheel input browses terminal history.
Shift reserves local selection and history scrolling when an application
captures the mouse. Ctrl+wheel remains browser zoom. Highlight tracking,
pixel-coordinate mouse reports, and complete keyboard-protocol coverage remain
out of scope.

The Unix shell advertises `TERM=xterm-256color` and `COLORTERM=truecolor`.
An early Backspace failure was actually missing shell redisplay: the key
deleted the shell's input, but an absent `TERM` prevented its line editor
from clearing the old text. The Unix PTY implementation now passes the
configured environment to `execve` for both login shells and explicit commands,
instead of inheriting the server's environment and dropping overrides.

The JSON command boundary and typed input encoder live in Hex1b. The demo no
longer reads internal terminal mode flags or duplicates their wire encodings.
Input is encoded using the producer's current modes. HMP1 remains the peer and
resize authority; secondary means “not resize primary,” not “read-only.”

## Text scrollback, selection, and copy

Each mounted view has independent scroll position and selection over shared
producer history. A newly attached thumbnail can inspect output produced before
it connected. The browser does not infer history from screen deltas or reflow
terminal text itself. The sample retains up to 1,000 history rows per producer.
This milestone is text-only when browsing history; live Sixel/KGP rendering is
unchanged, and historical graphics are a separate follow-up.

| Gesture | Result |
|---|---|
| Left-drag | Select characters. |
| Double-click, then drag | Select and extend by whole words. |
| Triple-click, then drag | Select and extend by whole logical lines, including soft wraps. |
| Alt/Option-drag | Select a rectangle across physical rows. |
| Shift-click | Extend the existing selection with its anchor and mode preserved. |
| Wheel during a local drag | Scroll and extend the moving endpoint under the pointer; rectangle columns remain fixed. |
| Wheel after release | Scroll without extending or clearing selection. |
| Drag beyond the top/bottom edge | Autoscroll and extend until release or cancellation. |

When an application captures mouse input, hold Shift for local selection or
wheel scrolling, and Shift+Alt/Option for rectangles. A gesture that starts
locally remains local until release, even if Shift is released partway through.
Clicks on historical rows are not forwarded as clicks on the live application.

Use Cmd+C on macOS or Ctrl+Shift+C elsewhere to copy. Ctrl+C still goes to the
application. Selection does not automatically overwrite the clipboard, and
keyboard/button copying keeps the selection. A local right-click instead copies
and clears the selection after success; a second right-click pastes the
clipboard. Hold Shift when the application has captured the mouse. These
defaults are the same in normal and alternate screens and can be replaced by
input bindings. Read-only views copy but never paste into the application.
Ordinary copy joins soft wraps but preserves hard
newlines. Rectangular copy produces one line per physical row and trims trailing
padding without removing interior spaces. Clipboard failures are reported.

Incoming output does not move a scrolled-up viewport. Use **Return to live** to
resume following output. Typing or pasting clears selection and returns to live
before sending input; copying does neither. If retained text is evicted, the
selection expires explicitly instead of silently producing partial or different
clipboard contents. Read-only mounts still support history inspection and copy.

Resize/reflow, reset, history clearing, and alternate-screen switches
conservatively clear generation-based selection and return to live. Preserving
selection through reflow is not implemented yet. Retained rows can keep their
original widths: narrower views crop them visually, while whole-logical-line
and multirow copy use the retained text rather than inventing browser reflow.

## Library and host boundary

The demo uses the public, experimental `Hwt1PresentationAdapter`. Projection,
image resources, HWT1 serialization, acknowledgement/resync handling, and input
encoding are library responsibilities. Current-frame KGP pixels, placement
ordering, and native-size intent remain internal; this sample needs no
`InternalsVisibleTo` access.
The adapter's public accessibility enables hosting the first-party client; it
does not make the wire format a supported contract for independent frontends.

The host owns persistent producer creation, HTTP controls, and termination.
`BrowserSession` owns a view's HMP1 peer and WebSocket delivery lifetime;
closing it does not dispose the shared producer. Pause/rate are no longer
WebSocket commands. HWT1 itself does not depend on ASP.NET or WebSockets.
Create the per-connection shared-source adapter with
`await producerPresentation.CreateBrowserViewAsync(displayName, ct)`, where
`producerPresentation` is the producer's `Hmp1PresentationAdapter`. Dispose that
view adapter on disconnect; do not create another terminal or workload mirror.
The library applies role/geometry changes; do not add a second primary election
or resize the producer in response to a view's local scrolling.
Simplified host glue, from separate send
and receive loops:

```csharp
var frame = await presentation.ReadFrameAsync(ct);
await socket.SendAsync(frame, WebSocketMessageType.Binary, true, ct);
```

```csharp
await presentation.HandleMessageAsync(completeUtf8JsonMessage, ct);
```

Use one view adapter per connection, deliver commands in transport order,
and keep the receiver running while the frame reader waits for acknowledgements.
The default acknowledgement timeout is two minutes; hosts also own transport
timeouts and must cancel both loops when the terminal or connection ends.
See [BrowserSession.cs](BrowserSession.cs) for the complete lifetime/error handling.

## Data path

```text
Workload / PTY -> shared Hex1bTerminal + HMP1 producer
                          |
                          +-> HWT1 viewport / selection A -> worker / view A
                          +-> HWT1 viewport / selection B -> worker / view B
                          ^
                          +---------- HMP1 peer input / resize authority
```

The sample registers browser peers directly with HMP1; it need not
expose an external HMP1 network listener. HMP1 is the sole primary authority,
not a separate browser-side election. Shared producer `Hex1bTerminal` instances
interpret ANSI, Sixel, and KGP. The browser receives
resolved cells, cursor state, raster resources, and image placement geometry.
It does not run a terminal parser.

**Current spike tradeoff:** every view still owns separate projection/resource
state and browser GPU caches, but no longer reparses HMP1 into a duplicate
terminal model. A shared change-feed/projection cache could avoid more work;
thumbnail CSS alone does not.

Native-size KGP placements (both `c` and `r` omitted) retain their sizing intent
in terminal state. Their occupied cell spans are only bounds, not a request to
stretch the image. The projection keeps a 3x3 sprite at 3x3 logical pixels while
its sub-cell offsets change, including when it crosses cell boundaries.
Viewport clipping and scrolling crop native pixels rather than scaling them.

The presentation adapter receives post-application cell-impact notifications
and invalidations, including KGP timer ticks. It coalesces those notifications
into a single dirty signal. A sender captures an atomic snapshot, projects it,
and sends a revision. It does not capture one snapshot per output token.

Synchronized-output markers (DEC mode 2026) defer capture until the complete
application update arrives, even when its bytes span multiple PTY reads.
The single canvas keeps showing its previous frame during that wait; it is not
hidden or swapped. A one-second watchdog releases a missing end marker, and
soft/full reset releases it immediately. Repeated begins do not extend the
deadline. Unmarked output can still produce intermediate snapshots.

At most one revision per view is in flight. The browser acknowledges after the selected GPU backend reports
submitted work complete. While it is busy, the terminal continues to consume workload output;
superseded display states coalesce, but workload commands are not discarded.
Resync and resize establish a complete cell and resource baseline. A resync
waits for the outstanding acknowledgement; a client that discards a delta
must still acknowledge it. Graphics resources are content-addressed and
cached separately from placements.

## HWT1 internal wire format

All integers are little-endian. Each WebSocket binary message contains:

1. `uint32` magic `0x31545748` (`HWT1`).
2. `uint32` UTF-8 metadata byte length, followed by JSON metadata.
3. `uint32` changed-cell count, followed by variable-size cell records.
4. Concatenated new image payloads, in metadata order.

A cell record contains: `uint32` row-major index; foreground, background, and
underline colors as three packed `uint32` RGBA values (R in the low byte);
`uint16` attributes; `uint8` owned cell span; `uint8` underline style;
`uint16` UTF-8 text byte length; text bytes.

Metadata carries revision/base revision, dimensions, cell metrics, default
colors, cursor, mouse tracking mode, required HMP1 `peer` role state, new image
descriptors, retained resource
keys, the complete placement list, warnings, and server counters. Default cell backgrounds are
transparent over the terminal's default background so deeply negative KGP
placements remain visible. Reverse and dim colors are resolved on the server.

The metadata JSON is a pragmatic implementation choice, not a stability promise.
Large cell arrays and pixel payloads are binary, not JSON/base64. The
[internal implementation notes](../../docs/web-terminal-protocol.md) record the
current layouts, input messages, resource lifetime, revisions, and limits
alongside the current text viewport/selection contract and future negotiation
and reconnection proposals.
These details can change as the paired implementation evolves. Supporting
third-party implementations would require a separate future decision.

## Earlier single-view observations

These observations predate the mounted multi-head topology. They do not claim
to validate its lifecycle, authority, fitting, or multi-view performance.
Current focused multi-view regression results are listed above; broad browser
and sustained-throughput qualification remain separate work.

The server-authoritative rendering boundary works: styled text, wide and
combining characters, color emoji, Sixel rasters, and KGP placements render
without a browser terminal parser. The worker shares one glyph atlas between
tinted text masks and full-color glyphs.

KGP movement used one 230,400-byte texture across 40 further frames without
another upload. Animation continued for 40 frames with **zero additional
workload bytes** and reused four textures after warm-up. The Sixel scene also
reused its four repeating raster resources. Resize, full resync, 2x backing
scale at 200x60, Unicode/paste/Tab input, and a shell interrupted with Ctrl+C
all worked. Withholding an acknowledgement for 300 ms kept exactly one state
frame in flight while the terminal continued processing output.

One five-second Release text-stress sample, after warm-up, used a 120x40 grid,
1x backing scale, requested rate 120, and batch size 1000. Environment: macOS
arm64, Chromium 149.0.7827.3 headless with `--enable-unsafe-webgpu`, Apple
`metal-3` adapter. These are exploratory observations, not a benchmark result
or a comparison against xterm.js/WebGL.

| Measurement | Observed |
|-------------|----------|
| Presented state frames | 33/s |
| Workload throughput | 0.52 MB/s |
| Wire throughput | 3.24 MB/s |
| Wire/workload amplification | 6.2x |
| Sampled renderer CPU median / p95 | 0.6 / 0.7 ms |
| Sampled snapshot + projection median / p95 | 11.5 / 98.1 ms |

Snapshot/projection time includes snapshot lock waiting and copying, but stops
before final metadata/cell serialization. Renderer CPU time excludes GPU
execution and the browser's complete decode/presentation path. The CPU
percentiles are from 163 sampled revisions, not GPU timestamp queries.

The result supports pursuing this architecture, but **does not yet establish
high-throughput readiness**. Scrolling re-encodes many cells, amplifying wire
traffic, and server-side snapshot/projection latency rises under stress.
Those are the first areas to investigate before drawing conclusions about
the fastest rendering backend or designing the production protocol.

The earlier mirror topology counted HMP1 ingress, including replay, separately
for each mirror. Shared-source views instead observe the producer's workload
counter; summing those repeated values across views is not aggregate throughput.
Role-only updates and producer resizes without new workload output must still
produce frames.

## Limits and interpretation

- Auto prefers WebGPU and falls back to WebGL2 for capability/device acquisition
  failures. Explicit backend choices never fall back. Shader, font, unexpected
  initialization, and runtime GPU/worker errors are surfaced, not hidden by a
  backend switch. There is no Canvas2D terminal renderer.
- Fixed 10x20 logical-pixel cells. Device pixel ratio affects browser
  rasterization, not terminal protocol geometry. Local resize/claim requests use
  20..300 columns and 10..100 rows; a native HMP1 primary can establish a larger
  authoritative grid. Projection rejects grids beyond 1024 columns, 512 rows,
  or 262,144 cells without clamping the producer/mirror. Within that envelope,
  GPU canvas limits reduce framebuffer resolution rather than grid dimensions.
- The server diffs complete snapshots. Snapshot copying, per-token impact
  allocation and per-view projection remain potential
  throughput bottlenecks.
- Full viewport rendering is distinct from incremental transport. A small
  cell delta does not imply an equally small GPU update.
- Sixel crop/damage is materialized on the server. KGP animation textures are
  cached as their server-selected frames become visible.
- Maximum image dimensions are 4096 per axis and 32 MiB decoded per image.
  The resource cache is bounded to 64 MiB decoded and 4096 images, matching the
  browser protocol's resource-count limit. The count accommodates workloads
  such as SixelCloudDemo's 700 small placements without relaxing the byte budget.
  Unique active images, including Sixel crop/damage variants, are preflighted
  before dense allocation; inactive cache entries make room before replacements
  are allocated. Producer graphics accounting remains independent.
  Inactive resources are evicted first; exceeding visible-resource limits ends
  the session explicitly.
- Historical graphics, hyperlink activation, a complete
  screen-reader experience, ligature shaping, and a cross-browser font/shaping
  guarantee remain future work. Glyphs are clipped to server-owned spans; decoration and
  cursor appearance still need a broader fidelity corpus.
- Existing terminal-model behavior is authoritative, including its bugs. For
  example, this revision's tokenizer does not recognize the standard spaced
  DECSCUSR sequence (`CSI 6 SP q`) as a cursor-shape token.
- Renderer CPU time is not GPU execution time. Workload throughput, wire
  throughput, projection time, and frame rate measure different stages;
  none alone establishes end-to-end performance.

The mounted class lives in `@hex1b/web-terminal`; the playground is a separate
TypeScript consumer rather than a second copy of the renderer. No
embedded-JavaScript .NET helper is introduced. The experimental server adapter
lives in Hex1b and the sample is an ordinary public-API
consumer. Other spike changes fix Unix PTY environment propagation and preserve
KGP native-size intent through snapshots and rendering/replay paths.
Native interop is rebuilt when its source changes; the original native entry
points remain available for existing callers.
