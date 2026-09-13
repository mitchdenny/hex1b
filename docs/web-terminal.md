# Web terminal: spike findings and a path to a supported feature

Status: exploratory design, based on the WebTerminalDemo spike as of 2026-09-07.
The implementation demonstrates viability; it is not yet a supported browser
terminal, a stable protocol, or a drop-in implementation of the xterm.js API.
The server-side pipeline now lives in Hex1b behind an experimental
`Hwt1PresentationAdapter`. Remaining production recommendations below are
proposals, not settled public APIs.

**HWT1 is internal state transfer between the first-party server and client,
not a protocol for third-party implementation.** Both ends evolve together,
without wire-compatibility or deprecation guarantees. The protocol notes are a
contributor reference, not an invitation to build independent frontends.
Recommending third-party implementations would be a separate future decision.

## Summary

We can render `Hex1bTerminal` state directly in a browser without running a
second terminal emulator there. The spike displays text, Unicode, Sixel and
Kitty Graphics Protocol (KGP) images, including server-driven animation, and
supports an interactive shell with keyboard, paste, and mouse input.

The mounted multi-head proof-of-concept adds floating, independently sized views
of persistent shared terminal instances. HMP1 supplies the single primary/resize
authority. Browser views now read shared producer state and retained text
history, with independent HWT1 delivery, viewports, selection, and GPU resources.
The earlier per-view terminal mirrors are no longer used by the web demo.

The most useful result is not just that WebGPU can draw a terminal. Having our
own renderer has exposed information missing from the terminal model and
integration bugs that another terminal emulator could mask. Native image size,
cell occupancy, input modes, and PTY environment all matter to fidelity.

The architecture is worth pursuing, but high-throughput readiness is not yet
established. Snapshot/projection work and scrolling-related wire amplification
are more immediate concerns than replacing the GPU backend. Text scrollback,
selection, and copy now have an explicit interaction model; historical graphics,
reflow-preserved selections, and production recovery remain separate work.

The recommended direction is a server-authoritative presentation contract,
a small embeddable browser component, and host-owned session management.
Keep WebTerminalDemo as the playground and conformance harness while these
boundaries become stable.

## Goals and boundaries

The goal is a first-party rendering layer for projects that already use
`Hex1bTerminal` as their terminal toolkit, potentially replacing their use of
xterm.js. It should work with `Hex1bApp`, PTY workloads, and other workloads
whose output is interpreted by `Hex1bTerminal`.

There must be one authority for ANSI interpretation, terminal modes, cursor
position, wrapping, scrolling, graphics placement, and animation state:
the shared producer `Hex1bTerminal`. HMP1 supplies peer and resize authority
without requiring a second terminal model for every web view.
The browser must not replay ANSI into an independent model,
invent missing history, or independently reflow terminal content.

The browser still owns presentation and interaction state: GPU resources,
glyph rasterization, device-pixel-ratio handling, focus, pointer capture, and
viewport/selection gestures. This milestone resolves selection ranges and text
on the server; a future bounded local prefetch/feedback layer need not duplicate
ANSI interpretation or server reflow.

Compatibility has two distinct targets: rendering the server's state faithfully,
and making that state conform to terminal protocols. A faithful renderer can
faithfully reveal a server bug. Such bugs should be fixed at the authoritative
layer rather than hidden by browser-specific compensation.

## What the spike implements

See [WebTerminalDemo](../samples/WebTerminalDemo/README.md) for run instructions.
It uses an ASP.NET Core host and the TypeScript
[`@hex1b/web-terminal` package](../src/web-terminal/README.md), compiled to
browser ES modules. The playground is a separate TypeScript package consumer.
There is no WASM component or embedded-JavaScript .NET helper.
The demo consumes the library's public presentation adapter without friend access.

### Ownership and data flow

| Component | Responsibility |
|-----------|----------------|
| Workload / PTY | Produces terminal output and consumes input bytes. |
| Shared producer `Hex1bTerminal` / HMP1 | Interprets workload output and owns terminal state, dimensions, and HMP1 primary identity. Persists independently of browser views. |
| Per-view HMP1 registration | Gives the browser view a peer identity and routes input/primary requests through the existing authority model, without replaying output into a mirror. |
| Per-view `Hwt1PresentationAdapter` (Hex1b) | Coalesces producer invalidations, captures the requested viewport, resolves selection/copy, and owns frame delivery, acknowledgements, resync, peer metadata, and mode-aware input. |
| `Hwt1RenderProjection` (internal to Hex1b) | Projects captured snapshots, computes cell differences, manages image resources, and serializes HWT1 frames. |
| Sample host / `BrowserSession` | Manages shared instances through HTTP; each WebSocket session owns only its HMP1 peer/view adapter and browser delivery loops. |
| Mounted `WebTerminal` | Owns its appended shadow-root wrapper, hidden input textarea, coordinate conversion, local fitting, and pointer capture; does not own the caller's container. |
| Per-view dedicated worker | Owns one WebSocket, decoder, transferred `OffscreenCanvas`, and independent WebGPU or WebGL2/glyph/image caches. |

```text
workload / PTY -> shared Hex1bTerminal + HMP1 producer
                       |                 |
                  HMP1 peer         HMP1 peer
                 HWT1 viewport A   HWT1 viewport B
                 selection A       selection B
                       |                 |
                 WebSocket/view A  WebSocket/view B
                 worker/GPU cache  worker/GPU cache
```

The sample registers browser views directly with the HMP1 presentation adapter
and reuses its native peer/primary model. This does not imply an external HMP1
network endpoint or a competing HWT1 primary election.

Shared-source views remove the earlier duplicate terminal models, HMP1
parsing/replay, and animation clocks. HWT1 snapshot/projection/cache and browser
GPU resources still exist **per view**. Thumbnails reduce displayed size, not
all of those fixed responsibilities. A shared change-feed/projection cache
remains a possible optimization; this milestone does not establish multi-view
throughput.

### Shell reflow configuration

Reflow belongs to the authoritative producer, not the browser. The HMP1 and
directly attached HWT1 presentation adapters retain their **crop-and-extend
default** for compatibility. Enable normal shell reflow before constructing the
terminal (configuration snippet):

```csharp
using Hex1b;
using Hex1b.Reflow;

var presentation = new Hmp1PresentationAdapter(80, 24)
    .WithReflow(GhosttyReflowStrategy.Instance);
// Assign presentation to Hex1bTerminalOptions.PresentationAdapter, or use
// WithPresentation(presentation) on Hex1bTerminalBuilder.
// Configure ScrollbackCapacity / WithScrollback(...) on the terminal as well.
```

`Hex1bTerminalBuilder.WithReflow(strategy)` also configures HMP1 and directly
attached HWT1 adapters. Prefer an explicit strategy for a remote producer;
parameterless builder `WithReflow()` auto-detects the *server's* environment,
which need not describe the browser terminal. `NoReflowStrategy.Instance`
selects crop behavior explicitly.
Crop paths erase wide glyphs split at the right edge, preserving valid cell
geometry for later screen replay instead of retaining an orphaned half glyph.

For a directly attached `Hwt1PresentationAdapter`, the same
`.WithReflow(GhosttyReflowStrategy.Instance)` opt-in applies. For views returned
by `CreateBrowserViewAsync`, configure the **HMP1 producer only**; a view's
reflow setting cannot override its producer. No browser configuration, transport
replacement, or second ANSI model is required.

The demo's optional HMP1 relay builds a terminal replica rather than a direct
browser view. It explicitly gives that replica the producer's reflow provider,
preserving the strategy and internal graphics-anchor mapping. HMP1 screen replay
preserves soft line breaks, but the protocol does not negotiate a reflow strategy:
hosts of other replicas must configure matching policies themselves.

Ghostty reflow preserves hard line breaks while rewrapping soft continuations,
including retained scrollback, styled and wide/combining text, and cursor and
saved-cursor insertion positions. Retention remains bounded by the configured
scrollback capacity **in physical rows**: narrowing can evict the oldest rows
when that limit is exceeded. Previously cropped or evicted text cannot be
recovered by enabling reflow later. Alternate-screen layouts still crop on
resize; the saved main screen and history reflow to the current dimensions when
the application returns to the main screen.

The browser's minimum requested width is 20 columns. Native HMP1 peers can
request narrower grids; a glyph wider than the entire grid is dropped, matching
the terminal's normal printing behavior at that width.

Primary-peer resize authority, graphics ownership/anchor remapping, and HWT1
frame refresh are unchanged. Resize/reflow invalidates existing selections
rather than trying to preserve stale coordinates.

WebTerminalDemo's **New terminal reflow** selector defaults to this policy for
its **shell** scene and cropping for generated text/graphics scenes. Select a
different built-in strategy before creating a terminal to compare behaviors;
the choice is fixed for that producer and shared by every attached view.
The sample reports the policy in its existing-terminal list and HTTP metadata.
See [the demo's strategy selector](../samples/WebTerminalDemo/README.md#choose-a-reflow-strategy)
for API and query-string options. Library adapter defaults are unchanged.
Existing consumers must opt in after upgrading to a release containing this
API; it is not available in 0.166.0. Upgrade `Hex1b` and
`@hex1b/web-terminal` together to matching released versions.

Multi-head testing also exposed a replay gap: a late viewer could repaint a
KGP animation's current image but then remained frozen once the producer stopped
writing bytes. HMP1 now replays all composed animation frames and playback
controls, followed by an ordered, one-time
[`KgpAnimationState` checkpoint](muxer-protocol.md#kgpanimationstate-0x0c)
for captured loop progress and frame age. This does not add per-tick pixel
retransmission. Native HMP1 mirrors still advance restored clocks; exact
wall-clock synchronization across those peers is not established. Shared-source
web views read the producer's animation state instead of advancing mirror clocks.

The presentation adapter's capacity-one dirty channel coalesces notifications.
The sender captures an atomic snapshot after a notification and after the
previous frame has been acknowledged. It does not snapshot every output token.
However, it still copies and scans the whole visible screen for each projected
revision; the notification channel is not a terminal change journal.

Post-application notifications are important. The raw presentation write path
can run before output has been applied to terminal state. The spike uses
`ICellImpactAwarePresentationAdapter` for the state-applied boundary and
`InvalidatePresentation()` for changes such as animation ticks without new bytes.

The terminal tracks synchronized output (DEC mode 2026). HWT1 defers capture
between begin/end markers, even across split PTY reads, so the browser keeps its
previous completed frame instead of displaying an erase or partial repaint.
Capture checks the mode under the same lock used to copy terminal state.
A one-second watchdog releases an unterminated update; repeated begin markers
do not extend it. Reset also releases the wait, and input/output processing
continues throughout. Unmarked updates have no application-frame guarantee.

Embedded `TerminalWidget` views honor the same boundary without blocking the
parent app. Each node retains a coherent text, Sixel, KGP, and cursor frame and repaints
it at the current bounds while the child update is open. The end marker requests
a redraw even when it arrives alone. This prevents nested applications such as
WindowingDemo's Bash terminal running KittySearch from exposing an intermediate
erase inside an otherwise complete outer frame.

The embedded view advertises Sixel decoding when its parent can present Sixel,
and inherits the parent's Sixel protocol metrics separately from text-cell
metrics. Overlapping child placements are composited from their decoded pixels
into a bounded viewport image before Surface rendering. This preserves palette
history, transparency, damage, native pixel size, and scrollback crops without
materializing large off-screen source rasters. Lossless register reuse also
preserves composites with more than 256 colors; KGP support is not required.

### Rendering surface

We considered SVG and canvas and chose a worker-owned WebGPU canvas for the
spike. Canvas is the hosting surface; the main rendering path is not Canvas 2D.
The renderer uses instanced quads, a glyph atlas, and persistent image textures.
Canvas 2D is used to rasterize reusable glyphs into the atlas.

The renderer now supports both WebGPU and WebGL2 behind a shared frame-preparation
layer. Mount-time `renderer: "auto"` prefers WebGPU and falls back to WebGL2 for
capability/device acquisition failures; `"webgpu"` and `"webgl2"` force a backend.
WebGPU requires a secure context; WebGL2 permits ordinary HTTP rendering.
Font/shader errors and runtime device/context loss remain errors, not fallback
triggers. Stats expose the active renderer and any automatic fallback reason.

This gives us explicit positioning and compositing without a DOM element for
each cell or image. Text masks and color emoji share an atlas. A font-wide
advance and line-box transform maps each style to the logical cell dimensions;
individual graphemes are clipped to the server-owned spans rather than
stretched independently.
Images are drawn with resolved source, destination, clip, and stacking data.

The default font is now the unmodified Cascadia Mono NF variable WOFF2, with
Nerd Font symbols, distributed alongside its OFL 1.1 license and
provenance in the package's `dist/fonts/cascadia-mono-nf/` directory.
Each view accepts a developer-selected font and optional downloadable faces.
The worker awaits its own font loading before opening the WebSocket or creating
glyphs: document fonts are not inherited by an OffscreenCanvas worker, and
font-load failures must not populate the atlas with unnoticed fallback glyphs.

The original fixed 16px font and baseline did not match the 10x20 cell box,
leaving visible gaps between font-rendered window borders. The renderer now
measures advance, ascent, and descent once per font style. Borders, blocks,
and icons remain font glyphs, not procedural replacements. The browser fixture
covers regular/bold window-border continuity and Nerd symbols at raster scales
1, 1.25, 1.5, 2, and 3. Other fonts still control their own outlines and coverage.
Font family/source choice is mount-time configuration; changing it requires a
remount and fresh glyph cache. Display size can change live through the Auto
sizing policy without replacing the font or atlas.

Sixel interpretation and crop/damage materialization happen on the server.
For KGP, the server chooses the current animation frame and placement state.
The browser uploads RGBA resources or decodes ordinary PNG resources; it does
not implement either terminal graphics protocol.

The grid currently uses fixed 10x20 logical-pixel cells. Device pixel ratio
changes rasterization resolution, not the terminal's coordinate system.
Each inner surface retains the authoritative `columns*10` by `rows*20` aspect
ratio and contain-fits the caller's sized outer element. A secondary view only
scales when its container changes. In Auto mode, the primary observes its outer
container and derives whole-cell dimensions from the requested text size.
The footer's -/+ controls select nominal sizes 8..32, with 16 retaining the
original 10x20 CSS-pixel cells. Display scale caps at `fontSize / 16`, rather
than enlarging text to consume leftover pixels; grid bounds can require a
smaller fit. The server still decides when the grid changes.

The resolution selector switches to a fixed grid such as 80x24 or 120x40.
That primary, like every secondary, contain-fits the authoritative grid when
its window changes size instead of issuing a new producer resize. Returning
to Auto restores the previous text-size preference. Sizing controls are
disabled on secondaries; a view retains its policy for an explicit future
primary claim. This adds no new HWT1 command or separate authority mechanism.
Local requests remain bounded to 20..300 columns and 10..100 rows, independently
of a larger grid established by a native HMP1 primary.
The glyph atlas starts at up to 2048x2048 and can grow to 4096x4096.
The renderer rebuilds viewport draw geometry each frame, even when the wire
update contains only a few changed cells.

The framebuffer now follows the fitted surface's physical pixel size, bounded
by requested scale and the GPU's texture-dimension limit; a hidden surface uses
1×1 backing. A canvas-limit warning accompanies resolution reduction instead
of changing the logical grid or failing solely because grid×DPR is too large.
Thumbnail overdraw is reduced, but full-grid CPU geometry/quads and per-view
models/caches remain. Glyph/atlas raster scale stays fixed at mount and is not
rebuilt on container resize. Local metrics separate `rasterScale`,
`backingScale` (which can be below 0.5), and backing dimensions.
The fitted viewport notification is main-thread-to-worker only, not HWT1.

WebGPU is a promising implementation choice, not a measured winner over
WebGL2, Canvas 2D, or xterm.js. SVG remains useful for exports and debugging.
WASM is also not an alternative GPU surface: it would only help particular
CPU-side tasks demonstrated by profiling. A browser-side ANSI parser in WASM
would work against the central design.

### Transport and backpressure

The internal [Hex1b Web Terminal Protocol (HWT1)](web-terminal-protocol.md)
currently contains a binary header, JSON
metadata, binary changed-cell records, and new image payloads. Metadata includes
the revision/base revision, grid and cell metrics, cursor, mouse tracking mode,
HMP1 peer/primary identity,
resource descriptors, retained resource keys, complete placement list, and
diagnostic counters. Input and control messages are semantic JSON commands.
HWT1 describes presentation state, not WebGPU commands. Shared workload controls
use host HTTP endpoints, not HWT1 pause/rate messages. The demo binds one
binary frame to each WebSocket message; the core adapter has no transport dependency.

Resources and placements have separate lifetimes. Moving an existing KGP image
does not require retransmitting its pixels. Current-frame image data is cached
by content, and unchanged byte-array identities avoid repeated hashing where
possible. The placement list itself is still sent in full.

There is at most one state revision in flight per view. The browser acknowledges after
the selected backend reports submitted work complete (WebGPU queue completion
or a WebGL2 fence). The terminal continues consuming output
while presentation is busy: intermediate visual states can be skipped, but
workload bytes are not intentionally dropped.

Full resyncs clear the projection's resource cache and provide a self-contained
baseline. A resync request does not release an outstanding acknowledgement.
A client discarding a mismatched delta still acknowledges it before requesting
resync, preventing a deadlock. This corrected an earlier spike behavior that
allowed resync to bypass the one-frame limit.

This is deliberately conservative. One frame in flight couples presentation
rate to acknowledgement latency, including network delay and GPU completion.
We have not established how it behaves over a realistic remote connection.

### Input

The browser sends text/IME commits, paste, named keys with modifiers, and mouse
intent. It never echoes or deletes terminal cells locally. Server-side code
handles application cursor keys, bracketed paste, and mouse protocol encoding.

Mouse support now includes left/middle/right buttons, dragging, hover movement,
and vertical/horizontal wheel reports. Tracking is gated by server modes:
X10, normal, button-motion, or any-motion. Encodings include legacy, UTF-8, SGR,
and urxvt. Legacy coordinates beyond their byte limit are dropped, not wrapped.
Highlight tracking and pixel-coordinate reporting are not implemented.

Pointer coordinates use the displayed canvas rectangle, not the backing-buffer
dimensions. Capture preserves drag releases outside the canvas, and losing
window focus releases held buttons. Continuous movement is coalesced to animation
frames, with pending motion flushed before button and wheel reports to preserve
ordering. Wheel batches are capped at 32 reports. Browser-reserved
shortcuts and Ctrl+wheel zoom remain browser-owned.

HWT1 command decoding and mode-aware encoding now live in Hex1b, not the sample.
The protocol-specific JSON boundary delegates to a shared, typed terminal input
encoder. This keeps terminal protocol decisions on the server and reusable by
the terminal's event-input path; it does not claim complete keyboard-protocol
coverage or consolidation of every older diagnostics/input utility.

### Mounted views, primary authority, and lifetime

[`WebTerminal.mount(container, options)`](../src/web-terminal/src/web-terminal.ts)
is the package's framework-neutral mounting API, not an injected-JavaScript .NET helper.
It resolves after a presented frame with a connected peer (or standalone primary)
and owns only the wrapper it appends. The host must size the outer element. Mounts have independent workers,
input, resource caches, callbacks, cancellation, and disposal, without global DOM
IDs or a dependency on the floating-window playground. See the
[sample mounting example](../samples/WebTerminalDemo/README.md#mount-in-a-sized-element)
for the current options and methods.

The playground creates draggable/resizable windows with New terminal, Attach
view, Thumbnail, Take primary, Resync, Close view, and End terminal actions.
A newly created instance's first view explicitly claims primary. Attaching an
existing instance starts secondary, even if no primary is assigned. HMP1 alone
confirms claims; `requestPrimary()` is a request, not an immediate local role
change. Primary status controls resizing, **not input ownership**: all peers
may send input. `readOnly` disables local browser input only and is not server
authorization.

Primary container changes request producer resizes. Secondary container changes
only change local CSS fit and never echo producer geometry back as a resize.
Role-only transitions and authoritative resizes without workload output still
invalidate presentation. Closing the primary preserves the last grid and leaves
the role unassigned; another view must explicitly take primary.

Closing/disposal detaches a view, not its producer. The producer remains alive
with zero views until explicit HTTP termination, workload exit, or server
shutdown. End terminal deletes the shared instance and affects every attached
view. The sample bounds this experiment to four producers and eight web views.
This is simple process-local retention, not durable recovery or an automatic
reconnect contract.

### Extracted library boundary

`Hwt1PresentationAdapter` implements the existing presentation and lifecycle
interfaces. A host attaches it to a terminal, sends the complete binary payloads
from `ReadFrameAsync`, and delivers complete UTF-8 JSON commands through
`HandleMessageAsync` in transport order. The frame reader and message receiver
must run independently so acknowledgements can release backpressure.

There is one view adapter per connection and one frame reader. Cancellation
does not acknowledge a frame; disposal releases pending waits. The next read
waits up to the configurable `AcknowledgementTimeout` (two minutes by default)
for the preceding acknowledgement. Hosts still need transport-write timeouts,
terminal/connection teardown, and any authentication or session policy.

The extraction includes projection, current-animation resource access, native
placement sizing, resource caching, serialization, and input encoding. Moving
only the notification adapter would have left the demo dependent on internal
KGP and terminal-mode members. Those members remain internal; the demo's
`InternalsVisibleTo` entry and the tests' linked sample implementation files have
been removed. Wire metadata uses source-generated JSON serialization so moving
the encoder into the AOT-compatible library does not introduce reflection requirements.

The sample now exercises mounting and shared-instance attachment, but this is
not finished browser packaging, durable reattachment, multi-viewer performance
qualification, or a stable production API.
The public adapter enables hosts to connect the matching first-party client;
it does not make HWT1 a supported third-party wire contract.

## What real workloads taught us

### Input problems can be below, or above, the terminal encoder

The first shell focus problem was a browser event-ordering bug. A pointer
handler focused the hidden textarea, then the pointer's default action blurred
it again. Preventing that default action fixed actual clicking and typing.
Programmatically focusing the textarea had hidden the problem in early checks.

Backspace was a different problem: DEL was reaching the shell and modifying
its command buffer, but the shell did not visibly erase the character. Its
`TERM` environment was missing because Unix PTY launch ignored the supplied
environment. Passing a per-child environment to native `execve`, including
`TERM=xterm-256color` and `COLORTERM=truecolor` for the web shell, fixed redisplay.
The change also removed process-wide environment mutation during launch and
preserved the old native entry points.

The lesson is to distinguish browser focus, input encoding, PTY delivery,
application state, terminal state, and final pixels. A visual symptom does not
identify the failing layer.

### Native image dimensions cannot be reconstructed from occupied cells

KgpCloudDemo's 3x3 sprites appeared as irregular blobs. The model substituted
cell dimensions for omitted sizing, and projection stretched the sprite into
that area. Subtracting the sub-cell offsets made its apparent size change as
it moved.

We now retain native-size intent when both `c` and `r` are omitted. Cell spans
describe occupancy, while the image remains 3x3 logical pixels. Clipping and
scrolling crop native pixels instead of rescaling them. This distinction is
preserved through snapshots, relative placements, replay, SVG export, embedded
terminals, occlusion, and placement identity/change detection.

Similarly, Sixel raster height must not be rounded up into a whole-cell display
height: a six-pixel raster is not automatically a twenty-pixel image.
Creation-time cell metrics matter when projecting that raster.

These fixes support a production contract exposing resolved geometry, rather
than asking each frontend to infer display intent from occupancy fields.
They do not establish complete graphics-protocol conformance.

### Resource counts and resource bytes are different constraints

SixelCloudDemo disconnected at the original 128-image cache limit. Its default
700 tiny motes exhausted the count budget, not the memory budget.
Raising the count to 4096 while retaining the 64 MiB decoded-image budget
allowed the workload to run. Inactive resources are evicted before active ones.

Current projection limits are 4096 pixels per image axis, 32 MiB decoded per
image, and 4096 cached images / 64 MiB decoded image data. These are spike
guardrails, not production sizing guidance. Glyph textures, driver overhead,
server history, and other allocations are separate costs.
The complete unique active-image set is checked before allocating dense
projection pixels, including per-placement Sixel damage/crop variants. Inactive
cache entries are evicted before replacements are allocated. This per-view
budget is separate from the producer's shared Sixel/KGP retained-memory budget.

Many tiny Sixels and a few large KGP images exercise very different paths.
Increasing a limit prevents one failure; it does not prove that thousands of
individual GPU textures are the right long-term representation.

### Smoothness reports require frame-level evidence

GlobeDemo was reported as jerky, then recovered without a rendering change.
An isolated run showed bursts separated by about 1.55-1.61 seconds and inexpensive
snapshot/projection and renderer CPU samples. The sample requests
`.RedrawAfter(2000)`; other invalidations can render sooner, so this is not a
universal two-second frame-rate limit.

We did not establish the cause of the user's transient behavior and did not
apply a performance fix. Application redraw cadence, server processing,
transport, and GPU/browser presentation must be measured separately.

### High DPI is part of correctness

An early preview forced a 1x backing scale on a Retina display, producing fuzzy
text. Automatic device-pixel-ratio scaling corrected it. Input must use the
logical displayed grid at the same time: multiplying mouse coordinates by DPR
would target the wrong cells. Real DPR2 clicking and dragging now exercise both
sides of this contract.

## Performance evidence so far

These are exploratory observations from the earlier single-view/direct-workload
spike, not validation of the mounted multi-head changes, release benchmarks,
or a backend comparison. The main browser measurements used Release builds on macOS arm64,
headless Chromium 149.0.7827.3 with `--enable-unsafe-webgpu`, and an Apple
`metal-3` adapter. Durations, scales, and workloads differ between observations.

| Workload / condition | Observation | What it supports |
|----------------------|-------------|------------------|
| Text stress, 120x40, 1x, requested rate 120 and batch 1000, five seconds after warm-up | About 33 state frames/s; 0.52 MB/s workload and 3.24 MB/s wire, about 6.2x amplification | Per-cell scrolling deltas and server-side work deserve investigation before GPU replacement. |
| Same text run, 163 sampled revisions | Snapshot/projection median 11.5 ms, p95 98.1 ms; renderer CPU median 0.6 ms, p95 0.7 ms | Server capture/projection is a visible cost in this workload; GPU execution was not measured. |
| KGP placement movement | One 230,400-byte texture reused across 40 further frames without upload | Resource/placement separation works. |
| KGP animation | 40 further frames with zero workload bytes, reusing four textures after warm-up | Presentation invalidation must not depend solely on workload output. |
| SixelCloudDemo after the count-limit fix | 291 state frames in six seconds; peak 2724 cached resources / about 2.6 MB decoded | The tiny-image workload can stay connected within the byte budget. |
| KgpCloudDemo after native-size correction, 120x40 at DPR2 | 147 state frames observed; 12 cached 3x3 textures totaling 432 bytes; sampled rate about 51/s | Correct small sprites can move while reusing a tiny image palette. |
| Acknowledgement withheld for 300 ms | One state frame remained outstanding while workload processing advanced | Presentation backpressure does not require pausing the terminal's output parser. |

Snapshot/projection time includes snapshot lock waiting and copying, but ends
before final metadata/cell serialization. Renderer CPU time excludes GPU
execution and is not the entire browser decode/preparation/presentation cost.
State-frame counts include partial, cursor-only, and other updates; they are
not counts of complete application animation frames.

In the earlier mirror topology, HWT1 `workloadBytes` measured HMP1 ingress
including replay, not original producer bytes. Shared-source views now repeat
the producer's workload counter; summing them still overcounts throughput.
Per-view projection and GPU/cache costs need separate measurements. Do not
reuse the historical amplification figures as multi-head measurements.
The historical observations above do not validate the
mounted topology; focused regression results below are distinct from actual
multi-view WebGPU and performance qualification.

Some historical cloud captures appeared to show partial scenes. The demos bracket
frames with synchronized-output sequences; HWT1 and embedded `TerminalWidget`
views now honor those boundaries. The real-browser cloud and nested KittySearch
fixtures cover blank/partial redraws, but do not establish that every possible
visual discontinuity has been eliminated.

## Scrollback needs a first-class design

### Current behavior

The web client can browse retained producer text through an independently
anchored viewport. Wheel input navigates history when the application is not
capturing it; Shift reserves local scrolling during capture. A late attachment
sees existing producer history, rather than only output after the view joined.

The browser cannot safely build history by observing screen deltas. Frames are
coalesced, so many lines can enter and leave the live screen between presented
revisions. History must come from the terminal that consumed all the output.

Existing [snapshot/history APIs](../src/Hex1b/Hex1bTerminal.cs) and
[TerminalWidgetHandle](../src/Hex1b/TerminalWidgetHandle.cs) provide useful
starting points. The web view resolves text ranges on the server; a general
paged range-prefetch/cache API is not part of this milestone.
In particular, [ScrollbackWidth.CurrentTerminal](../src/Hex1b/Automation/ScrollbackWidth.cs)
means truncate/pad to the current width; it should not be mistaken for reflow.

### Proposed ownership and behavior

The first milestone is **text scrollback, selection, and plain-text copy**.
Historical Sixel/KGP rendering follows separately; live graphics must keep
working. Web views read the shared producer rather than building separate
terminal histories from HMP1 replay. HMP1 remains responsible for peer identity,
input, and resize authority.

### Interaction contract

Selection and scroll position belong to a view, not to the producer or its
primary peer. Read-only views can inspect and copy. Local inspection does not
request primary, resize the producer, or move another view.

| Gesture | Behavior |
|---------|----------|
| Left-drag | Character selection. |
| Double-click and drag | Select and extend by whole words. |
| Triple-click and drag | Select and extend by whole logical lines, including soft-wrapped rows. |
| Alt/Option-drag | Rectangular selection across physical display rows. |
| Shift-click | Extend the existing selection, preserving its anchor and mode. |
| Wheel during an active local drag | Scroll first, then extend the moving endpoint under the pointer. Rectangles retain their column boundaries. |
| Wheel after releasing the button | Move the viewport without changing the selected buffer range, even when it goes offscreen. |
| Drag beyond the top or bottom edge | Autoscroll and extend using the same rule as wheel-during-drag. |
| Local right-click with selection | Copy and clear the same selection after successful clipboard writing. |
| Local right-click without selection | Paste the system clipboard; read-only views do nothing. |

When an application captures the mouse, unmodified input normally belongs to
the application. Shift reserves local selection and scrolling;
Shift+Alt/Option selects a rectangle. Gesture ownership is latched at
pointer-down until release or cancellation: releasing Shift mid-drag must not
turn a local selection into application mouse events.

Copy uses Cmd+C on macOS and Ctrl+Shift+C elsewhere; Ctrl+C remains application
input. Keyboard/button copy keeps the selection and does not jump to live output.
Right-click follows Windows Terminal's copy-then-paste interaction: successful
copy clears selection, so the next right-click pastes. Shift+right-click
overrides application mouse capture; main and alternate screens use the same
default. A failed copy preserves selection, and an older asynchronous copy
must not clear a newer selection. Selecting
does not automatically overwrite the clipboard. Ordinary text extraction joins
soft wraps but preserves hard newlines. Rectangles produce one clipboard line
per physical display row, preserve interior spaces, and trim trailing padding.
Clipboard permission failures are surfaced, not treated as successful copies.
Mouse paste requires browser clipboard-read access; keyboard paste continues
using native paste events. Input/selection/focus/buffer changes cancel a pending clipboard
read rather than delivering its text into a changed input context.

New output does not move a historical viewport or its selection. A visible
Return to live action restores follow-tail. Typing or pasting clears selection
and returns to live before sending input. If selected history is evicted, the
selection is invalidated explicitly rather than copying different or partial
text. Browser cache eviction is distinct from producer history eviction.

The active-drag wheel rule deliberately follows Windows Terminal/kitty rather
than claiming universal terminal behavior. Other implementations can scroll
without updating a stationary pointer's selection endpoint, and selection
override modifiers do not always override application wheel reporting.

### Browser input bindings and actions

Defaults are replaceable per view through `inputBindings`; `onInput` provides
synchronous interception ahead of binding lookup, and `actions` registers named
host callbacks. Default bindings have stable IDs and can be inspected, replaced,
or removed. Built-in actions are shared by controls, bindings, and `runAction`.
See the [sample API reference](../samples/WebTerminalDemo/README.md#input-customization)
for the concrete shapes and examples.

Decisions distinguish continuing lookup, consuming input, forwarding to the
application, leaving browser handling alone, and invoking an action. Action
execution can be asynchronous; event ownership cannot. Gesture ownership stays
latched through release/cancel, while IME, dead keys, and native paste retain
their browser input lifecycle.

Binding predicates can inspect selection, history, read-only, peer, mouse
capture, and main/alternate-buffer state. The default clipboard policy does
not special-case the alternate screen, but hosts may do so. This layer does not
replicate Hex1bApp's widget hierarchy, focus router, or multi-key sequences.
HWT1 still carries input intent; the server encodes keys, mouse, and bracketed
paste. Clipboard permission failures and the 64 KiB input-message bound are
reported locally. Large/multiline paste confirmation remains future work.

### Replaceable selection presentation

Selection UI is a separate browser-local extension point. The `onSelectionUI`
option handles a cancelable `selectionui` event on the view's element.
Preventing its default replaces the stock Copy control; leaving it uncanceled
allows custom controls alongside it. Highlights, Return to live, and error
feedback do not disappear when Copy is replaced.

Each view supplies a stable light-DOM overlay slotted above its canvas, immutable
state snapshots, visible selection rectangles in overlay-local CSS pixels, an
action dispatcher, and a disposal signal. Hosts can style/portal their own
controls into that layer and invoke the same guarded copy action from a user
gesture. Interactive controls keep their own input and focus; they do not
accidentally select terminal cells or send keystrokes to the workload.

Updates are coalesced and ignore unchanged frame revisions. Host controls
should be created once, then updated as selection, viewport, copy status, or
displayed geometry changes. CSS parts independently expose the highlight spans
and default button for simpler theming. See the
[selection UI API](../samples/WebTerminalDemo/README.md#selection-ui) for
replacement/augmentation, lifecycle, and error-handling examples.

### History ownership and remaining work

The server owns retained rows, wrapping information, buffer identity, and
graphics anchored to that history. Each viewer owns a viewport into it.
Prefer bounded viewport/range requests and a small prefetch cache over shipping
all retained history on every frame.

| Concern | Proposed direction / decision required |
|---------|-----------------------------------------|
| Follow-tail | Follow live output until the user scrolls away. Preserve the historical view during new output and expose a new-output indicator plus an explicit return-to-live action. |
| Viewport anchoring | Row identities and a generation anchor each view. Evicting selected text invalidates the selection explicitly; offscreen selection is distinct from evicted selection. |
| Resize and reflow | Resolve wrapping on the server. This milestone conservatively invalidates selection/viewport generations on resize/reflow. Preserving logical anchors through reflow remains future work. |
| Alternate screen | Normal history stays distinct from alternate-screen application state. Switching buffers invalidates selection; alternate-screen redraws do not become normal history. |
| Mouse wheel arbitration | Shift reserves local history navigation during application mouse capture. A local drag retains ownership through release/cancellation. |
| Input while viewing history | Historical pointer input remains local; typing/paste clears selection and returns live before delivery. Copy preserves the historical view. |
| Selection and copy | Server-resolved extraction spans offscreen rows, wide/combining cells, and soft wraps. Rich clipboard formats remain future work. |
| Graphics | Preserve KGP/Sixel anchors, crops, and lifetime through history capture and reflow. Re-upload visible history resources when needed; define animation behavior for historical and hidden placements. |
| Retention and eviction | Bound rows and bytes, including graphics. Distinguish server retention from browser/GPU residency. Evicting a browser texture must not destroy server history that can still be revisited. |
| Multiple viewers | Keep each viewer's scroll position and cache independent. A shared mutable history offset is not a sufficient multi-viewer contract. |

Acceptance cases should include output continuing while scrolled up, return to
live view, history eviction, resize while selecting, wide characters across
wraps, alternate-screen transitions, and graphics spanning live/history rows.
Scrollback, selection, and reconnect behavior should be designed together.

## Other gaps before this is a supported feature

| Area | Current gap | Direction |
|------|-------------|-----------|
| Completed frames | HWT1 honors DEC mode 2026 with atomic capture gating and a one-second watchdog. Unmarked updates and timeout recovery can still expose intermediate state. | Extend the marked/unmarked workload corpus and measure timeout behavior on slow connections; do not substitute an arbitrary per-frame delay. |
| Graphics geometry | Native sizing is fixed, but ordinary one-axis KGP sizing still defaults the omitted axis to one cell. | Audit aspect-ratio sizing, source rectangles, offsets, clipping, layering, relative placements, Unicode placeholders, scrolling, and reset behavior against a reusable corpus. |
| Change capture | Full snapshots and full-grid scans remain on the hot path; per-token impact work may also contribute. | Profile lock time, allocation, parsing/impact production, projection, and serialization separately. Consider revisioned dirty rows/rectangles and graphics changes while retaining full snapshots for recovery. |
| Wire efficiency | Scrolling rewrites many cell records; complete placement/resource-key lists travel each revision. | Evaluate row runs, server-defined scroll/copy operations, compact style/glyph references, and placement deltas before choosing compression. These remain rendering-state operations, not ANSI emulation. |
| Remote latency | One GPU-completion acknowledgement gates the next frame. | Measure realistic RTT and bandwidth. Evaluate a bounded credit window or a different acknowledgement boundary without allowing stale-frame or texture queues to grow indefinitely. |
| GPU work | Full viewport geometry is rebuilt; tiny images can create many textures/binds. | Measure preparation, uploads, draw calls, and GPU duration. Evaluate persistent row geometry and small-image atlases/texture arrays without changing compositing order. |
| Text fidelity | Bundled Nerd Font default, worker-side loading, per-view font selection, and measured cell-fit metrics exist. Missing-glyph fallback and rasterization remain browser/OS-dependent; no broad shaping guarantee. | Qualify more fonts, scripts, decorations, and fallback glyphs; preserve font-owned borders and server-owned spans rather than introduce procedural glyph replacements. |
| Resizing / zoom | Auto text-size controls, fixed-grid presets, element fitting, and HMP1 primary resize authority exist. Logical cell metrics and maximum raster scale remain fixed. | Qualify tiny/hidden/large containers and native-primary geometry; higher-resolution re-rasterization for zoom and live font-family/DPR changes remain separate work. |
| Input coverage | Shared encoding now lives in Hex1b, but keyboard layouts/IME have limited coverage and some keys stay browser-owned. | Extend modifier, composition/paste, mode-change, repeat, focus-loss, and browser-shortcut coverage. Decide advanced keyboard and pixel-mouse scope explicitly. |
| Browser UX | Text scrollback/selection/copy exist. Search, hyperlink activation, full accessibility, and complete touch behavior remain absent. | Build remaining features on server text/history and safe metadata, with keyboard-accessible controls and a real screen-reader strategy rather than only a text mirror. |
| Recovery | Views can detach and attach to a process-local persistent instance, each with a fresh viewport/selection and baseline. GPU failure is surfaced; there is no automatic reconnect or durable recovery. | Define recovery across transport/device/server failure, resource restoration, authorization, and retention policy beyond the spike's explicit attach/end actions. |
| Integration | The demo consumes a public experimental adapter, but stable hosting and browser-component APIs remain undefined. | Stabilize embedding and hosting APIs independently of HWT1, which remains internal to the paired server/client implementation. |
| Compatibility | WebGPU-preferred auto selection and explicit WebGPU/WebGL2 modes exist; exercised mainly in one Chromium/macOS environment. | Establish a browser/OS/GPU matrix and qualify backend performance. There is no Canvas2D terminal renderer or automatic runtime device-loss recovery. |

## Proposed production architecture

Keep four boundaries explicit:

1. **Terminal presentation state:** a coherent, versioned view of cells, cursor,
   modes, resolved graphics, history, and completed-update boundaries. A change
   feed should support efficient consumers without exposing mutable internals
   or requiring a full snapshot for every event.
2. **Transport/session service:** connection negotiation, bounded frame/resource
   delivery, per-viewer viewport state, input authorization, and recovery.
   Keep this separate from owning the workload or deciding how to launch a shell.
3. **Browser component:** mount/dispose, focus, fit/resize, input capture, renderer
   lifecycle, viewport/selection, and events for state/errors. Its rendering
   backend should consume the same resolved model rather than understand ANSI.
4. **Host application:** authentication, terminal creation and retention,
   workload permissions, routing, and any policy for shared sessions.

The current HWT1 implementation notes document revisions, resources, input, and limits.
A production protocol still needs negotiated capabilities, terminal/session identity,
durable epochs across reattachments, broader recovery dependencies, and resource
ownership beyond this milestone's per-view history generations. Resource replacement/eviction must not
invalidate an in-flight frame. Negotiated limits should cover decoded bytes as
well as wire sizes, counts, queues, and per-connection work.

The spike now reuses HMP1's explicit primary for resize authority and permits
input from all peers. Primary loss does not elect a replacement. A supported
release must still decide authorization and retention policies, and how far to
share change capture/projection work beyond the common producer state.
Do not add a parallel HWT1 election to solve a hosting or performance concern.

The current demo is local-only, checks WebSocket and mutating-request origins,
and allows four shared terminal instances and eight web views. It runs a shell
with the host's privileges and has no
production authentication/session-authorization model. These checks are not a
reason to expose it through a reverse proxy. Production hosting needs trusted
session creation, authorization for attach/input/resize/terminate, transport
security, quotas, idle/disconnect policy, and careful handling of untrusted
terminal output, links, clipboard requests, and graphics payloads.

## Package distribution

[`@hex1b/web-terminal`](../src/web-terminal/README.md) is the chosen npm
package name. It is published to npmjs, not GitHub Packages. Strict TypeScript sources
produce ES modules, declarations, a module worker, and the licensed default
font. `WebTerminalDemo` consumes this package rather than maintaining another
copy of the browser implementation.

The package's complete `dist/` tree can be served as static assets with its
relative module/font layout intact. The sample uses an import map for the npm
entry point and includes generated assets in `dotnet publish`. Node.js and the
TypeScript compiler are build-time dependencies, not server runtime dependencies.
Bundled consumers must ensure their toolchain emits the worker and font assets;
do not assume importing a library module automatically copies every asset.

CI uses the existing, single NuGet version calculation for the npm package too:
PR browser previews are downloadable workflow artifacts; NuGet PR previews
continue to go to GitHub Packages. Main/release builds use npmjs after trusted
publishing is configured and enabled. The initial manual `0.1.0` publish
bootstraps npm package ownership and publisher configuration, not a separate
automated version stream. See the
[publishing guide](web-terminal-publishing.md) for setup and release channels.

Packaging does not freeze HWT1. Keep the server and client implementations
paired; do not infer wire compatibility from the HWT1 name, its version field,
or a historical NuGet package with the bootstrap version number.
An embedded-JavaScript .NET helper or embedded-resource distribution remains
separate work and should reuse the same frontend build if introduced.

The component mounts into a supplied element without global DOM ownership.
Production embedding still needs a deliberate CSP, worker/font hosting, and
session-authorization policy; do not assume every consumer permits inline
scripts, `eval`, or `blob:` workers.

Do not add WASM simply because this is a terminal. Introduce it only for a
measured bottleneck or a specific text-rasterization/shaping requirement that
justifies its download, startup, memory, and worker-integration costs.

## Suggested milestones

| Milestone | Deliverable and exit evidence |
|-----------|------------------------------|
| 1. Establish internal invariants and a corpus | Document the evolving HWT1 implementation, add viewport ownership and repeatable text/graphics/input recordings, extend synchronized-output coverage, and address remaining geometry ambiguities. Equivalent cases should agree across browser, snapshots, SVG, and replay where their presentation capabilities overlap; this does not freeze a third-party wire contract. |
| 2. Make scrollback usable | Text viewports, follow-tail, stable row anchors, wheel arbitration, and selection/copy form the first milestone. Follow with historical graphics, reflow-preserved anchors, and range-prefetch optimization. |
| 3. Make performance measurable, then improve it | Capture end-to-end stage timings and memory under text scrolling, GlobeDemo, SixelCloudDemo, and KgpCloudDemo. Agree numeric targets before optimizing. Reduce the demonstrated capture/wire costs and test slow viewers and realistic network delay. |
| 4. Complete and qualify the browser/hosting boundary | The server pipeline is in Hex1b; the sample has element-local mount/dispose, focus/resize, shared-source HMP1 attachment, and explicit terminal lifetime. Validate multi-view authority, independent disposal, and embedding in an unrelated page; stabilize errors/capabilities/assets and measure remaining per-view projection/cache costs. |
| 5. Harden for supported use | Add reconnect/device-loss recovery, long-running resource checks, protocol fuzzing, accessibility and input coverage, browser/OS qualification, and a secure hosting model. Decide what is intentionally unsupported. |
| 6. Package and publish | The TypeScript npm package and coordinated release pipeline are implemented. Configure registry ownership/trusted publishing and extend consumer/browser qualification without creating a second renderer. |

Performance runs should separate workload bytes from projected bytes and
separate state revisions from completed visual frames. Measure parsing/impact
production, snapshot locking/copying, projection, serialization, network,
decode/preparation, GPU upload/execution, and input-to-visible-response latency.
Use sustained runs, warm and cold caches, multiple grid sizes and DPRs,
allocation/resource plateaus, and p95/p99 latency rather than average FPS alone.

The existing Surface benchmark project also has an unrelated stale
`ListNode.SelectedIndex` reference. During the spike, unchanged surface
benchmarks were run through an isolated launcher against pre/post-fix assemblies.
That supplied a local sanity comparison, not a reproducible web-terminal
performance suite. Repairing and extending the normal harness belongs in the
measurement milestone.

## Decisions still open

Before promoting the spike to a feature, settle the supported browser/backend
matrix; historical graphics and reflow-preserved selection; advanced input
scope; production multi-view cost/authorization and detach/reattach retention; and hosting
authorization boundaries. Stable public API commitments should follow those
decisions; the npm distribution does not imply they are settled. None requires
a second browser terminal emulator.

## Implementation and evidence map

| Area | Starting point |
|------|----------------|
| Running the playground and current guardrails | [Sample README](../samples/WebTerminalDemo/README.md), [host](../samples/WebTerminalDemo/Program.cs) |
| State notification and bounded delivery | [Hwt1PresentationAdapter](../src/Hex1b/Hwt1/Hwt1PresentationAdapter.cs), [BrowserSession](../samples/WebTerminalDemo/BrowserSession.cs) |
| Projection and wire validation | [HWT1 internal implementation notes](web-terminal-protocol.md), [Hwt1RenderProjection](../src/Hex1b/Hwt1/Hwt1RenderProjection.cs), [protocol.ts](../src/web-terminal/src/protocol.ts) |
| Worker and rendering | [terminal-worker.ts](../src/web-terminal/src/terminal-worker.ts), [renderer.ts](../src/web-terminal/src/renderer.ts) |
| Browser-independent peer/framebuffer regressions | [Package tests](../src/web-terminal/tests): metadata authority validation and framebuffer/logical-geometry calculations using a stub GPU interface. |
| Mounted browser component regressions | [DPR2 browser fixture](../samples/WebTerminalDemo/tests/mount.browser.js): actual DOM/input/ResizeObserver and cleanup with a mock Worker; no WebSocket, terminal instance, or GPU-worker integration. |
| Mounting, playground, and browser/server input | [web-terminal.ts](../src/web-terminal/src/web-terminal.ts), [main.ts](../samples/WebTerminalDemo/client/main.ts), [mouse-input.ts](../src/web-terminal/src/mouse-input.ts), [Hwt1Input](../src/Hex1b/Hwt1/Hwt1Input.cs) |
| Delivery, projection, native sizing, and input regressions | [Hwt1PresentationAdapterTests](../tests/Hex1b.Tests/Hwt1PresentationAdapterTests.cs), [WebTerminalProjectionTests](../tests/Hex1b.Tests/WebTerminalProjectionTests.cs), [KgpNativeSizingTests](../tests/Hex1b.Tests/KgpNativeSizingTests.cs), [BrowserMouseInputTests](../tests/Hex1b.Tests/BrowserMouseInputTests.cs) |
| PTY environment and downstream graphics | [UnixPtyEnvironmentTests](../tests/Hex1b.Tests/UnixPtyEnvironmentTests.cs), [HMP replay tests](../tests/Hex1b.Tests/Hmp1/Hmp1KgpStateReplayTests.cs), [TerminalWidgetKgpTests](../tests/Hex1b.Tests/TerminalWidgetKgpTests.cs), [KgpSvgExportTests](../tests/Hex1b.Tests/KgpSvgExportTests.cs) |

Earlier single-view browser interaction checks exercised actual canvas focus,
shell editing,
mouse clicks and wheel selection in MouseTest, exact splitter dragging at DPR2,
outside-canvas releases, movement coalescing, and blur cleanup. These exploratory
checks need to become a repeatable frontend/integration suite rather than
remain session-local browser scripts.

Mounted DOM/input, `ResizeObserver`, and component-lifetime checks now have a
persisted Playwright CLI fixture. It uses an isolated DPR2 context and a mock
Worker, including pending-mount focus isolation and abort cleanup, without
opening WebSockets or creating terminal instances. Seven zero-dependency Node
cases and this mounted Chromium fixture passed in the recorded local checks.
Follow-up targeted Release .NET tests passed 1,377 cases across HMP1/HWT1 and
related KGP replay/animation paths; the demo Release build passed with zero
warnings/errors. The persisted browser fixtures additionally cover real
multi-view worker/WebSocket lifetimes, primary resize and handoff, shell/input,
scaled MouseTest interaction, GPU sprite readback, Sixel/KGP resource reuse, and
late attachment to silent animation. These focused results do not establish
CI wiring, broad HMP graphics/performance stability, or browser/device qualification.
