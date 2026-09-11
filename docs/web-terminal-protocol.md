# Hex1b Web Terminal Protocol (HWT1): internal implementation notes

Status: **internal, evolving state-transfer protocol**, describing the
implementation as of 2026-09-08.

HWT1 is an implementation detail shared by Hex1b's server and first-party browser
client, **not a third-party integration contract**. Independent frontend
implementations are not currently supported or recommended; do not implement a
third-party client against these notes.

The server and client evolve together. Wire layouts, messages, and semantics may
change without backward-compatibility or deprecation guarantees. Keep both ends
in sync and upgrade them together. The `HWT1` name, magic, and current `version: 1`
field do not promise compatibility across revisions or releases.

This document is for contributors maintaining the paired implementations. A
future decision may recommend HWT1 for other frontends, but that would require
an explicit support and compatibility policy; it is not a commitment today.
Neither the protocol nor the public experimental `Hwt1PresentationAdapter` API
has a stability or production-security guarantee.

HWT1 presents the authoritative visible state of a `Hex1bTerminal`. The server
interprets workload output, terminal modes, Unicode cell ownership, and graphics.
The client receives positioned cells and image resources; it does **not** interpret
an ANSI output stream or run a second terminal emulator. HWT1 is **not a WebGPU
command protocol**. WebGPU, glyph atlases, fonts, device-pixel ratio, and DOM
interaction are choices of the first-party reference browser client.

This document records the existing messages, their state transitions, and the
current implementation profile. **MUST** and **MUST NOT** describe consistency
requirements for the paired implementations at this revision, not a frozen
external standard. Sections explicitly labeled *reference behavior* or *host
binding* describe implementation policy rather than additional wire messages.
Decoder tolerances are documented separately from what the server emits.

## 1. Implementation and ownership

| Source | Responsibility |
|---|---|
| [`Hwt1PresentationAdapter`](../src/Hex1b/Hwt1/Hwt1PresentationAdapter.cs) | Public, experimental adapter: snapshot scheduling, frame reads, acknowledgement gating, resync, resize, and client-message handling. |
| [`Hwt1RenderProjection`](../src/Hex1b/Hwt1/Hwt1RenderProjection.cs) | Internal projection, cell deltas, image cache, binary serialization. |
| [`Hwt1FrameMetadata`](../src/Hex1b/Hwt1/Hwt1FrameMetadata.cs), [`Hwt1RenderCell`](../src/Hex1b/Hwt1/Hwt1RenderCell.cs), [`Hwt1RenderImage`](../src/Hex1b/Hwt1/Hwt1RenderImage.cs), [`Hwt1RenderPlacement`](../src/Hex1b/Hwt1/Hwt1RenderPlacement.cs) | Internal wire data definitions. |
| [`Hwt1Input`](../src/Hex1b/Hwt1/Hwt1Input.cs), [`TerminalInputEncoder`](../src/Hex1b/Input/TerminalInputEncoder.cs) | Client-input validation and encoding against live server modes. |
| [`protocol.ts`](../src/web-terminal/src/protocol.ts) | Transport- and GPU-independent frame decoder; internal to the npm package. |
| [`terminal-worker.ts`](../src/web-terminal/src/terminal-worker.ts), [`renderer.ts`](../src/web-terminal/src/renderer.ts) | Reference receiver state machine, resource transactions, presentation, and acknowledgement policy. |
| [`web-terminal.ts`](../src/web-terminal/src/web-terminal.ts), [`mouse-input.ts`](../src/web-terminal/src/mouse-input.ts) | Element-local mounting, fitting, role callbacks, and browser event capture/filtering. |
| [`main.ts`](../samples/WebTerminalDemo/client/main.ts) | Floating-window playground; not a dependency of the mounted client. |
| [`BrowserSession`](../samples/WebTerminalDemo/BrowserSession.cs), [`Program.cs`](../samples/WebTerminalDemo/Program.cs) | Sample-only ASP.NET/WebSocket hosting and workload controls. |

The library has no ASP.NET or WebSocket dependency for HWT1. A host attaches one
adapter to one terminal and one ordered, message-preserving connection. It sends
each `ReadFrameAsync(CancellationToken)` result intact and delivers complete
client JSON messages serially to
`HandleMessageAsync(ReadOnlyMemory<byte> utf8Json, CancellationToken)`. The receive
loop MUST remain independent of the frame-read loop so acknowledgements can
release the sender. Only one frame reader is supported.

`ReadFrameAsync` returns `ValueTask<ReadOnlyMemory<byte>>`; `HandleMessageAsync`
returns `Task`. Attachment marks the initial state dirty, so the first baseline
can be read after attachment without waiting for the terminal run loop to start.

The adapter owns projection and mode-aware input; the host owns transport,
authentication, authorization, workload selection, and terminal lifetime. The
sample consumes the public adapter without friend-assembly access.
The adapter is public to enable hosting the first-party client; its accessibility
does not turn the underlying wire format into a supported extension point.

In the mounted multi-head sample, each WebSocket/view reads **shared producer
state** through a per-view HWT1 adapter. `Hmp1PresentationAdapter` registers
the view as a peer and remains the sole primary/resize authority. The browser
does not need a server-side mirror terminal, HMP1 replay, or an external HMP1
network endpoint to inspect the producer's existing history.

HMP1's [graphics replay and animation checkpoint](muxer-protocol.md#kgpanimationstate-0x0c)
restore native HMP1 mirrors before normal live output. Shared-source browser
views instead observe the producer's current animation state directly. The
browser receives resolved HWT1 cells, images, and placements, not KGP animation
commands or HMP1 control frames.

Each view has independent HWT1 revisions, acknowledgements, viewport, selection,
image retention, worker, and GPU caches. Removing mirror models does not remove
per-view snapshot/projection work or establish a shared change-feed cache. See
the [design discussion](web-terminal.md).

The host does not need role-election or resize-binding callbacks. Viewport
navigation is independent of producer geometry and never requests primary.

## 2. Message model and conventions

There are two directions:

* **Server → client:** one binary state frame, defined below.
* **Client → server:** one UTF-8 JSON object with a case-sensitive `type`, one of
  `ack`, `resync`, `resize`, `requestPrimary`, `input`, `paste`, `key`, `mouse`,
  `viewport`, `selection`, or `copy`.

There is no hello, capability exchange, negotiated version, retained-session
handshake, heartbeat, or close message inside HWT1. Viewport/selection/copy
requests have correlated identifiers and results inside ordinary state metadata,
not a separate response channel. HMP1 peer IDs are reported in frame metadata, not allocated through
an HWT1 session handshake. Initial dimensions are supplied by the host/upstream
producer, not negotiated with the browser.
The first server frame is a full baseline.

All binary integers are unsigned, fixed-width, **little-endian**; lengths count
bytes, not Unicode characters. JSON numbers use ordinary JSON numeric notation,
not binary integer encoding. JSON names and enum strings are case-sensitive.
JSON metadata and cell text use UTF-8 without a byte-order mark. Metadata key
order and JSON escaping are not significant; image-array order is significant.
There is no padding, alignment, compression wrapper, checksum, or trailing
extension block. PNG payloads retain PNG's own internal encoding.

Unless stated otherwise:

* Cell coordinates are zero-based, origin at the upper-left, positive right/down.
* Logical pixel coordinates are independent of browser backing scale.
* Local resize/primary requests use **20..300 columns, 10..100 rows**, with cells
  **10 logical pixels wide × 20 logical pixels high**. These request bounds are
  not a promise that upstream HMP1 geometry is always clamped to that range.
  A native HMP1 primary can establish a larger grid; receiver limits are in §8.
* Mounted surfaces preserve the authoritative grid's aspect ratio when
  contain-fitting a host-sized element. Only a confirmed primary's outer
  `ResizeObserver` can request a grid change; secondary resizing is local CSS
  fitting. Primary CSS scale caps at 1 but may shrink; secondary fit can enlarge
  or shrink. Configured raster scale and CSS fit are separate quantities.
* A client MUST use the server's grid and ownership; it MUST NOT reflow text,
  recompute cell advances, or infer terminal scrolling from local layout.

## 3. Binary server frame

Let `M` be the metadata byte length, `C` the changed-cell count, and `T[i]` the
UTF-8 text byte length of cell record `i`.

| Absolute byte offset | Size | Contents |
|---|---:|---|
| `0` | 4 | Magic: ASCII `HWT1`, bytes `48 57 54 31`; read as `u32` this is `0x31545748`. |
| `4` | 4 | `M`, metadata length (`u32`). |
| `8` | `M` | UTF-8 JSON metadata object. |
| `8 + M` | 4 | `C`, changed-cell count (`u32`). |
| `12 + M` | Sum of `22 + T[i]` | Exactly `C` variable-length cell records. |
| Immediately after the cells | Sum of `images[i].byteLength` | Image payloads, concatenated in metadata `images` order. |

The total frame length MUST equal
`12 + M + sum(22 + T[i]) + sum(images[i].byteLength)`. No terminator follows the
last text or image. Bounds MUST be checked before reading each field.

### 3.1. Metadata

The current producer emits every field in this table, including empty arrays.
Fields describe the **complete new state** except `images`, which contains only
resources supplied in this frame, and the binary cell section, which can be a
delta.

| Field | JSON type | Meaning |
|---|---|---|
| `version` | integer | Exactly `1`, in addition to the `HWT1` magic. |
| `revision` | integer | This frame's revision; producer uses `uint32`, starting at 1. |
| `baseRevision` | integer | `0` for a full frame; otherwise the previous projected revision. |
| `full` | boolean | Whether cells and image resources establish a self-contained baseline. |
| `columns`, `rows` | integers | Dimensions of the visible grid, in cells. |
| `cellWidth`, `cellHeight` | integers | Logical pixels per cell; currently exactly `10`, `20`. |
| `mouseTracking` | integer | Effective mouse tracking mode: `0`, `9`, `1000`, `1002`, or `1003`; see §7. |
| `peer` | object | Required complete HMP1 peer/primary state, or standalone defaults, defined below. |
| `title` | string | Required complete current workload window title on every full/delta frame; `""` means unset or explicitly cleared. At most 4,096 UTF-16 code units, no C0/DEL/C1 controls or unpaired surrogates. |
| `progress` | object | Required complete OSC 9;4 state: `state` and nullable `percentage`, defined below. |
| `shellIntegration` | object | Required complete OSC 133 state: `phase` and nullable `lastExitCode`, defined below. |
| `history` | object or null | Complete per-view text viewport, selection, and copy state, defined below. Null denotes a projection without history interaction metadata. |
| `hyperlinks` | array of objects | Complete OSC 8 destination ranges for the presented viewport, including on cell-delta frames. |
| `defaultBackground`, `defaultForeground` | integers | Resolved packed colors, using §3.3; producer emits opaque colors. |
| `cursor` | object | Complete cursor state, defined below. |
| `images` | array of objects | New/replacement resource descriptors, each with one corresponding binary payload. |
| `retainedImages` | array of strings | Complete image-key set the client must retain after applying this frame. |
| `placements` | array of objects | Complete ordered placement list; not placement deltas. |
| `stats` | object | Server counters and durations, defined below. |
| `warnings` | array of strings | Human-readable diagnostics; not machine-readable error codes. |

Title state is captured atomically with the screen and is not a stream of OSC
events. Title-only changes may produce zero-cell deltas and obey the normal
acknowledgement gate and synchronized-output boundary. Intermediate changes may
coalesce. Historical viewports carry the current workload title, not a title
associated with an old row.

The core normalizes title values before storage: it strips C0 (`U+0000..U+001F`),
DEL (`U+007F`), and C1 (`U+0080..U+009F`), replaces malformed surrogates with
U+FFFD, and truncates to 4,096 UTF-16 code units at a Unicode scalar boundary.
The browser validates this contract rather than renormalizing it. A missing,
non-string, oversized, control-bearing, or malformed-surrogate title is fatal;
there is no mixed-version fallback to an empty title.

OSC 0 and OSC 2 set or explicitly clear the window title; OSC 1 affects only
the icon name. Title payloads preserve semicolons and support BEL or ST (`ESC \`)
termination. Unicode C1 OSC/ST (`U+009D`/`U+009C`) are accepted through the UTF-8
input path, not a new legacy-byte encoding. Existing OSC 22/23 saved-title
extensions use the same authoritative state; HMP1 replays saved and current
values before pending parser continuation and subsequent live bytes.
The per-title bound does not cap parser buffering or saved-stack depth.
Oversized saved-state replay fails the HMP1 16 MiB StateSync budget explicitly.
RIS, soft reset, screen clearing, and alternate-buffer switches
preserve title state and existing saved-title semantics.

**Reference browser behavior:** the worker forwards title in its existing
geometry message only after an accepted frame completes presentation. The
read-only handle `title` is updated before `onTitleChange(title)`. The initial
authoritative value, including `""`, notifies once before mount resolves; later
notifications require a distinct presented value. Preliminary relay frames with
null peer IDs and `isPrimary:false` do not establish the initial title. The relay
publishes connected peer state only after authoritative StateSync is applied.
Discarded/invalid frames, same-title resyncs, blink redraws, and stats do not
notify. Disconnect/disposal retain the last known title without a synthetic
clear; disposal/abort stops callbacks. A new mount gets its own initial value.
There is no new subscription API, DOM event, or title-specific wire message.

Titles remain untrusted workload text, including literal markup and bidi text.
The component never changes `document.title`, the accessible label, or host
headers automatically. Hosts must use text rendering such as
`header.textContent = title || fallback` and apply their own presentation policy.

**Activity state:** `progress` is `{ "state": "none", "percentage": null }`
initially. State is one of `none`, `normal`, `error`, `indeterminate`, `warning`.
The percentage must be null for none/indeterminate and an integer 0-100 for
the other states. `shellIntegration` initially contains
`{ "phase": "unknown", "lastExitCode": null }`. Phase is one of `unknown`,
`prompt`, `commandLine`, `executing`, `finished`. Last exit code is null or a
signed 32-bit integer; Unknown requires null. Null does not imply success.
All properties are required, including explicit nulls.

Both objects are captured atomically with the screen, sent on every full and
delta frame, and remain current when inspecting historical rows. Metadata-only
output can emit a zero-cell delta. They use existing acknowledgement and
synchronized-output coalescing; they do not retain semantic boundary positions
or transport an event log. The browser validates fields before presenting,
updates both getters, then invokes `onProgressChange` and
`onShellIntegrationChange` for the initial state and distinct presented changes.
Missing/malformed fields are fatal. Unchanged resyncs, discarded frames, and
disposal do not notify.

HMP1 restores these values from its structured activity checkpoint as part of
the StateSync transaction, before the replica is available to browser capture.
It does not reconstruct shell phase by sending synthetic OSC 133 markers.
See [the muxer protocol](muxer-protocol.md) for ordering. Live OSC sequences
still pass through the core parser. RIS clears activity; soft resets, screen
clears, resize, and buffer switches preserve it. Disconnect/exit retain the
last reported values, not an invented completion. Hosts should combine these
values with connection status. The
[public client contract](../src/web-terminal/README.md#application-progress-and-shell-activity)
describes callback semantics and the supported OSC argument forms.

`cursor` has exactly these currently emitted fields:

| Field | Type | Meaning |
|---|---|---|
| `x`, `y` | integers | Zero-based column and row. A cursor outside the current grid is not drawn. |
| `visible` | boolean | Whether the cursor is enabled. |
| `shape` | integer | `0` Default, `1` BlinkingBlock, `2` SteadyBlock, `3` BlinkingUnderline, `4` SteadyUnderline, `5` BlinkingBar, `6` SteadyBar. |

The reference renderer treats Default as a blinking block. It uses a local
600 ms on/off phase, draws the cursor last, and uses the cursor cell's foreground
color. Its block has 0.55 alpha; underline and bar are two logical pixels thick.
These raster details and blink phase are not transmitted. The numeric shape
values follow [`CursorShape`](../src/Hex1b/CursorShape.cs).

Each `hyperlinks` entry contains `row`, `startColumn`, `endColumn`, and `uri`.
Coordinates are zero-based viewport cells, with an exclusive `endColumn`.
Ranges are nonempty, sorted by row then column, nonoverlapping, and bounded by
the current grid. The array is bounded by the total cell count. Contiguous cells
with the same destination are combined within each row, including wide-cell
continuations. Wrapped links have one range per row. Hidden cells are excluded.
OSC 8 parameters are not transmitted.

These ranges replace the previous frame's entire hyperlink map. An empty array
clears all links; changing only a destination need not resend any binary cells.
Historical ranges come from the same authoritative snapshot as displayed text.
URI strings are untrusted terminal output. The browser permits activation only
of absolute HTTP, HTTPS, or mailto URLs without raw ASCII whitespace/control
characters; it does not resolve page-relative paths or detect URLs in plain text.
Hyperlinks share the 8 MiB serialized metadata bound, which the producer enforces.

`peer` contains:

| Field | Type | Meaning |
|---|---|---|
| `id` | string or null | This view's upstream HMP1 peer ID, not its HTTP instance ID. |
| `primaryId` | string or null | HMP1's current primary peer ID; null when unassigned. |
| `isPrimary` | boolean | Whether this peer currently owns resize authority. |

A standalone adapter reports
`{"id":null,"primaryId":null,"isPrimary":true}`. An HMP1-backed view not yet
connected reports null IDs and `isPrimary:false`. A connected secondary can
have a non-null `id` and null `primaryId`; that does **not** implicitly make it
primary. For non-null `id`, `isPrimary` must equal `id === primaryId`.
On HMP1 disconnection, the previously assigned `id` is retained while
`primaryId` becomes null and `isPrimary` becomes false. Peer fields come from one
immutable applied-state snapshot; nullable fields are explicitly serialized.
Peer IDs mirror HMP1 and are opaque to the browser.

Role-only changes can produce zero-cell deltas and obey the same acknowledgement
gate as any other state. There is no separate role-change WebSocket message.
The mounted client's `onRoleChange(peer)` callback is a local notification
derived from this metadata, not an additional wire event.

`history` contains the complete per-view inspection state:

| Field | Type | Meaning |
|---|---|---|
| `generation` | decimal string | Positive signed-64-bit generation, encoded as text to preserve identity in JavaScript. |
| `buffer` | string | `main` or `alternate`; normal history is not appended from alternate-screen redraws. |
| `totalRows` | integer | Retained history plus current screen rows for this buffer. |
| `top`, `liveTop` | integers | Viewport top and current screen top in the virtual buffer. `liveTop = totalRows - rows`. |
| `following` | boolean | Whether this view follows live output. If true, `top` equals `liveTop`. |
| `rowIds` | array of decimal strings | One unique positive signed-64-bit identity per displayed row, ordered top to bottom. |
| `requestId` | integer | Last applied inspection request identifier. |
| `selection` | object | Complete selection result, described below. |
| `copy` | object or null | Most recent correlated copy result, or null. |

`selection` contains `requestId`, `status` (`none`, `valid`, or `invalidated`),
`mode` (`character`, `word`, `line`, or `rectangle`), `ranges`, and `text`.
Each highlight range is `{row,startColumn,endColumn}` in viewport coordinates;
`endColumn` is exclusive. Ranges are ordered by row and confined to the visible
grid. They do not enumerate unloaded selected rows.
`text` is the authoritative extraction across the selected range, including
offscreen rows, or null when the selection is not valid. Inactive selections
have no highlight ranges. A valid selection can have no visible ranges when
all of its text is offscreen.

`copy`, when present, contains `requestId`, `status` with the same three values,
and `text` or null. It is a text result, not an instruction to write the system
clipboard. Only a matching, user-initiated browser copy operation may consume it.
The browser must reject obsolete results after selection or generation changes.

Ordinary full-screen scrolling transfers live row identities into retained
history. Viewports and selections stay anchored while new output arrives.
Losing any selected row invalidates the complete selection instead of returning
partial text. Resize/reflow, reset, history clear, and alternate-screen
transitions conservatively invalidate generation-based anchors. Live edits
must not silently retarget selected text to unrelated rows.

Cell records in a history frame describe this view's viewport, not necessarily
the live screen. Producer dimensions and live input modes remain authoritative.
When not following live output, the cursor and historical graphics are not
drawn; this milestone exposes text history only. Returning to live restores
ordinary resolved graphics. History and selection metadata obey the same
one-frame-in-flight acknowledgement gate as cells.

`stats` has four fields:

| Field | Type | Meaning |
|---|---|---|
| `workloadBytes` | nonnegative number | Cumulative bytes from nonempty workload reads of the source terminal, counted by `terminal.OutputBytesRead`, including the pre-tokenized path. Shared-source browser views report the same producer counter, not per-view HMP1 replay traffic. Emitted from a .NET `long`. |
| `outputBatches` | nonnegative number | Cumulative state-applied output notifications received by the adapter, not the number of frames. Emitted from a .NET `long`. |
| `elapsedMs` | nonnegative number | Milliseconds since adapter construction. |
| `captureMs` | nonnegative number | Snapshot duration plus projection time measured up to metadata construction; excludes subsequent JSON/binary serialization, network delivery, and client/GPU work. |

Durations can be fractional. Counters are diagnostic: JavaScript can lose
integer precision above `Number.MAX_SAFE_INTEGER`. Browser statistics such as
FPS, glyph count, GPU memory, and presentation count are local metrics, **not**
additional metadata fields.

With a direct workload, `workloadBytes` measures that workload's original read
bytes. A separately hosted adapter on an HMP1 mirror still measures that mirror's
input stream. It is not HWT1 egress, and summing shared-source per-view values
counts the same producer more than once. `elapsedMs` and `captureMs` remain
per-adapter measurements.

The mounted renderer separately reports local `rasterScale` (the mount-time
requested value, 0.5..3), `backingScale` (actual, possibly below 0.5), and
`backingWidth`/`backingHeight`. Its framebuffer follows fitted physical pixels
within the requested-scale/device limits, using 1×1 backing when hidden. GPU
canvas limits reduce resolution with a local warning, not a logical-grid change.
Glyph/atlas scale stays fixed; full-grid geometry and per-view caches remain.
The internal main-thread-to-worker framebuffer-size notification and these local
metrics are separate from the HWT1 text `viewport` command. A client can append its canvas warning
to displayed diagnostics without modifying the server's `warnings` array.

**Decoder tolerances:** the reference decoder permits omitted default colors
and accepts cursor shape strings matching the names above, converting them to
numeric values. The current server emits both colors and numeric shapes.
Other unrecognized object properties are ignored by current readers, but that
is not a negotiated extension mechanism. Unsupported magic/version is fatal.

### 3.2. Cell records

Offsets below are relative to the start of each record:

| Offset | Size | Type | Field |
|---|---:|---|---|
| `0` | 4 | `u32` | `index` |
| `4` | 4 | `u32` | `foreground` |
| `8` | 4 | `u32` | `background` |
| `12` | 4 | `u32` | `underlineColor` |
| `16` | 2 | `u16` | `attributes` |
| `18` | 1 | `u8` | `width` |
| `19` | 1 | `u8` | `underlineStyle` |
| `20` | 2 | `u16` | UTF-8 text byte length `T` |
| `22` | `T` | bytes | `text` |

`index = row * columns + column`. Every index MUST be in the current grid and
occur at most once per frame. The producer emits changed indices in ascending
row-major order; the receiver does not require that order. A full frame contains
all `columns * rows` indices, including blanks and continuation cells. A delta
replaces only the listed cells; unlisted cells retain all their previous fields.
A zero-cell delta is valid: cursor, mouse mode, peer role, placements, or resources can
change without changing text.

Each text field contains the server's cell text, normally a grapheme cluster,
not necessarily one scalar value or one UTF-16 code unit. Its maximum encoded
length is 65,535 bytes. The reference decoder rejects malformed UTF-8.

#### Cell width and ownership

`width` is the number of horizontal cells **owned by this cell's glyph**, not a
request to perform Unicode width calculation in the browser:

* `width == 0` represents an empty-text continuation cell. It still has its own
  index, background, and attributes; do not draw another glyph there.
* A normal lead/blank cell has width at least 1.
* For a naturally wide grapheme, the server counts only contiguous empty-text
  continuation cells on the same row with the lead cell's sequence identity.
  A clipped or partially overwritten wide glyph can therefore own fewer cells
  than its natural advance.
* Sequence identity is used during projection but is **not** transmitted.
  Clients MUST rasterize at natural advance and clip to the owned span, not
  squeeze, reflow, or extend into cells no longer owned by the lead.

The projection substitutes a space for a null character value, a NUL character,
or the internal U+E000 placeholder. Empty strings remain continuation cells.
The binary cell record has no hyperlink, font, original palette index, terminal
sequence number, or original Sixel-ownership field. Hyperlinks are carried in
the complete viewport metadata instead.

### 3.3. Colors and attributes

A packed color is `R | (G << 8) | (B << 16) | (A << 24)`, conventionally written
`0xAABBGGRR`. Thus an opaque red color is the integer `0xff0000ff` and bytes
`ff 00 00 ff`, not an ARGB or CSS hex string. Channels are 8-bit values; alpha
uses ordinary unpremultiplied source-over compositing.

The server resolves palette/default foreground and background to RGB, swaps
foreground/background for Reverse, then halves each foreground RGB channel
(integer division) for Dim. These transformations are already reflected in the
wire colors. Clients MUST NOT apply Reverse or Dim to those colors again.

One deliberate exception to opaque cell colors supports graphics layering:
when a cell has a default/unset background and is **not** reversed, its background
alpha is zero while its resolved RGB remains present. Explicit cell backgrounds
and reversed backgrounds remain opaque. The opaque `defaultBackground` clears
the viewport beneath all layers. The adapter defaults are foreground
`0xffdedede` and background `0xff181818`.

`underlineColor` is the resolved explicit underline color, or the final projected
foreground when no explicit non-default underline color exists.

The `u16` attribute bits follow [`CellAttributes`](../src/Hex1b/CellAttributes.cs):

| Bit / mask | Meaning |
|---|---|
| 0 / `0x0001` | Bold |
| 1 / `0x0002` | Dim; projected foreground already dimmed |
| 2 / `0x0004` | Italic |
| 3 / `0x0008` | Underline |
| 4 / `0x0010` | Blink |
| 5 / `0x0020` | Reverse; projected colors already swapped |
| 6 / `0x0040` | Hidden |
| 7 / `0x0080` | Strikethrough |
| 8 / `0x0100` | Overline |
| 9 / `0x0200` | Retired Sixel bit; no current meaning |
| 10 / `0x0400` | SoftWrap, informational; not permission to reflow |
| 11 / `0x0800` | Protected, informational; selective erase is server-owned |
| 12..15 | No defined current meaning |

The producer carries the terminal attribute mask through projection. The
reference renderer uses Bold/Italic for glyph rasterization, hides glyphs and
decorations for Hidden, and suppresses blinking glyphs/decorations in the off
phase. Backgrounds remain. Intrinsically colored glyphs, such as color emoji,
keep their own colors and use Dim as a separate 0.5 RGB tint rather than the
projected foreground. No original glyph colors are transmitted.

`underlineStyle` follows [`UnderlineStyle`](../src/Hex1b/UnderlineStyle.cs):
`0` None, `1` Single, `2` Double, `3` Curly, `4` Dotted, `5` Dashed. A nonzero
style takes precedence over the Underline bit; style zero plus the Underline bit
renders a single underline. The reference decoder rejects styles above 5, but
does not reject unknown attribute bits or validate width/text ownership.

## 4. Images, placements, and compositing

### 4.1. Resources

Each `images` descriptor has:

| Field | Type | Meaning |
|---|---|---|
| `key` | string | Opaque, case-sensitive resource identity, scoped to this connection. |
| `width`, `height` | positive integers | Raster dimensions in source pixels. |
| `format` | string | Exactly `"rgba"` or `"png"`. |
| `byteLength` | positive integer | Number of bytes in this image's payload at the end of the frame. |

There is no payload offset, MIME field, base64 field, or image bytes inside JSON.
For `"rgba"`, bytes are tightly packed, top-to-bottom rows and left-to-right
pixels, four bytes `R G B A` per pixel, with unpremultiplied alpha;
`byteLength == width * height * 4`. For `"png"`, payload bytes are the complete
PNG, and decoded dimensions MUST match the descriptor.

The server translates KGP RGB to RGBA, forwards valid-sized KGP RGBA/PNG, and
materializes Sixel painted crops/damage as RGBA. KGP animation is server-driven:
resources describe the current frame, not an animation command stream.
Clients MUST NOT parse resource keys to determine rendering behavior. Current
keys are content-derived (`k:` for KGP, `s:` for Sixel); that spelling does not
replace `format` or `kind` and is not an API for reconstructing terminal IDs.

### 4.2. Retention transaction

Apply each frame as one state/resource transaction:

1. Validate descriptors, cell records, and exact payload boundaries before
   committing new terminal state.
2. Make incoming resources available by `key`. An incoming key replaces the old
   resource with that key, if any.
3. Establish exactly the resource set in `retainedImages`. A retained key MUST
   be supplied by this frame or already be available from the prior state.
4. Remove resources not retained, after outstanding rendering work can no
   longer reference them.
5. Replace the complete placement list and apply the cell/cursor state.

Both incoming and retained keys are unique within their respective arrays.
Every incoming key and placement key MUST belong to `retainedImages`. Multiple
placements can use one resource. Removing a placement does not necessarily
remove its resource: unplaced retained resources are a cache, not visible images.

A full frame is self-contained: the producer clears its cache and re-supplies
all resources retained by that baseline. The client MUST be able to construct it
without any resources from earlier frames. A delta can have `images: []` while
moving existing placements, and `placements: []` removes every prior placement.

The server keeps inactive resources until decoded-byte/count pressure requires
eviction, oldest last-used revision first. The complete retention list makes
those evictions explicit. There is no separate delete-resource or resource-ack
message. Failure to resolve a retained key is fatal in the reference client,
not an automatic resource-fetch request.

### 4.3. Placement geometry

Each placement has all of the following fields:

| Field | Type | Coordinate system / meaning |
|---|---|---|
| `key` | string | Retained image resource key. |
| `kind` | string | `"kgp"` or `"sixel"`. |
| `x`, `y` | numbers | Destination upper-left, in terminal logical pixels. |
| `width`, `height` | nonnegative numbers | Destination size, in terminal logical pixels. |
| `sourceX`, `sourceY` | numbers | Source rectangle upper-left, in image pixels. |
| `sourceWidth`, `sourceHeight` | nonnegative numbers | Source rectangle size, in image pixels. |
| `clipX`, `clipY` | numbers | Clip rectangle upper-left, in **absolute terminal logical pixels**, not relative to `x`,`y`. |
| `clipWidth`, `clipHeight` | nonnegative numbers | Clip rectangle size, in terminal logical pixels. |
| `z` | integer from server | Signed stacking value; not a cell coordinate. |

Geometry numbers can be fractional or negative in positions; sizes cannot be
negative. Rectangles use upper-left origins with exclusive right/bottom edges.
Map the source rectangle linearly onto the destination rectangle. Intersect the
result with the explicit clip rectangle, the terminal viewport, and the
destination-space image of the valid source raster. Source coordinates outside
the raster MUST be clipped, not rendered by stretching edge texels. A zero
source/destination/clip extent draws nothing. Do not independently reinterpret
KGP crop, native-size flags, or Sixel cell metrics on the client.

For example, a source rectangle `(2, 3, 3, 3)` from an 8×8 image mapped to
destination `(5, 4, 3, 3)` draws at its native cropped size. The clip rectangle
`(5, 4, 3, 3)` is in terminal pixels; it is not source `(5, 4)` and is not an
additional offset from destination `(5, 4)`.

The server resolves KGP cell offsets, explicit display spans, native sizes, and
render-geometry clips before emitting these fields. Sixel placements reference
already materialized painted pixels; Sixel crop/damage is not a browser command.
An unavailable Sixel raster is omitted with a warning; an empty KGP source crop
also produces a warning and no placement.

### 4.4. Layer order

The reference composition order, back to front, is:

1. Clear the viewport with opaque `defaultBackground`.
2. Image placements with effective `z < -1073741824` (`-2^30`).
3. Every cell's individual background, including width-zero cells.
4. Image placements with `-1073741824 <= z < 0`.
5. Text glyphs and decorations.
6. Image placements with `z >= 0`.
7. Cursor.

For KGP, effective `z` is the transmitted value. For Sixel, it is always `-1`
(the producer also emits `z: -1`). Within image layers, sort by effective `z`,
then preserve array order for ties. The producer orders KGP by z, image ID,
and graph ID, followed by Sixel in sequence order. Those IDs/sequence numbers
are not transmitted, so clients must preserve the resulting placement order.

This distinguishes images behind explicit cell backgrounds from images behind
text only. The reference renderer uses linear image sampling and ordinary
source-over blending; font selection and exact glyph rasterization remain local.

## 5. Revisions, acknowledgements, and resynchronization

### 5.1. Sender state

The producer starts at revision 0 and increments once per encoded frame. The
first frame is revision 1, `full: true`, `baseRevision: 0`. Subsequent deltas
refer to the immediately preceding projected revision. Full frames reset the
**baseline**, not the revision counter. A changed grid or a handled `resync`
request causes a full baseline on a subsequent capture. Resizing to the same
dimensions alone does not require one.

Here “producer” means this HWT1 connection's projector, not the shared HMP1
terminal instance. Role changes invalidate presentation without requiring new
workload output; upstream geometry changes likewise produce a frame even when
the shared workload emits nothing.

The producer uses checked `uint32` revision increments, not wrapping arithmetic.
There is no rollover procedure. A new connection/adapter is required before
that counter is exhausted.

At most **one state frame is outstanding**. After yielding frame `R`, the next
frame read waits for its acknowledgement before capturing another state.
Workload output and terminal interpretation continue during this wait. A
capacity-one dirty notification coalesces changes; intermediate terminal states
can be skipped. Acknowledging a frame does not itself cause another frame.
Frames are snapshots, not a replayable terminal change journal.

The terminal tracks DEC private mode 2026 (`CSI ? 2026 h/l`). HWT1 capture
waits while an update is open, including for initial attachment, resync,
history views, and invalidations from animation ticks. The existing browser
frame remains visible; the canvas is not hidden or swapped. Mode checking and
capture share the terminal buffer lock, so a new begin marker cannot race a
waking reader into publishing partial state. Producer ordering locks are
released while waiting so parsing, input, and the end marker can proceed.

A terminal-owned one-second watchdog releases an unterminated update.
Repeated begin markers are idempotent and do not renew the deadline; soft/full
reset also releases the wait. Read cancellation preserves pending invalidation,
and disposal cancels waiting views. This is paired implementation policy, not
a new HWT1 message or a guarantee that every application frame is delivered.
Diagnostic snapshots still expose current terminal state during an update.

### 5.2. Acknowledgement

```json
{"type":"ack","revision":2}
```

`revision` is a required unsigned 32-bit integer. An acknowledgement equal to the
outstanding revision releases the sender. A lower value is ignored; a duplicate
matching acknowledgement is harmless. A value ahead of the latest yielded
revision is invalid. An acknowledgement of zero before any frame has no effect.

An acknowledgement is a delivery/backpressure message, not an input result, a
resource-by-resource acknowledgement, or proof that a human saw the screen.
The reference client normally sends it **after** resource preparation, state
application, rendering, and `GPUQueue.onSubmittedWorkDone()`. That bounds GPU
work and texture lifetime; it is not GPU readback, a GPU timing measurement, or
display scan-out confirmation. A future alternative first-party rendering backend
would need an equivalent safe resource lifetime boundary, not WebGPU specifically.

The adapter's default acknowledgement timeout is two minutes, configurable by
`AcknowledgementTimeout`. It is measured while the next read waits for the prior
acknowledgement, not by a timer in the wire protocol. Hosts must independently
bound transport writes and end failed connections.

### 5.3. Delta acceptance and resync

A delta can be applied only if:

* a baseline already exists;
* `baseRevision` equals the receiver's local applied revision;
* `revision` is greater than that local revision; and
* `columns` and `rows` match the local grid.

The reference receiver does not require `revision == localRevision + 1`.
Although the producer emits full frames with `baseRevision: 0` and increasing
revisions, the reference receiver does not apply its delta ordering/base checks
to full frames.

If a structurally valid delta fails the acceptance checks, the reference client
does **not** apply its cells or resources. It releases the sender's gate and
requests a baseline, in this order:

```json
{"type":"ack","revision":7}
{"type":"resync"}
```

These are **two messages**, not one JSON document. Revision 7 is the discarded
frame's revision, not the local revision. This acknowledgement is an explicit
exception to the normal “applied and presented” acknowledgement policy.

`resync` has no other fields. It marks a future capture full and invalidates the
presentation. It does **not** acknowledge, cancel, or replace an outstanding
frame; repeated requests coalesce. Thus a manual resync during an outstanding
frame still needs that frame's acknowledgement. An already captured frame may
precede the requested baseline; resync is not an out-of-band transport barrier.

Malformed frames, invalid resource data, missing textures, or GPU failures do
not take this recovery path: the reference client fails the connection. Receiving
a second frame before releasing the first is also fatal. There is no nack.

## 6. Client input messages

All client messages are complete UTF-8 JSON objects of at most **65,536 bytes**,
including JSON syntax/escaping. There is no separate text-character limit.
Required properties must have their specified types; null does not substitute
for a required string. Unknown command types are rejected. Current handlers
ignore unrelated properties; this does not define additional input capabilities.

`shift`, `alt`, and `ctrl`, where applicable below, are optional booleans,
defaulting to false. Non-boolean values are invalid. There is no Meta modifier
semantics: the browser may include `meta: false`, but the core ignores `meta`.

### 6.1. Text and paste

```json
{"type":"input","text":"Hello 世界"}
```

`input.text` is sent to the workload as UTF-8 without key translation, newline
normalization, or bracketed-paste wrappers. Empty text is valid. IME/composition
commits and multi-code-point text belong here, not in `key`. Text is not
sanitized: escaped control characters are still input characters.

```json
{"type":"paste","text":"first line\nsecond line"}
```

`paste.text` is also UTF-8, but if the live terminal has bracketed-paste mode
enabled, the server surrounds it with `ESC [ 200 ~` and `ESC [ 201 ~`.
Otherwise it is passed through unchanged. The browser MUST NOT add those
wrappers itself. HWT1 does not provide clipboard reads, clipboard writes, or
confirmation of workload consumption.

### 6.2. Keys

```json
{"type":"key","key":"ArrowUp","shift":false,"alt":false,"ctrl":true}
```

`key` is required and accepts:

* `ArrowUp`, `ArrowDown`, `ArrowRight`, `ArrowLeft`;
* `Home`, `End`, `Insert`, `Delete`, `PageUp`, `PageDown`;
* `F1` through `F12`;
* `Enter`, `Backspace`, `Tab`, `Escape`; or
* a string of **one .NET UTF-16 code unit**.

Other key names are rejected. In particular, a supplementary Unicode character
or a multi-code-point grapheme is not a single-character `key`; send `input`.
There is no key-up, scan code, physical `code`, repeat count, or negotiated
extended keyboard protocol. Repeats can be sent as repeated key messages.

The server encodes against live terminal modes. With `m = 1 + shift + 2*alt +
4*ctrl`, each boolean taken as zero or one, current encoding is:

| Key family | No modifiers (`m == 1`) | With modifiers |
|---|---|---|
| Arrows/Home/End, finals `A/B/C/D/H/F` respectively | `ESC [ final`, or `ESC O final` in application-cursor mode | `ESC [ 1 ; m final` |
| Insert/Delete/PageUp/PageDown, numbers `2/3/5/6` | `ESC [ n ~` | `ESC [ n ; m ~` |
| F5..F12, numbers `15/17/18/19/20/21/23/24` | `ESC [ n ~` | `ESC [ n ; m ~` |
| F1..F4, finals `P/Q/R/S` | `ESC O final` | `ESC [ 1 ; m final` |

Spaces in the notation above separate tokens; they are not transmitted.
For other keys:

* Enter → CR (`0x0d`); Backspace → DEL (`0x7f`), or BS (`0x08`) with Ctrl.
* Tab → HT (`0x09`), or `ESC [ Z` with Shift; Escape → ESC (`0x1b`).
* A one-code-unit text key is unchanged, except Ctrl uppercases it invariantly
  and maps `@` through `_` to `character & 31`; Ctrl+space becomes NUL.
  Other text remains unchanged.
* Alt prefixes ESC to the result for these text/basic keys. Shift does not
  uppercase supplied text on the server. Ctrl does not otherwise change
  Enter/Tab/Escape.

For example, Ctrl+ArrowUp becomes `1b 5b 31 3b 35 41` (`ESC [ 1 ; 5 A`),
regardless of application-cursor mode.

### 6.3. Resize

```json
{"type":"resize","columns":100,"rows":30}
```

Both fields are required integers. Columns MUST be **20..300** and rows
**10..100**, inclusive. For a standalone terminal, the adapter resizes through
its normal notification. For a shared-source browser peer, the HMP1 presentation
adapter applies its existing primary-only resize handler. For an HMP1-backed
mirror, the request routes through
`Hmp1WorkloadAdapter`, which suppresses secondary resizes; the HMP1 producer also
accepts resize only from its primary. The mirror and
browser MUST NOT optimistically resize before authoritative geometry returns.
A secondary request does not change the shared grid.

When captured dimensions change, the next applicable frame is full. Resize does
not bypass the acknowledgement gate and has no separate success response.
Upstream geometry notification invalidates presentation even without workload
output, and MUST NOT echo back upstream as another resize request.

Until a new frame arrives, a client can still display the old grid. Mouse input
is checked against the **live** server dimensions, so stale coordinates can be
ignored during that interval.

### 6.4. Request primary

```json
{"type":"requestPrimary","columns":100,"rows":30}
```

Both dimensions are required and use the same local bounds as `resize`.
The request explicitly asks the upstream HMP1 producer to make this peer primary
using the requested grid. It is not an HWT1 election or a locally granted role.
Updated `peer` metadata confirms the result; a mounted client's
`requestPrimary()` method returns without awaiting that confirmation.

For a standalone adapter this message performs a normal resize and retains the
standalone primary state. For an HMP1-backed adapter not yet connected, valid
dimensions are checked but the request is ignored. The mounted JS method instead
reports that it is waiting for HMP1 when the view has no peer and is not primary.

Attaching to an existing sample instance starts secondary even if primary is
unassigned. The New terminal UI explicitly claims for its newly created
instance; ordinary attachment does not. Closing the primary preserves the grid
and leaves `primaryId` null until an explicit claim. Primary controls geometry,
not input: all connected HMP1 peers may send input. A client's `readOnly` option
only suppresses its local input capture and is not server authorization.

### 6.5. Text viewport, selection, and copy

Inspection commands use one monotonically increasing positive safe-integer
`requestId` sequence per connection, shared across `viewport`, `selection`, and
`copy`. This sequence is independent of HWT1 frame revisions. Results can be
coalesced into a later frame, so receivers compare identifiers rather than
assuming one response per pointer event.

Scroll relative to the current view, with positive deltas toward live:

```json
{"type":"viewport","requestId":1,"delta":-12}
```

Return to live:

```json
{"type":"viewport","requestId":2,"live":true}
```

During an active local drag, combine scrolling and endpoint extension:

```json
{"type":"viewport","requestId":3,"delta":-3,"extend":{"row":0,"column":12}}
```

The server resolves `extend` against the viewport **after** scrolling. Its row
and column are zero-based displayed coordinates. The selection's original
anchor and mode are retained; the browser preserves rectangular column bounds
for wheel and edge-autoscroll operations.

Start a selection using a row identity from a presented frame:

```json
{"type":"selection","requestId":4,"action":"start","generation":"1","rowId":"42","column":5,"mode":"line"}
```

Modes are `character`, `word`, `line`, and `rectangle`. `line` selects whole
logical lines, traversing soft wraps. An `extend` action uses the same
generation/row/column coordinates while retaining the existing anchor and mode.
The browser does not derive identities from row offsets or parse ANSI.

Resolve the current selection for an explicit copy operation:

```json
{"type":"copy","requestId":5,"generation":"1","selectionRequestId":4}
```

The selection and generation guards refer to the selection being copied.
A successful result carries authoritative plain text, including selected rows
outside the displayed viewport. Character/word/line extraction joins soft wraps;
rectangles preserve one line per physical row and trim trailing padding.

Clear a selection:

```json
{"type":"selection","requestId":6,"action":"clear"}
```

Copying after a clear produces a non-valid result, not the old selection's text.
Selections exceeding 512 Ki UTF-16 code units invalidate explicitly rather than
returning truncated text.

Clipboard access remains a browser user gesture. The reference client creates
a promise-backed `ClipboardItem` during that gesture and resolves its text from
the correlated server result. It rejects pending, changed, evicted, or disconnected
selections and reports clipboard errors. Metadata arriving without a pending
copy request never writes the clipboard.

The reference client's input bindings are local policy, not HWT1 commands.
Its default right-click action copies a selection and then explicitly sends a
`selection` command with `action: "clear"` after a successful clipboard write, provided the same
selection is still current. With no selection it reads the clipboard and sends
the existing `paste` command. Read-only views never perform application paste.
Main/alternate screen do not change that default; application mouse capture
wins unless Shift requests local handling. Consumers can replace this policy
without changing the wire protocol.

Likewise, the browser's `selectionui` event/`onSelectionUI` overlay hook is local
presentation policy. Custom controls invoke existing actions and commands;
overlay elements, CSS, and UI event handlers are never transferred over HWT1.

Inspection does not require primary status or send application mouse input.
Text, paste, and key input clear selection and return the requesting view to
live before delivery. Copy does neither. The main screen and alternate screen
have distinct generations; historical graphics are not included in this first
text-history milestone.

## 7. Mouse intent, filtering, and encoding

```json
{"type":"mouse","action":"down","button":"left","x":12,"y":3,"shift":false,"alt":false,"ctrl":false}
```

| Field | Required / bounds |
|---|---|
| `action` | Required string: `down`, `up`, `move`, or `wheel`. |
| `button` | Required string, from the combinations below. |
| `x` | Required integer, zero-based column, **0..1023**. |
| `y` | Required integer, zero-based row, **0..511**. |
| `count` | Optional integer, default 1; **1..32**, and MUST be 1 except for `wheel`. |
| `shift`, `alt`, `ctrl` | Optional booleans, default false. |

Valid action/button combinations:

| Action | Allowed buttons |
|---|---|
| `down`, `up` | `left`, `middle`, `right` |
| `move` | `left`, `middle`, `right`, `none` |
| `wheel` | `wheelUp`, `wheelDown`, `wheelLeft`, `wheelRight` |

`count` repeats a wheel report; it is not click count. There is no `drag` action,
pressed-button bitset, pointer ID, pixel coordinate, or double-click message.
A move with a held button represents drag intent. For example:

```json
{"type":"mouse","action":"wheel","button":"wheelDown","x":0,"y":0,"count":3}
```

### 7.1. Live-mode filtering

The server, not the browser, chooses the terminal mouse encoding and rechecks
the latest mouse tracking mode on every input. Metadata `mouseTracking` is a
capture-time hint for browser event collection, not authority to override live
state. If several tracking modes are enabled, precedence is
`1003 > 1002 > 1000 > 9 > 0`.

| `mouseTracking` | Effective reporting |
|---:|---|
| `0` | No reports. |
| `9` | X10: button-down only; no release, motion, or wheel; modifiers suppressed. |
| `1000` | Normal: down, up, wheel; no motion. |
| `1002` | Button tracking: normal reports plus motion with a non-`none` button. |
| `1003` | Any tracking: normal reports plus motion, including `none`. |

Out-of-protocol coordinates are invalid messages. Coordinates within protocol
bounds but outside the current terminal width/height are silently ignored.
Mode-excluded events are also silently ignored after message validation.
The server does not reconstruct pointer capture or verify a down/up history.

### 7.2. Terminal bytes, not HWT1 coordinates

These encodings describe bytes injected into the workload; they are **not**
alternate HWT1 message forms. Encoding precedence is SGR (mode 1006), then urxvt
(1015), then UTF-8 (1005), then legacy. The chosen encoding is not a metadata
field.

Button codes are `left=0`, `middle=1`, `right=2`, `none=3`, `wheelUp=64`,
`wheelDown=65`, `wheelLeft=66`, `wheelRight=67`. Add modifier bits Shift=4,
Alt=8, Ctrl=16, and motion bit=32. X10 suppresses modifier bits.
The `wheel` action is encoded as a down report.

| Encoding | Output |
|---|---|
| SGR | ASCII `ESC [ < code ; (x+1) ; (y+1) M`, with lowercase `m` for release. Releases retain the named button code. |
| urxvt | ASCII `ESC [ (code+32) ; (x+1) ; (y+1) M`. For release, replace the button code with `3` plus modifier bits. |
| UTF-8 / 1005 | `ESC [ M`, then UTF-8 encoding of code points `code+32`, `x+33`, `y+33`. Release uses `3` plus modifiers. Coordinates above 2014 are ignored by the shared encoder, though HWT1's input bounds are already smaller. |
| Legacy | Six raw bytes: `1b 5b 4d (code+32) (x+33) (y+33)`. Release uses `3` plus modifiers. If either zero-based coordinate exceeds **222**, ignore the event; do not clamp or UTF-8-encode these raw bytes. |

Each wheel report is repeated `count` times after encoding. For an unmodified
left-button down at HWT1 `(0,0)`, SGR emits `ESC [ < 0 ; 1 ; 1 M`; legacy emits
`1b 5b 4d 20 21 21`.

### 7.3. Reference browser capture policy

This policy is not additional wire syntax:

* Pointer position is `floor((clientPosition - canvasOrigin) * gridSize /
  canvasCssSize)`, independently on each axis. Outside positions are discarded,
  except captured drags/releases are clamped to the grid.
* Mouse pointers and the three named buttons are supported. Pointer capture
  maintains drags; loss of capture, cancellation, or window blur releases held
  buttons when reporting is active (except X10).
* Motion is deduplicated by cell/button/modifier state and coalesced to at most
  one pending animation-frame callback. Held-button preference for move reports
  is left, then middle, then right.
* Wheel deltas accumulate in CSS pixels. Line-mode deltas use cell height,
  page-mode deltas use canvas height; one cell height is one report on either
  axis. Each emitted direction is capped at 32 reports. Ctrl+wheel/pinch and
  Meta+wheel remain browser gestures. X10 sends no wheel.
* Ctrl/Cmd+left-click on an allowed OSC 8 destination opens a new tab on release
  with `noopener,noreferrer`, without sending a mouse report or starting a selection.
  Explicit input routes/actions take precedence, and Shift/Alt retain selection
  behavior. Dragging, cancellation, or a changed destination cancels activation.
  Hovering shows the destination and modifier hint. This works in historical and
  read-only views too; pending viewport transitions cannot activate stale links.
* Other Meta-modified pointer-down/motion is filtered. Geometry changes cancel capture.
  Changing a tracking mode does not transfer a locally owned gesture to the
  application.

Without application mouse reporting, left-drag selects locally and vertical
wheel scrolls text history. Double-click selects words; triple-click selects
logical lines; Alt/Option-drag selects a rectangle. Shift-click extends an
existing selection. During application reporting, Shift reserves local
selection/wheel and Shift+Alt/Option reserves rectangular selection.
Ownership is latched through pointer release/cancellation, even if modifiers
change. Historical rows never forward pointer input as live application clicks.
Wheel during a local drag scrolls and extends the endpoint; wheel after release
does not extend the retained selection. Edge-autoscroll follows the drag rule.

The reference keyboard collector preserves browser shortcuts and does not send
every syntactically supported HWT1 key. It excludes Meta, Alt without Ctrl,
AltGraph, composition/dead/process key events, Ctrl+Tab, Ctrl+F4,
Ctrl+Shift+single-character combinations, Ctrl+`l/t/v/w/+/-/=/0`, and
F1/F3/F5/F6/F7/F10/F11/F12. Normal text uses input/composition events; composition
commits are deduplicated. Clipboard text uses `paste`, not a second `input`.
Cmd+C and Ctrl+Shift+C are handled as explicit copy gestures instead of
application input; Ctrl+C remains application input. These are sample UX
choices; the core key parser still accepts F1..F12 and Alt.

## 8. Validation, bounds, and failure

The following are current implementation limits, not negotiated capacities.
Use the smaller sender/profile limits to keep the paired implementations aligned; a more permissive
decoder does not expand the public adapter's input or image limits.

| Limit | Reference receiver | Server adapter / projection |
|---|---|---|
| Complete state frame | 96 MiB | No separate total-frame budget check in the projection. |
| Metadata length | 2 bytes..8 MiB | UTF-8 serialized metadata. |
| Window title | Required string, at most 4,096 UTF-16 code units; no C0/DEL/C1 controls or unpaired surrogates | Normalized in the core before storage; scalar-safe truncation. |
| Grid | Columns 1..1024, rows 1..512, product at most 262,144 | Projection rejects geometry outside those receiver limits without clamping producer/mirror dimensions. Local resize/claim bounds remain 20..300 columns, 10..100 rows. |
| Changed cells | 0..grid product; full count equals product | Every full cell or each differing projected cell. |
| Text per cell | `u16` byte length; strict UTF-8 | At most 65,535 UTF-8 bytes. |
| Incoming/retained resources | At most 4,096 in each array | At most 4,096 retained after eviction. |
| Placements | At most 16,384 | No separate placement-count guard in the projection. |
| Image axis | 1..16,384; GPU device limit can be smaller | 1..4,096. |
| One image decoded size | Subject to receiver texture budget | At most 32 MiB (`width * height * 4`). |
| Decoded images | 256 MiB incoming descriptors and 256 MiB complete retained texture set | Preflight the unique active set against 64 MiB before dense pixel allocation; evict inactive cache entries before allocating replacements. Active resources cannot be evicted to fit. |
| Image key | Nonempty string, at most 1,024 JavaScript UTF-16 code units | Content-derived key. |
| Warnings | Array of at most 256 strings | Diagnostic strings; no matching explicit count cap in projection. |
| Client JSON message | Host assembles whole message | At most 64 KiB in adapter and sample host. |
| Selection text | Within the complete metadata limit | At most 524,288 UTF-16 code units; an oversized selection invalidates without returning partial text. |

One MiB means 1,048,576 bytes; one KiB means 1,024 bytes.

The grid guard runs before projection advances its revision or cache. A remote
grid larger than local request bounds is supported within the receiver envelope;
one exceeding that envelope fails presentation instead of resizing the producer.
This is distinct from reducing only the browser framebuffer to fit GPU limits.

Additional receiver checks:

* Revisions are safe JSON integers, revision at least 1 and base revision at
  least 0. The producer and acknowledgement parser are narrower (`uint32`).
* Default colors, when present, are integers 0..4,294,967,295.
* Cursor coordinates are integers `x=-1..1024`, `y=-1..512`; visibility is boolean
  and shape is 0..6 after optional string conversion.
* `peer` is required; `isPrimary` must be boolean. `id` and `primaryId` must each
  be null or a nonempty string of at most 256 JavaScript UTF-16 code units.
  When `id` is non-null, its equality to `primaryId` must match `isPrimary`.
* Each placement numeric field must be finite, absolute value at most
  2,147,483,648; widths/heights must be nonnegative. The decoder allows numeric
  fractional `z`, although the producer emits a signed integer.
* Stats' four values must be finite and nonnegative; warnings must be strings.
* Every cell read must fit, indices must be unique/in bounds, and remaining
  bytes must equal the sum of image lengths exactly.
* Incoming keys must be unique/retained. Placement keys must be retained.
  Resolving retained keys against the cache happens during resource preparation.
* For PNG, the renderer checks the signature, a 13-byte first IHDR chunk, and
  descriptor-matching dimensions before browser decode, then verifies decoded
  dimensions again. Invalid/undecodable PNG is fatal.

These checks are not a comprehensive semantic validator. In particular, the
decoder does not verify grapheme ownership, unknown attribute bits, a zero base
on full frames, or the presence of every full-frame resource without consulting
the existing cache. The producer's full-baseline guarantees remain requirements
even where the decoder is more permissive.

Invalid JSON, missing/wrongly typed fields, unsupported enum values, ahead-of-
server acknowledgements, and exceeded input bounds raise errors in the adapter.
Projection failures (including oversized/invalid graphics or text) are fatal to
the connection: the projection can already have advanced its baseline and is not
a retryable encoder. Do not discard a yielded frame and continue reading from
that adapter. Reconnect with a new adapter/terminal after fatal transport or
projection failure. In the shared-instance sample this means a new view
adapter; the host can attach it to the still-running HMP1 producer.

The reference client closes on decode/resource/render failure rather than
presenting partial state. Its renderer also has local GPU, glyph-cache, atlas,
and quad budgets; passing wire validation does not guarantee a device can render
the frame. Defensive limits do not make an exposed PTY safe for production.

## 9. Sample WebSocket host binding and lifetime

This section describes [`WebTerminalDemo`](../samples/WebTerminalDemo/README.md),
not requirements for using the transport-independent library.

* The `/ws` endpoint accepts a WebSocket connection without a negotiated
  `Sec-WebSocket-Protocol` value. **`HWT1` is the binary magic, not a currently
  registered WebSocket subprotocol token.**
* The host sends one complete HWT1 frame per **binary WebSocket message**.
  A WebSocket message may be transport-fragmented, but HWT1 frames are not split
  across messages or concatenated inside one message.
* The client sends one command object per **text WebSocket message**. The host
  assembles text fragments up to 64 KiB and rejects binary client messages.
* `/ws?instance={id}&name={displayName}` attaches a new view to an existing
  HTTP-created instance. The display name identifies an HMP1 peer; neither query
  parameter is an HWT1 handshake field.
* Requests must originate from a loopback remote IP and use a loopback IP or
  `localhost` Host. `/ws` and mutating HTTP requests additionally require an
  absolute `Origin` matching request authority and scheme, with path `/` and no
  query, fragment, or user info. Failed containment/origin checks return 403;
  a valid-origin `/ws` request without WebSocket upgrade returns 400. The mounted
  client/worker validates `ws:`/`wss:` URLs; origin authorization is the host's
  responsibility, not a worker-enforced same-host rule.
* The sample bounds shared producers to four instances and browser connections
  to eight web views. Capacity and request-validation responses are HTTP host
  responses, not HWT1 errors.
* HTTP request bodies are limited to 16 KiB and API responses use
  `Cache-Control: no-store`. This HTTP-body limit is separate from the 64 KiB
  HWT1 WebSocket command limit.

`Origin` checks and loopback restrictions are development containment, not an
authentication scheme. A different host must establish its own authentication,
authorization, origin policy, isolation, quotas, TLS, and transport timeouts.
The sample can start a shell as the server user; it is not a secure remote shell
service.

The sample ends a **view** when its sender or receiver finishes, or its producer
stops. It cancels sibling tasks and disposes that adapter/HMP1 peer, not the
shared producer. Closing the primary leaves the last grid and no assigned
primary. Closing the final view does not terminate the producer.
Adapter disposal cancels pending waits; hosts must also cancel the frame loop
when the terminal ends. Returned frame memory remains valid after later reads,
but an adapter cannot be reattached to another terminal or resumed on a new
connection.

The sample uses WebSocket close 1008 for handled invalid-input/state errors and
timeouts, close 1011 for other view failures, and normal close 1000 when the
view ends. Frame transport writes have their own two-minute timeout, separate
from the adapter's acknowledgement wait. The browser
uses close 1011 for renderer failure and destroys GPU resources. These codes and
reason strings belong to this binding, not a binary/JSON HWT1 error schema.
Attaching again creates a fresh HWT1 revision baseline, viewport/selection,
worker, and GPU cache for the same surviving producer, including access to its
retained history. This is host-managed attachment, not HWT1 revision replay
or automatic reconnect. Producer lifetime
ends on explicit HTTP deletion, workload exit, or server shutdown.

### 9.1. Shared-instance HTTP API — not core HWT1

| Route | Purpose |
|---|---|
| `GET /api/terminals` | Return the live-instance DTO array (200). |
| `POST /api/terminals` | Create an instance; return its DTO and instance `Location` (201). JSON properties default to `scene:"mixed"`, `columns:100`, `rows:30`, with optional `name`. |
| `DELETE /api/terminals/{id}` | End the shared producer and all attached views, then return 204; absent instance returns 404. |
| `POST /api/terminals/{id}/controls` | Update shared generated-workload controls (204); absent instance returns 404, shell returns 409. |

Create accepts the six sample scenes and dimensions 20..300 by 10..100. A
supplied name must be nonblank, contain no control characters, and be at most
80 .NET UTF-16 code units; the optional WebSocket display name uses the same
limit. Invalid request values return 400. Creating a fifth instance or opening
a ninth web view returns 429. `/ws` requires an instance ID; missing/invalid
query values return 400 and an absent/stopping instance returns 404.
Creation during shutdown returns 503; a workload-creation failure returns 500.

The instance DTO fields are `id`, `name`, `scene`, `columns`, `rows`, `peerCount`,
`primaryPeerId`, `createdAt`, `paused`, `rate`, and `batch`. Instance IDs and HMP1
peer IDs are distinct. `primaryPeerId` is null while no primary is assigned;
the grid remains at its last authoritative dimensions.
`peerCount` counts upstream HMP1 clients, not local playground windows.
`createdAt` is the instance's timestamp. Generated instances expose their current
shared `paused` boolean and integer `rate`/`batch`; those three fields are null
for a shell. Generated defaults are `paused:false`, `rate:60`, `batch:100`.

For example, the controls endpoint accepts:

```json
{"paused":true,"rate":30,"batch":20}
```

Fields are optional: `paused` is boolean, `rate` is an integer 1..120, and `batch`
is an integer 1..1000; omitted/null fields leave that setting unchanged. Invalid
numeric ranges return 400. They apply to the shared generated workload, not frame
acknowledgements, GPU presentation, or the terminal's animation clock. Controls
do not apply to shell instances.

There are **no WebSocket `pause` or `rate` commands** in this sample binding.
The core adapter rejects those command types as unknown. The HTTP API, instance
lifetime, floating-window actions, and mount/dispose API are host/client
integration, not additional HWT1 binary fields or a third-party contract.

## 10. Small annotated wire example

This illustrative **delta** changes index 0 of an already established 20×10
grid at revision 1 to `A`. There are no images. Metadata is shown pretty-printed
for readability; whitespace contributes to `M` if actually transmitted.
The example uses standalone peer defaults; counters/timings are illustrative,
not a recorded multi-head benchmark.

```json
{
  "version": 1,
  "revision": 2,
  "baseRevision": 1,
  "full": false,
  "columns": 20,
  "rows": 10,
  "cellWidth": 10,
  "cellHeight": 20,
  "mouseTracking": 0,
  "peer": { "id": null, "primaryId": null, "isPrimary": true },
  "title": "",
  "progress": { "state": "none", "percentage": null },
  "shellIntegration": { "phase": "unknown", "lastExitCode": null },
  "history": null,
  "hyperlinks": [],
  "defaultBackground": 4279769112,
  "defaultForeground": 4292796126,
  "cursor": { "x": 1, "y": 0, "visible": true, "shape": 0 },
  "images": [],
  "retainedImages": [],
  "placements": [],
  "stats": { "workloadBytes": 1, "outputBatches": 1, "elapsedMs": 1, "captureMs": 0.25 },
  "warnings": []
}
```

Construct the envelope as magic `48 57 54 31`, the four-byte little-endian UTF-8
metadata length, the metadata bytes, then the following **27 bytes**. The first
four bytes are the changed-cell count; the remaining 23 bytes are one record.
This cell section is covered by
`WebTerminalProjectionTests.Encode_TextDelta_MatchesDocumentedHwt1CellBytes`.

```text
01 00 00 00                changed count = 1
00 00 00 00                index = 0
de de de ff                foreground = 0xffdedede
18 18 18 00                default cell background, alpha = 0
de de de ff                underline color = foreground
00 00                      attributes = none
01                         width = 1
00                         underline style = none
01 00                      UTF-8 byte length = 1
41                         "A"; end of frame (no image payloads)
```

A width-two `界` lead instead has width byte `02`, text-length bytes `03 00`,
and text bytes `e7 95 8c`. Its owned continuation at the next index has width
`00` and text length `00 00`. Both are ordinary cell records; the lead does not
implicitly create or overwrite the next record. A full 20×10 baseline must send
all 200 records.

Existing executable coverage includes
[`WebTerminalProjectionTests`](../tests/Hex1b.Tests/WebTerminalProjectionTests.cs)
for projection, Unicode ownership, image placement/cropping, and resync, and
[`Hwt1PresentationAdapterTests`](../tests/Hex1b.Tests/Hwt1PresentationAdapterTests.cs)
for the full-frame and acknowledgement/input lifecycle.
The package's [Node regressions](../src/web-terminal/tests/web-terminal.test.mjs)
cover peer metadata validation and local framebuffer/logical-geometry
calculations without a browser. Their stub GPU does not validate actual GPU
presentation or mounted DOM/input/lifetime behavior.
[Title regressions](../src/web-terminal/tests/title.test.mjs) additionally execute
the real decoder, worker, and mounted handle through Node worker messages with
minimal DOM, WebSocket, and renderer doubles. They cover presentation ordering,
initial/duplicate/clear notifications, rejected frames, independent views,
disconnect/remount, abort/disposal, and callback errors, not real GPU or network I/O.
The [title browser fixture](../samples/WebTerminalDemo/tests/titles.browser.js)
uses a real POSIX shell, worker, renderer, and WebSocket for direct/relay title
changes, late/reconnected views, safe host text, reset retention, and disposal.
The persisted [mounted-browser fixture](../samples/WebTerminalDemo/tests/mount.browser.js)
exercises the actual component's DOM, input, resize observation, and cleanup in
an isolated DPR2 context, but mocks the Worker and opens no WebSocket. It is not
an end-to-end HWT1/HMP1 or real GPU presentation test.

## 11. Explicitly absent and future work

Current HWT1 supports server-backed text viewports, row identities, selection,
and plain-text copy. Historical Sixel/KGP projection, preserving selections
through reflow, independent range-prefetch caches, a reattach/replay token,
retained-session negotiation, and a browser-side animation timeline remain
absent. Resync replaces the current view baseline; it is not durable recovery.
Unmarked output has no application-frame transaction guarantee. Synchronized
output gates HWT1 capture as described in section 5.1; watchdog recovery may
publish an incomplete update rather than leave the display frozen.

The sample's HTTP instance attachment permits fresh views of a surviving
producer. It does not add a HWT1 reattach message,
preserve old HWT1 revisions/caches, or provide durable/automatic recovery.

Future negotiation, version evolution, historical graphics, automatic recovery,
more efficient deltas, broader geometry profiles, and production-host security
are **proposals, not implemented messages**. See the separate
[web-terminal design discussion](web-terminal.md) for those directions.
Changes should update the first-party server, client, tests, and these notes
together. There is no requirement to preserve an older wire format for independent
implementations. Recommending third-party implementations remains a separate,
future decision.
