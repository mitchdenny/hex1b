# @hex1b/web-terminal

The first-party GPU-rendered browser terminal for Hex1b. It renders server-authoritative
cells and graphics in a module worker, with local input routing, producer-backed
history and selection, clipboard actions, and primary/secondary view sizing.
There are no runtime package dependencies.

**Experimental and paired with Hex1b:** this client speaks the evolving HWT1
transport implemented by the matching Hex1b server. HWT1 and internal browser
modules are not a supported third-party protocol or renderer API. The bootstrap
npm version `0.1.0` must be paired with the server build from the **same
implementation commit**; it is not compatible merely by version number with the
historical Hex1b NuGet `0.1.0`. Subsequent normal CI releases coordinate npm and
NuGet versions; use matching builds from the same release.

## Install and mount

```sh
npm install @hex1b/web-terminal
```

Give the container a nonzero width and height. Mount resolves after a connected
frame has been presented, or rejects on initialization failure or a 30-second
first-frame timeout.

```html
<div id="terminal" style="width: 100%; height: 480px"></div>
```

```ts
import { WebTerminal } from "@hex1b/web-terminal";

const container = document.getElementById("terminal");
if (!container) throw new Error("Missing terminal container");

const terminal = await WebTerminal.mount(container, {
  url: "/ws/terminal", // Your matching Hex1b HWT1 WebSocket endpoint.
  sizing: { mode: "auto", fontSize: 16 },
  onStatus(message, level) {
    console.log(level, message);
  }
});

terminal.focus();
// On component teardown:
// terminal.dispose();
```

`url` accepts a string or URL; relative URLs resolve against the page, and
`http:`/`https:` become `ws:`/`wss:`. An optional `AbortSignal` cancels mounting
or disposes a mounted view. Disposal removes only the appended element and its
connection, not the container or server-side shared terminal.

### Live transports

Supply **exactly one** of `url` or `transport`. TypeScript rejects both/neither,
and JavaScript callers receive a `TypeError` before a worker or connection is
created. `url` is only shorthand for the same first-party transport:

```ts
import { WebTerminal, createWebSocketTransport } from "@hex1b/web-terminal";

const container = document.getElementById("terminal");
if (!container) throw new Error("Missing terminal container");
const terminal = await WebTerminal.mount(container, {
  transport: createWebSocketTransport("/ws/terminal"),
});
```

Both forms normalize URLs identically and run the WebSocket directly in the
rendering worker; frames and controls do not detour through the main thread.
Custom transports run in the host JavaScript context. Hex1b supplies the bridge
to its own worker, so adapters need neither a replacement worker nor knowledge
of private worker messages. All exports are also available from the standalone
`dist/index.js` ES module, with the same API and no framework dependencies.

A `TerminalTransport` implements `connect(context)`, returning a
`TerminalTransportConnection` synchronously or asynchronously. Return/resolve
when the channel is ready for controls. `context` is provided **before** attachment,
so even synchronous startup delivery has listeners. One early `onFrame` may
wait for connection readiness; **do not await it inside `connect`**.

The following adapter snippet assumes a host-provided `bridge.attach` operation.
It must install the supplied listeners before enabling delivery, honor `signal`
while attaching, and return a per-view channel (not the terminal workload):

```ts
import { WebTerminal, type TerminalTransport } from "@hex1b/web-terminal";

// `bridge` is supplied by your host, not by Hex1b.
const transport: TerminalTransport = {
  async connect({ signal, onFrame, onClose, onError }) {
    const channel = await bridge.attach({
      signal,
      onFrame, // (ArrayBuffer | Uint8Array) => Promise<void>
      onClose: (reason: string) => onClose({ reason }),
      onError,
    });
    return {
      // Forward opaque serialized controls unchanged. No keyboard/mouse/ACK parsing.
      send: (control: string) => channel.send(control),
      dispose: () => channel.detach(),
    };
  },
};

const container = document.getElementById("terminal");
if (!container) throw new Error("Missing terminal container");
const terminal = await WebTerminal.mount(container, {
  transport,
  onClose: details => console.log("View detached", details.reason),
  onStatus: (message, level) => console.log(level, message),
});
```

**Ordering and ownership:** deliver complete binary HWT1 frames in order, awaiting
each `onFrame` promise before delivering another. An `ArrayBuffer` is handed over
exclusively and may be detached; do not read, mutate, or reuse it after the call.
A `Uint8Array` backed by an `ArrayBuffer` is copied synchronously using only its
slice, leaving the host's buffer intact. Other views and shared buffers are not
supported. The existing 96 MiB HWT1 frame limit still applies.

`onFrame` completion means worker acceptance, **not GPU presentation or producer
ACK**. The worker forwards the actual HWT1 ACK only at its existing protocol
boundary: after renderer completion for presented frames, or when discarding a
mismatched revision / accepting an inventory fragment. Adapters must forward all
outgoing controls independently and unchanged; they must not manufacture ACKs,
wait for an ACK inside `send`, or use receipt completion to release the producer's
one-unacknowledged-state-frame gate. This preserves image resource lifetimes and
full-baseline/resync behavior. Concurrent deliveries or producer gate violations
fail the view rather than dropping deltas or accumulating frames.

`send(control)` may return `void` or `Promise<void>`. Hex1b waits for completion
before invoking the next send, preserving completion order as well as invocation
order. Resolve after the underlying channel accepts the control in order, not
after a response frame. Throw/reject to report failure. Queued controls are bounded
to 256 messages / 1 MiB (including the active send); overflow fails the view. The
WebSocket adapter additionally caps the browser's outgoing buffer at 1 MiB.
Adapters must keep their own native/channel buffering bounded too.

**Lifetime:** `signal` aborts on disposal, timeout, failed attachment, fatal error,
or close. Cancel pending attachment and detach listeners promptly. Hex1b disposes
any connection returned after cancellation. `dispose()` is synchronous, idempotent,
nonthrowing, and should initiate per-view channel teardown, never terminate the
server terminal. Late callbacks cannot revive a disposed view. Use `onError(Error)`
for a fatal transport failure (reported through `onStatus`, rejecting a pending
mount), and `onClose({ reason })` for actual custom-channel closure. Neither errors
nor local teardown invent WebSocket status codes. There is no automatic reconnect.
Recording APIs and formats are unchanged.

The public-bundle graphics regression needs only a static server, not a terminal
server. After building, serve this package directory as the HTTP root, open its
`/tests/` URL in an isolated Playwright CLI session, and run
`playwright-cli -s=transports run-code --filename src/web-terminal/tests/transport.browser.js`
from the repository root. It verifies real worker rendering, transferred buffers,
KGP/Sixel pixels, retained-image movement/release, ACK ordering, controls, and
closure without any WebSocket, using WebGL2 and WebGPU when available.

### Light and dark terminal palettes

Supply JSON-compatible palettes independently for light and dark mode. Palette names
are not required: hosts own their presets and may derive terminal-specific colors
from their design system. Colors must be opaque `#RRGGBB` strings.

```ts
import { WebTerminal, defaultDarkPalette, defaultLightPalette } from "@hex1b/web-terminal";

const container = document.getElementById("terminal");
if (!container) throw new Error("Missing terminal container");
const terminal = await WebTerminal.mount(container, {
  url: "/ws/terminal",
  colorMode: "system", // "light", "dark", or the browser's prefers-color-scheme.
  lightModePalette: { ...defaultLightPalette, background: "#fafafa" },
  darkModePalette: { ...defaultDarkPalette, background: "#202020" },
});

// A host such as a dashboard can drive the mode instead of following the OS.
terminal.setColorMode("light");
terminal.setPalette("dark", { ...defaultDarkPalette, foreground: "#eeeeee" });
```

`TerminalPalette` requires `foreground`, `background`, and `ansi` (exactly 16
colors: black, red, green, yellow, blue, magenta, cyan, white, then their bright
variants). Optional `cursor` overrides the cursor tint. `selectionForeground`
and `selectionBackground` control selected text and its opaque background;
when omitted, they default to the palette's background and foreground,
respectively. Optional `extended`
maps indices 16–255 to custom colors. Unspecified extended entries use the xterm
216-color cube and gray ramp. Palettes are validated and copied on assignment.

Dark mode is the default. Omitting the palette options selects **Hex1b Dark**
and **Hex1b Light**, exported as `defaultDarkPalette` and `defaultLightPalette`.
These are the package's only built-in palettes. The
`colorMode` and `resolvedColorMode` getters expose the requested and effective
modes. Changing the active palette or mode repaints existing cells without
reconnecting, sending application input, resizing, or clearing selection.
Previously captured scrollback retains its color references and uses the active
palette when viewed. Explicit RGB colors and image pixels are not remapped.
Reverse and dim apply after color resolution; bold uses the bold font, not an
automatic bright-color substitution.

Selection is rendered by both GPU backends using the active palette, including
over explicit RGB text and image placements. It swaps the terminal's default
colors, not each cell's colors. Selected text decorations use the selection
foreground; colored emoji retain their colors and concealed text stays hidden.
Changing palettes recolors an existing selection without changing its text or
range. This replaces the translucent UI-accent overlay; selection colors no
longer depend on the embedding page's `--cp-accent`.

The client opts into `indexed-v1` colors only after the matching HWT server
advertises support, then receives a full reference-colored frame. Older clients
continue receiving resolved RGBA; this client can still display older RGBA
frames, but cannot recolor their already-resolved text. The wire extension uses
the existing cell color fields, not extra per-cell JSON. This spike does not add
application-driven OSC palette mutation or change server color-query responses.
Use matching client/server builds for the palette feature.

#### Hex1b's default color pair

The defaults adapt Chris Kempson's **Tomorrow Night Eighties**, not Ghostty's
Tomorrow Night-like palette. They use a neutral-charcoal/warm-stone pair,
with the default foreground and background exchanged between modes:

| Mode | Background | Foreground |
|---|---|---|
| Hex1b Dark | `#323232` | `#d4d0c8` |
| Hex1b Light | `#d4d0c8` | `#323232` |

The named chromatic slots are retuned in **OKLCH**, rather than RGB-inverted.
Starting from Eighties red, green, yellow, blue, purple, and aqua, the hue
adjustments are respectively -4, -7, +4, -5, -7, and +4 degrees. Dark-mode normal
slots use 81% of the original chroma and bright slots 89%; light mode uses 95%
and 100% for richer accents. Chroma is reduced when necessary to stay in the
sRGB gamut. Lightness is solved independently for each background: dark mode
targets approximately **5.2:1** normal and **6.3:1** bright contrast; light mode
targets **4.6:1** and **5.2:1**, allowing lighter colors that remain readable.
The resulting rounded hex values are shipped as constants; no color
conversion or palette-generation dependency runs in the browser.

This preserves each slot's hue across modes while softening Eighties' stronger
accents. Default text contrast is **8.34:1** in both modes (original Eighties:
approximately 8.58:1). Bright chromatic slots are *darker* in light mode, giving
them more emphasis rather than washing them out. Bright black is a readable
mid-gray in each mode; the remaining ANSI black/white slots retain their
conventional neutral roles for applications that explicitly choose them.

These contrast targets apply to opaque, non-dim chromatic text against the
default background, not every foreground/background combination, selection,
image overlay, or explicit RGB color.

The npm package and `dist/` include Hex1b's `LICENSE`. Preserve it and the bundled
font license when vendoring.

### Connection closure and workload completion

Use `onClose(details)` to observe the browser's actual WebSocket close event.
`TerminalCloseDetails` contains readonly `code`, `reason`, and `wasClean` fields.
This payload is unchanged for both WebSocket configuration forms. Custom
transports use `TerminalTransportCloseDetails`; `code` and `wasClean` are absent
unless the underlying channel actually reports native WebSocket details.
The callback runs once with the view already disconnected, **even if the socket
closes before the first HWT frame or authoritative HMP peer state**. A pending
mount rejects after the callback, so capture any host state before calling mount.
The details object is frozen. Reasons are untrusted text; do not render them as HTML.

```ts
import { WebTerminal, type TerminalCloseDetails } from "@hex1b/web-terminal";

const container = document.getElementById("terminal");
if (!container) throw new Error("Missing terminal container");
let closed: TerminalCloseDetails | undefined;
try {
  const terminal = await WebTerminal.mount(container, {
    url: "/ws/terminal",
    onClose(details) {
      closed = details;
      console.log("View closed", details.code, details.reason, details.wasClean);
    }
  });
  terminal.focus();
} catch (error) {
  // closed is set for a transport close, but not for local initialization failure.
  console.error("Mount failed", closed, error);
}
```

Transport loss is **not workload completion**. Code 1006 means the browser did not
receive a close frame; even code 1000 and `wasClean: true` only describe transport
closure, not successful process exit. Hosts can define an application close-code
contract and send it when their authoritative producer reports completion,
including before any HWT frame exists. Hex1b does not assign workload meaning to
close codes or reason strings. HTTP upgrade failures generally surface as 1006;
browsers do not expose the rejected HTTP response body or status through this API.

The client never retries automatically. The host decides whether to mount a new
view after transport loss or leave an ended tab/dialog visible. Abort, explicit
disposal, mount timeout, and local initialization/renderer failures do not
synthesize `onClose`; no callback runs after disposal. Callback exceptions are
reported to the host, not swallowed or retried, and do not leave mounting pending.
The API does not retain the producer after exit or promise a final rendered frame.

### Browser and deployment requirements

Use a browser with WebGPU or WebGL2, module workers, transferable OffscreenCanvas,
worker animation frames, ResizeObserver, and CSS Font Loading. WebGPU requires
HTTPS or localhost; WebGL2 rendering also works on ordinary HTTP origins.
Clipboard API access still requires a secure context, browser permission and,
for relevant actions, a user gesture. There is no Canvas2D terminal-rendering fallback.
Renderer selection does not change transport security: use HTTPS/WSS to protect
terminal input and output.

### Renderer selection

Set the mount-time `renderer` option to `"auto"` (the default), `"webgpu"`, or
`"webgl2"`. Auto prefers WebGPU and uses WebGL2 if the secure context, API,
adapter, device acquisition, or presentation context is unavailable. Explicit
modes require that backend and report an error rather than falling back.
Shader, font, validation, and unexpected initialization errors are not
compatibility fallbacks. Runtime GPU/context loss terminates the view with an
error; it does not switch backends behind the caller's back.

```ts
const terminal = await WebTerminal.mount(container, {
  url: "/ws/terminal",
  renderer: "webgl2", // Use WebGL2 even if WebGPU is available.
  onStats(stats) {
    console.log(stats.renderer, stats.rendererFallbackReason);
  }
});
```

`stats.renderer` identifies the active backend after initialization.
`stats.rendererFallbackReason` explains an automatic fallback and is absent
for explicit selections and successful WebGPU initialization. Both backends
share glyph rasterization, frame preparation, clipping, and image ordering.
WebGPU preference is not a performance guarantee; compare representative
workloads on your target browsers and devices.

### Module and worker deployment

The package ships **one JavaScript file: `dist/index.js`**. This ESM bundle
contains the public main-thread API, terminal worker, and link-detection worker;
there is no runtime JavaScript module tree to copy. npm imports and public
exports are unchanged: import from `@hex1b/web-terminal`, not internal modules.

For static hosting or vendoring, copy `index.js` and `dist/fonts/` into the same
served directory, preserving the font paths and their license/provenance files.
Keep the package's MIT license too. Fonts are separate assets, not embedded in
the JavaScript. Alternatively, supply your own explicit `font.faces` URLs.
`index.js.map` is optional for runtime use; declarations and declaration maps
support TypeScript consumers.

With the bundle at `/web-terminal/index.js` and fonts at `/web-terminal/fonts/`,
this is a complete mount example (the host supplies the matching HWT1 endpoint):

```html
<div id="terminal" style="width: 100%; height: 480px"></div>
<script type="module">
  import { WebTerminal } from "/web-terminal/index.js";

  const terminal = await WebTerminal.mount(document.getElementById("terminal"), {
    url: "/ws/terminal",
    sizing: { mode: "auto", fontSize: 16 },
    onStatus(message, level) { console.log(level, message); }
  });
  terminal.focus();
  window.addEventListener("pagehide", () => terminal.dispose(), { once: true });
</script>
```

Workers load the **same bundle URL**, replacing its fragment with
`#hex1b-terminal-worker` or `#hex1b-link-detection-worker`. The actual filename,
path, and query string are preserved, so renaming the unmodified bundle works:
`/vendor/terminal.js?v=42` uses `/vendor/terminal.js?v=42#hex1b-terminal-worker`.
Worker entry selection runs only in dedicated worker contexts, not when importing
the API into a page. The default font resolves relative to the JavaScript URL,
not the page.

Both workers are module workers; they need neither Blob URLs nor `eval`.
Same-origin deployment works with `worker-src 'self'` without adding `blob:`.
Also configure JavaScript/WOFF2 MIME types and CSP permissions for scripts, fonts,
and the intended WebSocket endpoint. Browser worker-origin restrictions still apply.

**Rebundling into an application is a separate deployment choice.** Defaults
assume the unmodified ESM bundle's URL, not an arbitrary application bundle with
DOM startup side effects. A bundler may rewrite asset URLs or tree-shake worker
code. If needed, separately host the unmodified `index.js` from the same package
build and explicitly point both workers to it, with explicit font URLs:

```ts
const terminal = await WebTerminal.mount(container, {
  url: "/ws/terminal",
  workerUrl: "/web-terminal/index.js#hex1b-terminal-worker",
  linkDetectionWorkerUrl: "/web-terminal/index.js#hex1b-link-detection-worker",
  font: {
    family: "My Terminal Font",
    faces: [{ url: "/fonts/my-terminal.woff2", weight: "100 900", style: "normal" }]
  }
});
```

`workerUrl` and `linkDetectionWorkerUrl` accept nonempty strings or URLs;
relative strings resolve against the **page**, not the package. Overrides do
not copy fonts. Bundlers differ in dependency and asset handling; this package
does not claim universal or individually verified bundler support.

## Configuration and state

`WebTerminalOptions` includes:

| Option | Meaning |
| --- | --- |
| `workerUrl` | Optional module-worker entry; useful when worker assets are deployed separately. |
| `linkDetectionWorkerUrl` | Optional detection module-worker entry (`string \| URL`), parallel to `workerUrl`. |
| `links` | Opt-in per-view text detection and OSC 8 interaction policy; `false` disables all local links. |
| `onLinkDetectionError` | Feature-local detection diagnostics; also reported through `onStatus`. |
| `scale` | GPU backing scale `0.5`–`3`, or `"auto"` (default, bounded device pixel ratio). |
| `renderer` | `"auto"` (prefer WebGPU), `"webgpu"`, or `"webgl2"`; selected once per mount. |
| `font` | One family and optional downloadable font faces; see below. |
| `sizing` | `{ mode: "auto", fontSize?: number }` or `{ mode: "fixed", columns, rows, fontSize?: number }`. |
| `scrollbar` | Auto-hiding canvas overlay by default; `{ placement: "beside" }` reserves a gutter and stays visible while scrollable; `false` disables built-in chrome. |
| `padding` | Nonnegative CSS pixels: a uniform number or `{ top?, right?, bottom?, left? }`. Omitted edges are zero. |
| `onLayoutChange`, `onMarkersChange` | Local content/gutter geometry and authoritative retained marker inventory for host-owned chrome. |
| `readOnly` | Initial per-view input policy; change it later with `setReadOnly(boolean)`. |
| `label` | Accessible label for the terminal's hidden keyboard input. |
| `onTitleChange` | Initial authoritative workload title, then distinct presented changes; see below. |
| `onClose` | Native WebSocket close details, including pre-mount transport failure; not workload completion. |
| `onProgressChange`, `onShellIntegrationChange` | Initial authoritative activity, then distinct presented changes for host-owned chrome. |
| `onWorkingDirectoryChange`, `onCommandMarkChange` | Initial authoritative OSC 7 directory and latest OSC 133 marker, then distinct presented changes; see below. |
| `inputBindings`, `onInput`, `actions` | Per-view input policy and custom actions. |
| `onSelectionUI` | Synchronous, cancelable UI notification hook. |

Font size is an integer from 8–32, defaulting to 16. Import `MIN_FONT_SIZE` and
`MAX_FONT_SIZE` from `@hex1b/web-terminal` for sizing controls. Requested fixed grids allow
1–300 columns and 1–100 rows, matching `Hex1bTerminal`'s minimum of one cell
in each dimension. Automatic sizing uses the same bounds. The producer still owns actual grid geometry.
`resize()`, `setSizing()`, and automatic resize requests require primary
ownership. `requestPrimary()` explicitly requests ownership; inspect `peer` or
`onRoleChange` to observe the result.

The handle exposes `geometry`, `peer`, `connected`, `readOnly`, `title`, `progress`, `shellIntegration`,
`workingDirectory`, `commandMark`, `stats`, `screenText`,
`sizing`, `viewport`, `layout`, `padding`, `scrollbar`, `markers`, `selection`, `inputBindings`, and `inputContext`.
Metrics start empty; check optional fields before using them. History may be
unavailable, and selection can be unavailable, none, pending, valid, or
invalidated. Narrow `viewport.available` and `selection.status` before using
their state-specific values. `screenText` reflects the presented viewport, not
an independently reconstructed ANSI buffer.

Callbacks include `onGeometry`, `onRoleChange`, `onTitleChange`, `onSizingChange`, `onStats`,
`onProgressChange`, `onShellIntegrationChange`, `onWorkingDirectoryChange`, `onCommandMarkChange`,
`onViewportChange`, `onSelectionChange`, `onStatus`, and `onInputError`.

### Live read-only views

Call `terminal.setReadOnly(true)` to disable application input on an already
mounted view. `terminal.readOnly`, `inputContext.readOnly`, and selection UI
notifications reflect the new policy. Use `setReadOnly(false)` to re-enable input;
neither call remounts, reconnects, releases the peer's primary role, or changes
the server's current grid. A writable primary resumes automatic sizing requests.
Mutating the original `options.readOnly` after mount has no effect.

Read-only blocks keyboard/text/IME input, application mouse reports, direct
`paste()`/`pasteClipboard()`, the paste paths of `runAction()`, `resize()`,
`setSizing()`, `requestPrimary()`, and automatic resize requests. Explicit input
methods throw when disabled; DOM application input is not forwarded. Routing
overrides cannot bypass this policy. Custom actions can still run local operations,
but any terminal input method they call remains gated.

Active pointer capture and queued mouse movement, pending composition, queued
resize, and pending clipboard pastes are cancelled on policy change. Quickly
re-enabling input does not revive a previously pending paste. Commands already
dispatched cannot be recalled. Output, local selection gestures, history
navigation, resync, and copying remain available, including a copy already in
progress. UI notifications follow their usual coalescing rules.

```ts
import { WebTerminal } from "@hex1b/web-terminal";

const container = document.getElementById("terminal");
const inputEnabled = document.querySelector<HTMLInputElement>("#input-enabled");
if (!container || !inputEnabled) throw new Error("Missing terminal controls");

const terminal = await WebTerminal.mount(container, {
  url: "/ws/terminal",
  readOnly: !inputEnabled.checked
});
inputEnabled.addEventListener("change", () => terminal.setReadOnly(!inputEnabled.checked));
```

**Client policy is not authorization.** Enforce it independently on each server
view with `Hwt1PresentationAdapter.IsReadOnly`, initially or at runtime. For
example, in the host's existing connection setup (C# snippet):

```csharp
var presentation = new Hwt1PresentationAdapter { IsReadOnly = true };
// Attach this presentation to the view's terminal and drive its existing transport loops.
// Only trusted host policy should grant writes:
presentation.IsReadOnly = false;
```

The adapter ignores producer-mutating browser input, resize, and primary requests
while read-only, but still processes acknowledgements, resync, history, selection,
and copy. This is **per presentation**, not a producer-wide input lock: direct
terminal automation and other authorized viewers continue. A policy change does
not retract a command the adapter already accepted. The host must update both its
server policy and browser UX; client changes do not authorize themselves, and the
server property does not automatically change client UI.

### Workload titles

The read-only `terminal.title` is the current presented workload title. An empty
string means unset or explicitly cleared; choose your own fallback. The optional
`onTitleChange(title)` callback runs once with the first authoritative presented
value, **including `""`, before mount resolves**. The getter is updated before
the callback. Later notifications report only distinct presented values. Identical
updates, same-title resyncs, cursor blinking, and statistics do not notify again.
Intermediate workload changes can coalesce; this is not an event for every OSC
sequence.

```ts
import { WebTerminal } from "@hex1b/web-terminal";

const container = document.getElementById("terminal");
const header = document.getElementById("terminal-header");
if (!container || !header) throw new Error("Missing terminal elements");
const resourceName = "Build service";

const terminal = await WebTerminal.mount(container, {
  url: "/ws/terminal",
  onTitleChange(title) {
    header.textContent = title || resourceName;
  }
});
console.log(terminal.title);
```

The callback uses elements and fallback text captured **before** mounting, not
the still-pending `terminal` result. The component does not change `document.title`,
your header, or the input's accessible `label` automatically. There is no title
subscription method or DOM title event.

Titles are normalized by the core to at most 4,096 UTF-16 code units, with C0,
DEL, and C1 controls removed, malformed surrogates replaced with U+FFFD, and
truncation at a Unicode scalar boundary. They remain **untrusted text**: markup
and bidi characters are preserved. Use `textContent`, not `innerHTML`; apply your
own presentation and bidi policies.

OSC 0 and OSC 2 set or explicitly clear the window title; OSC 1 is icon-only.
Use `ESC ] 0 ; text BEL` or `ESC ] 2 ; text BEL`; `ESC \` (ST) may replace BEL.
The UTF-8 input path also accepts Unicode C1 OSC (`U+009D`) and ST (`U+009C`).
Semicolons within `text` are literal. Existing OSC 22/23 saved-title extensions
update the same state, including after a late HMP1 attachment.
RIS, soft reset, screen clearing, and buffer switching preserve the title and
existing saved-title behavior. History inspection retains the current workload
title rather than a title associated with an old row.

Disconnect and disposal retain the last known title without a synthetic clear.
Disposal (including abort) stops title callbacks. Attach a new view to reconnect;
it receives its own initial current title. Preliminary disconnected relay frames
do not trigger the initial notification. Callbacks run directly like the other
state callbacks; thrown host errors are not swallowed or retried.

The required title field needs the matching server build. Missing or malformed
title metadata fails the connection; an older server is not silently treated as
an empty title.
The per-title bound is not a limit on all parser buffering or saved-stack depth.
An HMP1 snapshot with too much saved title state fails its 16 MiB replay limit
rather than silently discarding saved titles.

### Application progress and shell activity

`terminal.progress` exposes OSC 9;4 state as `{ state, percentage }`.
The states are `"none"`, `"normal"`, `"error"`, `"indeterminate"`, and `"warning"`.
Normal/error/warning percentages are integers from 0 through 100. None and
indeterminate have `percentage: null`; none means the host should hide its indicator.
This is application-reported progress, not inferred from output or CPU activity.
It is independent of `ProgressWidget`, which draws inside terminal cells.

`terminal.shellIntegration` exposes OSC 133 as `{ phase, lastExitCode }`.
The phases are `"unknown"`, `"prompt"` (A), `"commandLine"` (B),
`"executing"` (C), and `"finished"` (D). B means input after the prompt, **not**
command execution. Unknown does not mean idle. `lastExitCode` is a signed
32-bit integer, or null when no status was reported; null is not success.
A/B/C preserve the last reported result, and D replaces it, including clearing
it to null when the shell omits its status. No command text, history, or output
locations are retained by these APIs.

`terminal.workingDirectory` exposes OSC 7 state as `{ uri, host, path }`, all
`null` until the first report. `uri` is the raw reported `file://` URI; `host`
and `path` are derived from it (`host` is `""` for a local/unqualified
authority). A malformed or non-`file` URI leaves the previous value unchanged.

`terminal.commandMark` exposes the single most-recently-reported OSC 133 marker
as `{ phase, exitCode, rawParameters } | null` — `null` until the first marker.
`phase` uses the same enum as `shellIntegration.phase`. `exitCode` is non-null
only on a `finished` (D) marker. `rawParameters` is the verbatim
`key=value[;key=value...]` text trailing the marker (for example a
`cmdline_url` extension on marker C), or `null` when none was present; use the
exported `parseCommandMarkParameters(rawParameters)` helper to parse it into a
`Map`, or `getCmdlineUrl(mark)` as a shortcut for the `cmdline_url` entry. This
is **not** a command-mark history — this getter exposes only the latest marker,
mirroring `shellIntegration`. Use `markers` / `onMarkersChange` for the retained
inventory and `getCommandMarkDetails(id)` for raw parameters on demand. Do not
reconstruct history from coalesced `onCommandMarkChange` callbacks.

All four getters return defensive copies. Their callbacks receive the first
authoritative presented state before mount resolves, then distinct presented
changes. All four getters are updated before their corresponding activity
callback. Callbacks use the same direct, synchronous host-callback convention
as title changes; host exceptions are not swallowed or retried.

Frames coalesce: the browser might see only Finished for a fast command, or
miss an entire command whose final state is unchanged. These callbacks are
**current-state notifications, not a lossless start/finish event stream**.
Resync/replay never invent commands, unchanged state does not notify again,
and a new mount receives its own baseline.

This example creates optional chrome outside the terminal:

```ts
import { WebTerminal, getCmdlineUrl } from "@hex1b/web-terminal";

const status = document.createElement("span");
const cwd = document.createElement("span");
const progress = document.createElement("progress");
progress.max = 100;
progress.hidden = true;
const container = document.createElement("div");
container.style.cssText = "width:800px;height:480px";
document.body.append(status, cwd, progress, container);

const terminal = await WebTerminal.mount(container, {
  url: "/ws/terminal",
  onProgressChange(value) {
    progress.hidden = value.state === "none";
    progress.dataset.state = value.state; // Host CSS can distinguish error/warning.
    if (value.percentage === null) progress.removeAttribute("value");
    else progress.value = value.percentage;
  },
  onShellIntegrationChange(value) {
    status.textContent = value.phase +
      (value.lastExitCode === null ? "" : ` (last exit ${value.lastExitCode})`);
  },
  onWorkingDirectoryChange(value) {
    cwd.textContent = value.path ?? "";
  },
  onCommandMarkChange(value) {
    const cmdlineUrl = getCmdlineUrl(value);
    if (cmdlineUrl) console.log("Command link:", cmdlineUrl);
  },
  onStats(stats) {
    if (!stats.connected) {
      progress.hidden = true;
      status.textContent = "Disconnected";
    }
  }
});
console.log(terminal.progress, terminal.shellIntegration, terminal.workingDirectory, terminal.commandMark);
```

No title, document chrome, or progress UI is changed automatically by the
component. The sample endpoint must be supplied by your application.
Callbacks can run before the `terminal` variable is assigned; use their
arguments during initial mounting.

RIS resets progress to None and shell integration to Unknown. Soft reset,
screen clearing, resize, and buffer switches preserve them. OSC 9;4 state 0
clears only progress; a shell completion does not implicitly clear it.
Disconnect, process exit, and disposal retain the last reported values
without inventing a completion or progress clear. Check `connected` before
showing active chrome, and remount to reconnect. Disposal stops callbacks.
Snapshots and historical viewports carry current activity, not activity at
the time a particular row was printed.

The core accepts BEL, ESC-backslash ST, and decoded Unicode C1 terminators
through its UTF-8 input path. Determinate progress requires unsigned decimal
0-100; clear/indeterminate allow an omitted percentage and ignore its optional
value. OSC 133 supports the basic A/B/C forms and D with an optional signed
decimal exit status (an empty field also means no status). Missing required,
malformed, overflowed, excess, or unsupported arguments do not change state.
The raw-output presentation path still forwards the original sequences to
supporting outer terminals. Required activity metadata needs the matching
server build; invalid/missing wire fields fail the connection, not silently
fall back to default state.

## Scrollbars, padding, and retained markers

The terminal canvas remains GPU-rendered. Scrollbar chrome uses a separate,
transparent main-thread Canvas2D layer with the same behavior under WebGPU and
WebGL2. Change presentation without remounting or replacing the WebSocket:

```ts
terminal.setPadding({ top: 8, right: 16, bottom: 12, left: 24 });
terminal.setScrollbar({ placement: "beside", width: 12, markers: true });
terminal.setScrollbar({ placement: "overlay", hideDelay: 900, fadeDuration: 300 });
terminal.setScrollbar(false); // Keep history/navigation; paint your own chrome.
```

Padding is **outside** cells and scrollbar chrome, in CSS pixels, not rows or
backing-store pixels. `setPadding(8)` sets all four edges; omitted edges in an
object become zero. Beside mode stays visible without fading while scrollback is
available, and reserves its gutter even when there is no history. Its default
frame opacity is always `1`; `hideDelay` and `fadeDuration` apply only to overlay
mode. Idle beside scrollbars do not schedule fade timers or animation frames.
Overlay mode auto-hides and does not reduce columns. An auto-sized primary
can request a new producer grid when the available space changes. Fixed grids
and secondary/read-only views instead fit the authoritative grid; changing
chrome never claims primary. Tiny/hidden containers may have no drawable area.

`layout` / `onLayoutChange` expose immutable CSS-pixel measurements relative to
`terminal.element`: outer `width`/`height`, displayed `content` rectangle,
`scrollbar` rectangle (or `null`), normalized `padding`, and displayed
`cellWidth`/`cellHeight`. Use the content rectangle for host hit testing, not the
outer mount box. `padding` and `scrollbar` getters expose normalized current
settings; the latter is `false` when disabled. Selection, links, input, and
graphics remain aligned to content, not the added padding/gutter.

Layout and marker notifications may occur before `mount()` resolves. Read
their supplied snapshot during initialization, then initialize host-owned UI
from the returned handle as well. Related getters update before notification;
notifications may coalesce and do not form a change log. No notifications run
after disposal. Check `connected` and `viewport.available` before navigating
or synchronizing external chrome; retained display state on disconnect does
not imply an available producer.

### Configure the default capsule painter

`createDefaultScrollbarRenderer(appearance?)` returns an ordinary synchronous
`TerminalScrollbarRenderer`. The built-in `renderDefaultScrollbar(frame)` uses
the same factory with no overrides; there is no separate rendering API or
controller path for styled defaults. Given a mounted `terminal`:

```ts
import { createDefaultScrollbarRenderer } from "@hex1b/web-terminal";

const painter = createDefaultScrollbarRenderer({
  track: { opacity: 0.12 },
  thumb: { opacity: 0.7 },
  markers: { opacity: 0.85 }
});
terminal.setScrollbar({ placement: "beside", render: painter });
```

The readonly `TerminalScrollbarAppearance` has optional `track`, `thumb`, and
`markers` parts. Each accepts `color?: string` and `opacity?: number`;
`TerminalScrollbarMarkerAppearance` also accepts `errorColor?: string`.

| Part | Default opacity | Default color |
| --- | --- | --- |
| Track | `0.35` | `frame.colors.track` |
| Thumb | `1` | `frame.colors.thumb` |
| Markers | `1` | Each tick's resolved `color`, distinguishing its kind/outcome; falls back to `frame.colors.marker`/`error` |

The default canvas palette is monochrome: a grey thumb (`#999999`) over a
translucent dark track (`#202020`). Command input, execution, successful
completion, failed completion, and bookmarks use `#888888`, `#bbbbbb`,
`#999999`, `#eeeeee`, and `#dddddd`, respectively. Unknown outcomes use
`#aaaaaa`. These defaults do not inherit the embedding app's accent or danger
colors. Prompt marks remain omitted from the canvas rail.

Embedding CSS can override the live palette through `--cp-scrollbar-track`,
`--cp-scrollbar-thumb`, `--cp-scrollbar-marker`, `--cp-scrollbar-error`,
`--cp-scrollbar-command-line`, `--cp-scrollbar-executing`,
`--cp-scrollbar-success`, and `--cp-scrollbar-custom`. Explicit factory
`markers.color`/`errorColor` overrides remain available, as do per-marker
colors. Custom painters receive the resolved default shade on each
`TerminalScrollbarMarker.color`; `marker.color` is the explicit host override.

Part opacity must be finite and between `0` and `1`, inclusive. It **multiplies**
the frame's fade opacity and incoming `context.globalAlpha`; it does not replace
either. Color alpha is applied by Canvas2D as usual. A marker's explicit `color`
takes precedence over configured marker/error colors, then theme fallbacks.
Invalid per-marker CSS colors retain the safe fallback. The rounded focus
outline uses the configured thumb color or neutral thumb default and remains
independent of track/thumb/marker opacity. Thumb dragging suppresses both the
canvas outline and the DOM focus outline; keyboard focus remains visible.

Supplied appearance colors are validated when the factory is called. Use
nonempty **concrete CSS colors** supported by the browser's Canvas2D parser
(for example `#8b5cf6`, `rebeccapurple`, or `rgb(139 92 246 / 80%)`), at most
256 characters. Unresolved `var()`, CSS-wide keywords such as `inherit`,
`currentColor`, system/context-dependent colors, escapes, and comments are
rejected rather than silently ignored. Resolve host CSS variables first if
you want to snapshot their values. **Omit a color to keep theme changes live**:
the painter reads that fallback from each frame.

Options are snapshotted; mutating your original object does not change an
existing painter. Create and install another renderer to change its overrides.
The visible thumb is a capsule, inset by `min(2, width / 4)` CSS pixels on each
side, with radius half its smaller painted dimension. Styling changes painting
only: track, thumb, marker hit regions, and all navigation APIs remain unchanged.

### Custom synchronous painting and fade

This complete painter delegates geometry and colors to the default renderer but
uses a longer quadratic fade. It is the same policy demonstrated by
[the playground painter](../../samples/WebTerminalDemo/client/scrollbar-renderer.ts).
This example opts into its own fade policy for overlay placement. The playground
uses the non-fading default painter for this choice in beside mode.

```ts
import {
  WebTerminal, renderDefaultScrollbar, type TerminalScrollbarRenderer
} from "@hex1b/web-terminal";

const softFade: TerminalScrollbarRenderer = frame => {
  const { interaction, now } = frame;
  const active = interaction.near || interaction.hovered ||
    interaction.dragging || interaction.focused;
  const elapsed = Math.max(0, now - interaction.lastActivityAt - 1200);
  const remaining = active ? 1 : Math.max(0, 1 - elapsed / 700);
  const opacity = interaction.reducedMotion
    ? (active || elapsed === 0 ? 1 : 0)
    : remaining * remaining;
  renderDefaultScrollbar({ ...frame, opacity });
  return !active && opacity > 0; // Request the next animation frame until hidden.
};

const host = document.getElementById("terminal");
if (!host) throw new Error("Missing sized terminal host");
const terminal = await WebTerminal.mount(host, {
  url: "/ws/terminal",
  padding: 8,
  scrollbar: { placement: "overlay", render: softFade }
});
// After changing host-owned painter state or theme colors:
terminal.refreshScrollbar();
// On component teardown: terminal.dispose();
```

The callback is synchronous: never return a Promise. `frame.context` is prepared
for CSS-pixel drawing on `frame.canvas`; context state is isolated between
calls. The frame includes layout, viewport, nullable `pendingTarget` (the locally
desired row during navigation), track/thumb rectangles, marker rectangles,
nullable `hoveredMarker` (the hovered `TerminalScrollbarMarker`, including its bounds),
interaction state, monotonic `now` and `lastActivityAt`, default
`opacity`, and resolved theme colors. Return `true` only while another frame is
needed; an unconditional `true` creates an unnecessary animation loop.
`refreshScrollbar()` invalidates painting without sending terminal frames.
Painter failures are local scrollbar errors, not reasons to stop terminal output.

Painting does not redefine hit testing: the library owns thumb dragging,
track paging, marker clicks, proximity activation, and keyboard interaction.
The thumb takes precedence over overlapping marker ticks so dense markers cannot
prevent dragging. Alt+ArrowUp/ArrowDown navigates adjacent available markers.
The default painter respects reduced motion and the embedding theme.
`markers: false` hides ticks without removing the inventory or disabling
`scrollToMarker`. Inventory changes briefly reveal the scrollbar, including when
a bookmark is added after it has faded out. A custom painter can call `renderDefaultScrollbar(frame)` and
then add decoration; use `frame.colors` or your embedding theme rather than
assuming a dark terminal.

To draw completely different chrome, use the same callback without calling a
default painter. This snippet uses square geometry and keeps the normal fade;
the playground's **Custom Canvas2D** mode adds thumb grips and diamond markers.

```ts
import { type TerminalScrollbarRenderer } from "@hex1b/web-terminal";

const squareScrollbar: TerminalScrollbarRenderer = frame => {
  const { context, track, thumb, colors } = frame;
  context.save();
  try {
    const alpha = context.globalAlpha * frame.opacity;
    context.globalAlpha = alpha * 0.2;
    context.fillStyle = colors.track;
    context.fillRect(track.left, track.top, track.width, track.height);
    context.globalAlpha = alpha;
    context.fillStyle = colors.thumb;
    context.fillRect(thumb.left, thumb.top, thumb.width, thumb.height);
    for (const { marker, bounds } of frame.markers) {
      context.fillStyle = marker.exitCode != null && marker.exitCode !== 0
        ? colors.error : colors.marker;
      if (marker.color) context.fillStyle = marker.color;
      context.fillRect(bounds.left, bounds.top, bounds.width, bounds.height);
    }
    if (frame.interaction.focused) {
      context.strokeStyle = colors.marker;
      context.lineWidth = 1;
      context.strokeRect(thumb.left + 0.5, thumb.top + 0.5,
        Math.max(0, thumb.width - 1), Math.max(0, thumb.height - 1));
    }
  } finally {
    context.restore();
  }
};
terminal.setScrollbar({ render: squareScrollbar });
```

### Marker hover tooltips

Canvas marker tooltips are enabled by default. They show a custom bookmark's
label or retained command details: shell phase, exit code, decoded `cmdline_url`
when supplied, or raw parameters when no command text was provided. A shell
mark is **not** proof of a particular command line; the default never invents
command text. Labels, decoded text, raw parameters, and errors are rendered as
text, not HTML or navigation links.

Use `tooltip: false` to suppress hover content without hiding marker ticks,
removing the Marks inventory, or disabling marker navigation:

```ts
terminal.setScrollbar({ render: painter, tooltip: false });
```

`tooltip` also accepts a synchronous `TerminalScrollbarTooltipRenderer`:
`(context: TerminalScrollbarTooltipContext) => HTMLElement | null`. Its readonly
context contains:

| Field | Meaning |
| --- | --- |
| `marker` | The retained `TerminalMarker` being hovered. |
| `anchor` | Marker bounds in terminal-local CSS pixels. |
| `layout` | Current `TerminalLayout`. |
| `details` | `TerminalCommandMark` after successful lookup, otherwise `null`. |
| `loading` | Whether retained command details are being fetched. |
| `error` | Detail-fetch failure text, otherwise `null`. |
| `signal` | Aborted when this rendering is replaced or hidden. |

Return an element and the library mounts it in a **light-DOM overlay slot**,
positions it beside the marker, and clamps it inside the terminal. You do not
need to calculate viewport offsets or install mouse listeners. This is a
non-interactive hover preview, not a popover of clickable controls.

`renderDefaultScrollbarTooltip(context): HTMLElement` builds the same safe
content as the default. Decorate it without reimplementing command parsing:

```ts
import {
  renderDefaultScrollbarTooltip, type TerminalScrollbarTooltipRenderer
} from "@hex1b/web-terminal";

const tooltip: TerminalScrollbarTooltipRenderer = context => {
  const element = renderDefaultScrollbarTooltip(context);
  // These --cp-* variables belong to this example's embedding application.
  element.style.background = "var(--cp-surface)";
  element.style.color = "var(--cp-text)";
  element.style.borderLeft = "3px solid var(--cp-accent)";
  const heading = document.createElement("strong");
  heading.textContent = context.marker.source === "custom" ? "Bookmark" : "Shell mark";
  heading.style.display = "block";
  element.prepend(heading);
  return element;
};
terminal.setScrollbar({ render: painter, tooltip });
```

Callbacks run initially (with `loading` for command details), again when details
or an error arrive, and when relevant geometry changes. **Do not make either
the painter or tooltip callback `async`, or return a Promise.** The library
fetches retained details on demand, with the same 8,192 UTF-16-unit bound as
`getCommandMarkDetails`. Oversized, unavailable, or expired details produce an
error state rather than truncated or guessed command text. The callback receives
that state; it does not need to issue its own detail request on every frame.

Tooltips are suppressed during thumb dragging and hidden when the pointer
leaves the marker/terminal, the view disconnects, configuration changes, or the
terminal is disposed. Replacement/hide aborts the rendering's `signal`, so late
work cannot resurrect an old tooltip. For externally owned UI, return `null`
and use that signal to clean up your node. For example, given a host-owned
`inspector` element:

```ts
terminal.setScrollbar({
  tooltip(context) {
    const element = renderDefaultScrollbarTooltip(context);
    inspector.replaceChildren(element);
    context.signal.addEventListener("abort", () => element.remove(), { once: true });
    return null; // Host owns mounting and placement, not the terminal overlay.
  }
});
```

These refinements do not change the external layout, viewport, marker,
navigation, or native-wrapper APIs. `markers: false`, native host chrome, and
`scrollbar: false` do not acquire canvas hover targets.

### Navigation and marker lifetime

`scrollToRow(top)` requests an absolute row, clamped to the current scrollable
range. It does not calculate a relative delta from stale presented state.
`viewport.pending` distinguishes a requested view from a presented one;
`viewport.top`, `liveTop`, `totalRows`, and `rowIds` remain authoritative.
Rejected stale-position requests settle pending state and expose
`viewport.navigationError`; a new navigation request clears the previous error.
At the live end, following resumes. Reading history never claims primary,
and these operations are available in read-only views.

The retained `markers` array contains command and custom points with stable
string `id`, `source`, `buffer`, `row`, and `column`, plus optional shell
`phase`/`exitCode` or host `label`/`color`. A `null` row means unavailable, **not
row zero**. Filter by the current buffer before painting an external rail.
The inventory is not a command-execution event stream. Same-row shell marks
remain distinct; do not infer command starts from an isolated finish marker.
Raw command parameters are fetched on demand by command tooltips or an explicit
`getCommandMarkDetails(id)` call, not included in the marker inventory.
Details exceeding 8,192 UTF-16 units reject explicitly instead of truncating.
Large inventories are paged internally and published only after a coherent
replacement is complete. The accumulated serialized marker index is bounded
to 8 MiB; exceeding that transport budget fails explicitly rather than exposing
a silently incomplete inventory.

```ts
const viewport = terminal.viewport;
if (viewport.available && viewport.rowIds.length) {
  const bookmark = await terminal.addMarker({
    position: {
      generation: viewport.generation,
      rowId: viewport.rowIds[0],
      column: 0
    },
    label: "Review this output"
  });
  // The producer resolves the current anchor, including after retained-text reflow.
  await terminal.scrollToMarker(bookmark.id);
  await terminal.removeMarker(bookmark.id);
}
```

Registration uses a **presented** producer-backed position. It can reject if
that position has expired before acceptance. Catch registration/navigation/
details errors and show them as text. Labels and colors stay in the browser;
labels and shell details are untrusted and must not be inserted as HTML.
Anchors track positions, not immutable search matches: recompute search hits
when their text changes.

Surviving text anchors follow supported producer reflow and horizontal character
insertion/deletion (ICH/DCH). Insertions at a marked cell move the anchor with that
cell; deleting it or pushing it beyond the right margin collects the marker.
Insert-mode typing into a blank marked cursor position binds the marker to the
newly printed text instead. An end-of-row position remains a boundary until the
following glyph wraps onto the next row. Wide-glyph positions follow the leading
cell and expire if editing splits and discards the glyph.

When backing text is evicted, destructively cleared, reset, or discarded by
reflow, its markers and
retained metadata are collected rather than accumulated as unavailable entries.
Navigation to a collected ID rejects rather than guessing. Main and alternate
buffers are isolated: switching away from retained main-buffer content does not
collect its markers; those markers are temporarily unavailable in the other
buffer. Custom markers belong to their owning view and are also released on
removal/disconnect/disposal, not carried into a reconnect. Browser-only labels
and colors are released when their markers leave the authoritative inventory.
The default server quota is 1,000 custom markers per view, configured through
`Hex1bTerminalOptions.CustomMarkerLimit`; zero disables registration and exceeding
the limit rejects explicitly. Collection reclaims custom-marker quota slots.
Shell markers also have a producer-configured count limit.
Late direct HWT1 attachment can see retained producer marks. HMP1 negotiates
**retained text** and **retained OSC 133 command marks** independently, so a late
relay or fresh reconnect can recover both when supported. Raw command details,
phase, exit status, and producer IDs are restored without replaying command events.
Historical graphics and custom/browser-owned markers are not transferred.
Custom markers still belong to their view and do not survive reconnect.

#### HMP1 relay history

An HMP1-backed replica needs its own `WithScrollback(capacity)` configuration,
even when the upstream producer retains history. Local capacity remains
authoritative: requesting more rows never increases it. For example, this server
configuration snippet assumes a connected bidirectional HMP1 `stream` and a
terminal builder named `replicaBuilder`:

```csharp
replicaBuilder
    .WithScrollback(1000)
    .WithHmp1Stream(stream, options =>
    {
        options.ScrollbackHistoryRows = 10_000;
        options.EnableCommandMarkHistory = true;
    });
```

`Hmp1ClientOptions.ScrollbackHistoryRows` defaults to 10,000, accepts 0..100,000,
and uses `0` to opt out. The producer must have scrollback storage and permit
transfer through `Hmp1ServerOptions.EnableScrollbackHistory` (or the direct
`Hmp1PresentationAdapter.EnableScrollbackHistory` property), both defaulting to
`true`. The first builder listener's setting wins for a shared adapter.
The separate `EnableCommandMarkHistory` option defaults to `true` on
`Hmp1ClientOptions`, `Hmp1ServerOptions`, and `Hmp1PresentationAdapter`.
The server setting is also captured from the first builder listener.

This extension does **not** change the HMP1 version. Optional `ClientHello`
history fields request version `1` and a row limit; `Hello` acknowledges them
only when supported and enabled. Missing fields preserve screen-only text replay
with existing HMP1 peers that support the current mandatory `ActivityState`
baseline, not ancient pre-`ActivityState` peers.
Command marks use their own `commandMarkHistoryVersion: 1` field in
`ClientHello` and `Hello`. Missing acknowledgement disables only command-mark
transfer; negotiated text history still works, and vice versa. Without transferred
history, only marks backed by the transferred active screen are eligible.

After each negotiated screen/activity checkpoint, the replica receives the
newest contiguous retained suffix, bounded by the requested/accepted row limit,
32 MiB of row-chunk payloads (including row-length prefixes, not the 8-byte
header), and two million cells. Complete checkpoints are
validated before screen, activity, history, and negotiated command marks are atomically applied, then
graphics and live output resume. Reconnect/resync **replaces**, rather than
appends to, history, avoiding duplication. An available empty checkpoint clears
history; an unavailable checkpoint from a relay with a non-supporting upstream
does not perform an additional history replacement. Explicit history-clearing
operations in the screen replay still have their normal effect.
Main-buffer history also transfers while the alternate
screen is active but stays hidden until returning to the main buffer.

Transferred rows preserve graphemes, continuation cells, soft wraps, padding,
palette colors, and hyperlinks as inert data, never ANSI to execute. Normal
scrolling output afterward accumulates local history as before. If negotiation
is absent or disabled, a new replica still starts without pre-attachment history;
an existing replica retains history according to normal screen/output processing.
A direct producer-backed
HWT1 view reads shared retained history without this transport limit.

When negotiated, `CommandMarkState` follows activity and any text-history rows.
It transfers up to 10,000 newest eligible marks in an 8 MiB frame, preserving raw
OSC 133 parameters (at most 65,536 UTF-8 bytes each), statuses, phases, and IDs.
Positions refer to the accompanying retained text and active screen, not
producer-only row identities. Local command-mark and scrollback capacities
still apply; markers whose backing text was omitted or later redrawn/evicted
are not kept. Main-history marks remain available after returning from the
alternate screen, but marks on the untransferred saved main screen are omitted.

Available checkpoints replace command history rather than append duplicates;
unavailable checkpoints do not additionally replace it. ID high-water state
keeps subsequent live marks aligned even if the latest old record was collected.
No `CommandMarkAdded` events are synthesized for imported records. On close and
reattach, restored retained shell marks support normal inventory, details, hover,
and navigation; custom bookmarks from the closed view are not restored.
Disable `EnableCommandMarkHistory` to retain legacy locally observed command
marks independently of the scrollback setting. See
[CommandMarkState](../../docs/muxer-protocol.md#commandmarkstate-0x10) for the wire contract.

These are transport/retention rules, not differences between canvas and HTML
scrollbars. See the [HMP1 protocol](../../docs/muxer-protocol.md#scrollbackstate-0x0e-and-scrollbackrows-0x0f)
for wire layout, omitted-row counts, bounds, and failure behavior.

### A native HTML scrollbar using only public APIs

Keep a fixed terminal mount **beside** an overflowing rail; never place the
terminal itself inside the spacer. This complete module example uses native
scrolling and separate clickable marker ticks. It caps the spacer below browser
scroll-height limits, then maps the browser's **actual** pixel range to rows.
For a reusable lifecycle-owned version and live mode switching, see
[`NativeScrollbar`](../../samples/WebTerminalDemo/client/native-scrollbar.ts).

```html
<div id="terminal-wrapper" style="display:flex;width:100%;height:480px">
  <div id="terminal" style="flex:1;min-width:0;overflow:hidden"></div>
  <div id="history" style="display:flex;flex:0 0 32px;align-self:flex-start">
    <div id="ticks" style="position:relative;width:12px"></div>
    <div id="rail" tabindex="0" aria-label="Terminal history"
         style="flex:1;min-width:0;overflow-y:scroll;overflow-x:hidden;overscroll-behavior:contain">
      <div id="spacer" aria-hidden="true" style="width:1px"></div>
    </div>
  </div>
</div>
<p id="scroll-status" role="status"></p>
```

```js
import { WebTerminal } from "@hex1b/web-terminal";

const host = document.getElementById("terminal");
const history = document.getElementById("history");
const rail = document.getElementById("rail");
const spacer = document.getElementById("spacer");
const ticks = document.getElementById("ticks");
const status = document.getElementById("scroll-status");
const lifetime = new AbortController();
let terminal, frame = 0, desired, synchronizedTop = 0, markerKey = "";
const report = error => { status.textContent = String(error); };

function update() {
  if (!terminal) return; // Initial notifications can precede mount resolution.
  const { viewport: v, layout: l } = terminal;
  history.style.marginTop = `${l.content.top}px`;
  history.style.height = `${l.content.height}px`;
  history.inert = !terminal.connected || !v.available;
  const maximum = v.available ? v.liveTop : 0;
  spacer.style.height = `${Math.min(8_000_000,
    rail.clientHeight + maximum * l.cellHeight)}px`;
  const range = Math.max(0, rail.scrollHeight - rail.clientHeight);
  if (!v.pending && desired === undefined && !frame)
    rail.scrollTop = maximum ? v.top / maximum * range : 0;
  synchronizedTop = rail.scrollTop;

  const markers = v.available
    ? terminal.markers.filter(m => m.buffer === v.buffer && m.row !== null) : [];
  const nextKey = JSON.stringify([markers, v.totalRows]);
  if (markerKey === nextKey) return;
  markerKey = nextKey;
  ticks.replaceChildren(...markers.map(marker => {
    const tick = document.createElement("button");
    tick.type = "button";
    tick.textContent = "–";
    tick.title = marker.label ?? marker.phase ?? "Bookmark";
    tick.setAttribute("aria-label", tick.title);
    tick.style.cssText = "position:absolute;left:0;padding:0;border:0;" +
      "width:12px;height:6px;line-height:6px;transform:translateY(-50%)";
    tick.style.top = `${marker.row / Math.max(1, v.totalRows - 1) * 100}%`;
    tick.onclick = () => terminal.scrollToMarker(marker.id).catch(report);
    return tick;
  }));
}

rail.addEventListener("scroll", () => {
  if (!terminal?.connected || !terminal.viewport.available ||
      Math.abs(rail.scrollTop - synchronizedTop) < .5) return;
  synchronizedTop = rail.scrollTop;
  const range = rail.scrollHeight - rail.clientHeight;
  desired = range > 0 ? Math.round(rail.scrollTop / range * terminal.viewport.liveTop) : 0;
  if (!frame) frame = requestAnimationFrame(() => {
    frame = 0;
    const target = desired;
    desired = undefined;
    if (terminal.connected && target !== undefined) terminal.scrollToRow(target);
  });
}, { signal: lifetime.signal });

function dispose() {
  lifetime.abort();
  cancelAnimationFrame(frame);
  terminal?.dispose();
  ticks.replaceChildren();
}
window.addEventListener("pagehide", dispose, { signal: lifetime.signal });
try {
  terminal = await WebTerminal.mount(host, {
    url: "/ws/terminal", signal: lifetime.signal, scrollbar: false, padding: 8,
    onLayoutChange: update, onViewportChange: update, onMarkersChange: update,
    onClose: () => { history.inert = true; },
    onStatus: (message, level) => { if (level === "error") report(message); }
  });
  update();
} catch (error) { report(error); dispose(); }
// On SPA component teardown, call dispose() explicitly.
```

Do not synchronize the native thumb to older presentations while navigation is
pending; doing so causes feedback oscillation. Coalesce scroll events to one
absolute request per animation frame. Native scrollbar appearance and visibility
are platform preferences; the spacer does not force a permanently visible OS
thumb. At extreme history sizes scaling trades subpixel precision for a bounded
DOM extent. The separate marker rail and `scrollToMarker` retain precise
producer-backed jumps. The terminal's wheel/key/history controls still work
when no built-in scrollbar is painted.

## Input and clipboard

Import `InputRoute`, `TerminalAction`, and `defaultInputBindings` to inspect and
customize routing. Defaults preserve browser shortcuts, forward terminal keys,
copy with Cmd+C or Ctrl+Shift+C, and use a local right-click to copy or paste
when application mouse capture does not own that gesture. IME composition and
paste are forwarded through the producer's mode-aware input encoder.

Overrides match first. Reuse a default binding's ID to replace it, or specify
`{ id: "clipboard.context-click", remove: true }` to remove that default.
`match`, `when`, and `onInput` must finish synchronously. Actions may be async.

```ts
import { InputRoute, TerminalAction, type WebTerminalOptions } from "@hex1b/web-terminal";

const options: WebTerminalOptions = {
  url: "/ws/terminal",
  inputBindings: [
    {
      id: "history.previous-page",
      match: input => input.type === "key" && input.key === "PageUp" && input.shift,
      action: TerminalAction.ScrollLines,
      args: -20
    },
    { id: "clipboard.context-click", remove: true }
  ],
  onInput(input) {
    if (input.type === "key" && input.meta) return InputRoute.Browser;
    return InputRoute.Continue;
  }
};
```

Named actions are `copySelection`, `pasteClipboard`, `copyOrPaste`,
`clearSelection`, `scrollToLive`, and `scrollLines`.
`terminal.runAction(TerminalAction.ScrollLines, -20)` shares the same
implementation as bindings and UI controls. Custom `actions` receive
`(context, args, input)`; their argument/result types are `unknown`, so custom
handlers validate their own data. Built-in action names cannot be overridden.

You can also call `scrollLines()`, `scrollToLive()`, `clearSelection()`,
`copySelection({ clear: true })`, `paste(text)`, or `pasteClipboard()` directly.
Copy uses authoritative producer selection text, not rendered cells. Clipboard
actions reject if selection/input/focus changes before their asynchronous work
can be applied safely. Errors are surfaced rather than silently reported as
successful copies or pastes.

### Hyperlinks

Hold Ctrl or Cmd and click an OSC 8 hyperlink to open its destination in a new
tab. Hovering shows the destination and activation hint; holding the modifier
also shows a pointer cursor. Links work in live output, scrollback, and read-only
views. Plain clicks and drags retain their existing selection/application
behavior, and explicit input-policy routes or actions take precedence.
Shift and Alt/Option continue to reserve selection gestures.

Only absolute `http:`, `https:`, and `mailto:` destinations are activated
(`mailto:` handling depends on the browser). New tabs use `noopener,noreferrer`.
Script, data, file, relative, and custom-scheme URLs are not activated.
This is the default when `links` is omitted: text detection is off and legacy
allowlisted OSC 8 navigation is preserved.

### Opt-in text links and host actions

Detection is browser-local and per view. It does not create server hyperlinks,
emit OSC/SGR, change copied text, or modify HWT1. Detected text **never opens a
browser, application, or file automatically**. Register actions at mount time,
even if detection starts disabled; `setLinks` does not register actions.

This example creates a plain-text preview, not a navigation or file-access UI:

```ts
import { WebTerminal, linkAction, type TerminalLinkOptions } from "@hex1b/web-terminal";

const container = document.createElement("div");
container.style.cssText = "width:800px;height:480px";
const preview = document.createElement("pre");
document.body.append(container, preview);

const links: TerminalLinkOptions = {
  osc8: { action: "previewUri" }, // Optional: replace legacy navigation as well.
  detection: {
    activation: "modifierClick",
    decoration: "always",
    underlineStyle: "solid",
    rules: [
      { id: "web", builtin: "url", action: "previewUri" },
      { id: "files", builtin: "absolutePath", action: "remoteFile" },
      { id: "home", builtin: "homePath", action: "remoteFile" },
      { id: "uris", builtin: "uri", action: "previewUri" },
      {
        id: "issues", pattern: /\bPROJ-(?<number>\d+)\b/gu,
        kind: "custom", text: "logicalLine", action: "issue",
        resolve(match) {
          const number = match.groups.number;
          return number ? { target: number, data: { label: match.text } } : null;
        }
      }
    ]
  }
};

const terminal = await WebTerminal.mount(container, {
  url: "/ws/terminal",
  links,
  actions: {
    previewUri: linkAction((_context, activation, _input) => {
      preview.textContent = `URI preview: ${activation.target}`;
    }),
    remoteFile: linkAction((context, activation) => {
      preview.textContent = `Remote path: ${activation.target}\n` +
        `Remote cwd: ${context.terminal.workingDirectory.path ?? "unknown"}`;
    }),
    issue: linkAction((_context, activation) => {
      preview.textContent = `Issue ${activation.target}: ${activation.text}`;
    })
  },
  onLinkDetectionError(error) {
    console.warn(error.code, error.ruleId, error.revision, error.message);
  },
  onStatus(message, level) {
    console.log(level, message);
  }
});

// Replace the entire configuration, disabling only the "home" rule.
if (links.detection) {
  terminal.setLinks({
    ...links,
    detection: {
      ...links.detection,
      rules: links.detection.rules.map(rule =>
        rule.id === "home" ? { ...rule, enabled: false } : rule)
    }
  });
}
terminal.setLinks({ detection: false }); // Reset to legacy OSC 8, no detection.
terminal.setLinks(false);               // Disable every local link interaction.
terminal.setLinks(links);               // Re-enable the original configuration.
```

`setLinks(options)` replaces, rather than merges, the complete configuration.
Omitted fields reset to defaults. Validation occurs before replacing active
state; malformed rules, duplicate IDs, unsupported regex flags, and missing
named actions throw. `enabled: false` disables an individual rule.
`osc8: false` disables OSC 8 interactions while allowing configured detection;
`osc8: { action }` delegates OSC 8 activation to a consumer action. An omitted
`osc8` keeps legacy allowlisted navigation, even when detection is configured.

Actions use the existing `actions` registry, not an `onLinkClick` callback.
`linkAction((context, activation, input) => unknown)` returns an
`InputActionHandler` that validates and types its activation argument.
`context` is `TerminalInputContext`; `input` is a readonly `TerminalInput` or
`undefined`. Async action completion uses the existing dispatcher. A rule or
OSC 8 action can also be an inline handler. Built-in terminal action names
cannot serve as link actions; use a registered custom action or callback.

`TerminalLinkActivation` contains `source` (`"detected"` or `"osc8"`), `ruleId`
(`null` for OSC 8), `kind` (`"uri"`, `"path"`, or `"custom"`), matched `text`,
resolved `target`, visible end-exclusive cell `ranges`, presented `revision`,
and optional consumer `data`. Core activation fields and ranges are frozen;
consumer-owned `data` is not deep-frozen. Targets, OSC 8 destinations, and data remain untrusted: authorize any
navigation or remote operation in your application and display text with
`textContent`, not `innerHTML`. A custom OSC 8 action can receive schemes outside
the legacy allowlist; this is not permission to open them.

#### Rules, text modes, and resolution

Rules compete in array order; the first accepted match owns its cells. Presets
recognize HTTP/HTTPS URLs (`url`), general including opaque URIs (`uri`), POSIX
and Windows drive-absolute paths (`absolutePath`), and literal `~/...`
(`homePath`). Paths are lexical, whitespace-delimited **remote terminal paths**,
not browser-local files. There is no `~` expansion, percent decoding, existence
check, home-directory inference, or resolution against the page URL. Use custom
rules for quoted/spaced paths, UNC paths, or `file:line:column` suffix grammars.

Custom rules supply `pattern: RegExp`, `kind`, and an action. `text` applies to
built-ins and custom rules:

| `text` | Match input |
| --- | --- |
| `"logicalLine"` (default) | Displayed rows joined only across authoritative soft wraps. |
| `"physicalRow"` | Each displayed physical row independently. |
| `"viewport"` | Displayed text with soft wraps joined and hard breaks retained as `\n`. |

An optional synchronous `resolve(match)` returns `null` to reject a match, or
`{ target, action?, data? }` to transform its destination, override the action,
and attach local data. Without a resolver, `target` is the recognized text.
`match` exposes matched `text`, UTF-16 `index`, `captures` (excluding the full
match), named `groups`, and `chunk: { text, mode, start, end }`. Boundary values
are `"complete"`, `"clipped"`, or `"unknown"`. The full regex match determines
the highlight; a resolver cannot replace its range. Regexes scan all matches,
with or without `g`, without changing the caller's `lastIndex`; sticky `y` is
unsupported. Empty matches have no clickable cells.

#### Visible-only limits and styling

Only the currently displayed text is scanned, including history **while it is
displayed**. There is no off-screen fetch, unseen-history scan, independent
reflow, or reconstruction of missing text. HWT1 already carries the soft-wrap
flag but does not carry wide-wrap padding markers. Such blanks must remain
spaces, so some Unicode targets wrapped at a wide glyph will not match.
Candidates depending on uncertain/clipped edges or unavailable continuations
are not active. Complete visible delimiters are important; false negatives
are intentional rather than activating truncated destinations.

Cell mapping respects wide/combining characters and rejects partial-grapheme
matches. Hidden cells and graphics placeholders are barriers, not text to
silently remove. Authoritative OSC 8 spans reserve cells **even when disabled
or blocked**; an overlapping detected candidate is rejected in full.

Underline visibility and appearance are independent:

| Option | Values | Default |
|---|---|---|
| `decoration` | `"always"`, `"hover"`, `"none"` | `"always"` |
| `underlineStyle` | `"solid"`, `"dashed"` | `"solid"` |

For example, use `decoration: "hover", underlineStyle: "dashed"` for dashed
underlines only while the pointer is over a detected link. Hover decorates
the whole match, including its visible wrapped spans; no modifier key is needed
to reveal it. `"none"` retains hit-testing/activation without inferred underlines.
Change either option at runtime by passing the updated configuration to `setLinks`.
Decorations are local. Existing SGR underline style and color are preserved;
disabling links cannot erase application-authored underlines.

`activation` defaults to `"modifierClick"` (Ctrl/Cmd+click); `"click"` is an
explicit alternative that takes ownership only on a link. Input policy retains
first refusal. Activation occurs on release, is canceled by dragging or stale
content/configuration, and does not forward the consumed gesture to the
workload. Read-only views may still invoke local link actions.

#### Detection isolation and deployment

Regex scanning uses a separate, lazily created detection module worker so a
pathological regex cannot stall the rendering worker. Work is bounded by
internal text, rule, match, and time budgets; oversized work is diagnosed, not
silently presented as complete. These are implementation limits, not public
scheduling options. Disabling detection or disposing the view releases its
detection worker.

Current internal limits (not benchmark-derived performance guarantees):

| Budget | Limit |
| --- | --- |
| Configured rules | 32 |
| Regex source length | 8,192 UTF-16 code units |
| One text chunk | 65,536 UTF-16 code units |
| Total scan text | 262,144 UTF-16 code units |
| Mapped cells | 262,144 |
| Matches | 2,048 per rule and 2,048 visible resolved matches |
| Estimated result payload | 262,144 budget units (bounds capture amplification) |
| Cache entries / estimated retained payload | 2,048 entries / 1,048,576 budget units, including keys and all matched/captured/group text |
| Per-rule timeout | 250 ms, including worker startup |

Payload budgets count UTF-16 text units plus estimated overhead (16 units per
match and 8 per capture/group), including empty captures. They bound estimated
payload size, not exact JavaScript heap bytes.

The cell budget does not override the chunk budget: oversized logical-line or
viewport chunks are rejected, not split into apparently complete targets.

The first displayed row's start is treated as unknown, as are full right
edges. A visible delimiter is required when a candidate would otherwise
depend on an uncertain edge. Budget diagnostics remain feature-local; a regex
timeout disables its rule until `setLinks` reconfigures detection.

**Resolvers are trusted synchronous main-thread JavaScript and cannot be
preempted by the regex watchdog.** Keep them fast, side-effect-free, and
nonblocking; they can run repeatedly during detection. Promises are invalid.
Timeouts and resolver failures disable the affected rule until reconfiguration.
`onLinkDetectionError` receives
`{ code: "timeout" | "limit" | "resolver" | "worker", ruleId: string | null, revision, message }`;
errors also use `onStatus`. Detection failure leaves the terminal running.
Activation failures instead use existing `onInputError`/status handling and
never fall back to navigation.

The link-detection worker is included in the same `index.js` bundle as the
terminal worker. If your deployment requires explicit worker URLs:

```ts
const terminal = await WebTerminal.mount(container, {
  url: "/ws/terminal",
  workerUrl: "/web-terminal/index.js#hex1b-terminal-worker",
  linkDetectionWorkerUrl: "/web-terminal/index.js#hex1b-link-detection-worker",
  links: { detection: false }
});
```

`linkDetectionWorkerUrl` accepts `string | URL`, resolves relative strings
against the page like `workerUrl`, and is a mount-time override. Without it the
entry uses the actual bundle URL with the link-detection fragment, preserving its
path, filename, and query. Worker origin/CSP restrictions still apply. For app
rebundling and fonts, see [deployment](#module-and-worker-deployment).
Rules and matched text are not sent to an external service.
See the [opt-in demo](../../samples/WebTerminalDemo/README.md#try-local-link-previews).

## Selection UI hooks

`onSelectionUI` receives a typed `SelectionUIEvent`, also dispatched as the
`selectionui` DOM event on `terminal.element`. Its frozen detail includes
selection, viewport, geometry, canvas size, connection/read-only state, and:

- `overlay`: a stable light-DOM host for custom UI; style your controls and set
  `pointer-events: auto` on interactive descendants.
- `rects`: selection rectangles in overlay-local CSS pixels.
- `runAction`: the same typed action API as the terminal handle.
- `signal`: cleanup lifetime, aborted on disposal.

Call `event.preventDefault()` **synchronously** to replace the default Copy
button. This does not remove selection highlights or transfer ownership of
selection/clipboard state. The callback must return `undefined`, not a Promise.
Events coalesce meaningful changes; `refreshSelectionUI()` re-notifies hosts
after external styling or policy changes. External DOM listeners may also
cancel the default UI.

Inspection UI inherits the embedding page's `--cp-*` theme tokens and otherwise
uses its own light/dark defaults. Shadow parts include `selection-highlights`,
`selection-highlight`, and `selection-copy-button`. The first two retain
transparent geometry for host adornments; use the terminal palette to control
the rendered selection colors rather than CSS background/opacity on those parts.

## Fonts and licenses

The default is the bundled **Cascadia Mono NF** variable WOFF2 font. The package
includes the unmodified font, SIL Open Font License, and provenance in
`dist/fonts/cascadia-mono-nf/`; no sample assets are required.

```ts
const font = {
  family: "My Terminal Font",
  faces: [{ url: "/fonts/my-terminal.woff2", weight: "100 900", style: "normal" }]
};
```

Custom face URLs resolve against the host page before the configuration reaches
the worker. Page-loaded fonts are not inherited by workers: supply face URLs,
a locally installed family, or a generic family such as `monospace`. A generic
family cannot have downloadable faces. Font loading failures reject rather
than silently selecting a different font.

Hex1b's code is MIT licensed (`LICENSE`); the bundled font uses its separate
SIL Open Font License.

## Build, test, and pack

From this package directory, with Node.js 22 or later:

```sh
npm ci
npm run build
npm test
npm pack
```

The strict TypeScript build emits private modules into ignored `.build/`;
esbuild bundles the public API and both workers into `dist/index.js`.
`dist/` contains that single JavaScript file, `index.js.map`, declarations,
declaration maps, and font/license/provenance assets. `.build/` is not shipped.
Run `npm run build` **before** `npm test`: private unit tests import `.build/`,
while packaging checks exercise the actual `dist/` bundle. Tests use
zero-dependency `node:test`, plus strict public-consumer declaration checks in
NodeNext and bundler modes.
`npm run typecheck` validates sources without emitting.

`prepack` rebuilds for `npm pack` and manual `npm publish` from this directory.
Only `dist/`, this README, the MIT license, and package metadata are shipped.
A prepared tarball is self-contained and can be published with
`npm publish ./hex1b-web-terminal-<version>.tgz --ignore-scripts`; it does not
need development sources or build scripts. The package name is always
`@hex1b/web-terminal`. CI publishes main/release builds to npmjs; PR builds
provide the tarball as the `npm-web-terminal` workflow artifact and do not
publish it to a registry. No registry is pinned in `package.json`.
