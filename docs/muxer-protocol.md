# Hex1b Muxer Protocol (HMP) v1

The Hex1b Muxer Protocol is a binary framing protocol for multiplexing terminal sessions over any bidirectional byte stream (Unix domain sockets, TCP, named pipes, etc.).

> **In-place update — no protocol version bump.** Hex1b is pre-v1 and currently
> coordinates its first-party consumers. HMP1 has been extended in-place
> with multi-head primary/secondary semantics and four new frame types
> (`RequestPrimary`, `RoleChange`, `PeerJoin`, `PeerLeave`) plus an extended
> `Hello` payload and a new client-emitted `ClientHello`. Binaries predating those
> baseline additions cannot speak updated HMP1 — those builds upgrade together. Animation replay also
> adds the one-time `KgpAnimationState` checkpoint described below.
> Activity replay adds a mandatory `ActivityState` checkpoint after every
> `StateSync`; producers and consumers must upgrade together.
>
> **Optional scrollback extension.** Retained-history transfer is negotiated
> independently with `scrollbackHistoryVersion: 1`; the HMP1 version remains
> `1`. It is backward compatible with peers that already implement the current
> `ClientHello` / `StateSync` / mandatory `ActivityState` baseline. Missing
> scrollback negotiation fields keep screen-only text replay. This does not restore compatibility
> with the older, pre-`ActivityState` protocol described above.
> Retained OSC 133 command marks use a separate optional
> `commandMarkHistoryVersion: 1` negotiation with the same compatibility rules.
> Text history and command marks can fall back independently.

## Frame Format

Every message is a frame with the following structure:

```
+------+-----------+---------+
| Type |  Length   | Payload |
| 1 B  | 4 B (LE) | N bytes |
+------+-----------+---------+
```

- **Type** (1 byte): Frame type identifier (see below)
- **Length** (4 bytes, little-endian): Payload length in bytes (0–16,777,216)
- **Payload** (variable): Type-specific data

Maximum payload size: 16 MB.

## Frame Types

| Name | Value | Direction | Description |
|------|-------|-----------|-------------|
| Hello | `0x01` | Server → Client | Initial handshake: protocol version, dimensions, assigned peer id, current primary, roster |
| StateSync | `0x02` | Server → Client | Full screen snapshot (raw ANSI) |
| Output | `0x03` | Server → Client | Incremental terminal output (raw ANSI) |
| Input | `0x04` | Client → Server | Keyboard input (raw bytes) |
| Resize | `0x05` | Bidirectional | Terminal dimensions changed (silently dropped server-side from a non-primary peer) |
| Exit | `0x06` | Server → Client | Terminal session has ended |
| RequestPrimary | `0x07` | Client → Server | Asks the server to make this peer the primary at the given dimensions |
| RoleChange | `0x08` | Server → Client (broadcast) | Primary peer changed (or transitioned to "no primary") |
| PeerJoin | `0x09` | Server → Client (broadcast) | A new peer joined the session |
| PeerLeave | `0x0A` | Server → Client (broadcast) | An existing peer disconnected |
| ClientHello | `0x0B` | Client → Server | Client identifies itself before the server's Hello (display name, default role) |
| KgpAnimationState | `0x0C` | Server → Client | One-time playback-progress checkpoint following KGP animation replay |
| ActivityState | `0x0D` | Server → Client | Mandatory activity baseline immediately following each StateSync |
| ScrollbackState | `0x0E` | Server → Client | Negotiated retained-history header following ActivityState |
| ScrollbackRows | `0x0F` | Server → Client | Negotiated binary history rows, in chunks of at most 1 MiB |
| CommandMarkState | `0x10` | Server → Client | Independently negotiated retained OSC 133 marks after activity and optional scrollback |

## Peer IDs

A **peer ID** is an opaque string assigned by the producer (server) to each
attached client. Peer IDs appear in the `Hello`, `RoleChange`, `PeerJoin`,
and `PeerLeave` frames as well as in the `peers[]` roster. They are the
sole means by which the producer and clients refer to specific connected
peers.

**Lifetime and scope**

- Assigned by the producer when it accepts a connection and emits its
  `Hello`.
- Stable for the lifetime of a single connection.
- **Per-connection, not per-client identity.** A client that disconnects
  and reconnects is a new peer with a new ID. The protocol does not
  carry a notion of identity that survives reconnects.
- Locally unique within the set of currently-connected peers of a single
  producer. They are not globally unique and are not intended to be
  cross-referenced across producers, sessions, or time.

**Opacity contract**

Peer IDs **must** be treated as opaque values by clients. Specifically,
clients must not:

- assume any particular length, character set, prefix, or encoding;
- parse, decode, or extract structure from a peer ID;
- generate or invent peer IDs (`ClientHello` supplies hints and capabilities,
  not a peer ID);
- assume two equal peer IDs from different connections refer to the same
  client.

The only operations clients should perform on peer IDs are:

- byte-wise / string equality comparison (e.g. `peerId == primaryPeerId`
  to determine whether *this* client is currently the primary);
- storing them in a roster keyed by peer ID;
- copying them verbatim into outbound frames (none of the
  client-emitted frames currently carry a peer ID, but future versions
  may);
- displaying them in diagnostic UI or logs.

**Producer flexibility**

The producer is free to change the format of peer IDs in any release —
length, character set, prefix, encoding — without a protocol version
bump, provided the new format is still a UTF-8 string that satisfies
the equality contract above.

### ClientHello (0x0B)

Sent **once** by the client immediately on connect, **before the server emits
its `Hello`**. Lets the client declare a friendly display name, a default
role hint, and an optional retained-history request.

**Payload:** UTF-8 JSON:

```json
{
  "displayName": "aspire-cli",
  "defaultRole": "secondary",
  "scrollbackHistoryVersion": 1,
  "scrollbackHistoryRows": 10000,
  "commandMarkHistoryVersion": 1
}
```

- `displayName` — Free-form label that other peers see in roster snapshots
  (`Hello.peers[*].displayName`, `PeerJoin.displayName`). May be `null` or
  omitted; the server uses an empty string in that case.
- `defaultRole` — `"primary"` or `"secondary"`. A hint to the consumer's UX
  (e.g. CLI viewers default to `"secondary"` so they don't promote themselves
  to primary on attach). The server does **not** auto-promote on the basis of
  this field in the current iteration; explicit `RequestPrimary` is always
  required. The split is about screen-size following: `"primary"` peers drive
  the producer's PTY dims, `"secondary"` peers follow them. Both are fully
  interactive.
- `scrollbackHistoryVersion` — Optional retained-history extension version.
  Version `1` requests the binary checkpoint described below.
- `scrollbackHistoryRows` — Requested maximum number of physical history rows,
  from 1 through 100,000 when requesting version `1`. Omit **both** history
  fields to disable the extension; do not send a zero-row version-1 request.
- `commandMarkHistoryVersion` — Optional retained OSC 133 command-mark
  extension version, currently `1`. Omit it to disable the request. This is
  independent of scrollback negotiation; screen-backed marks can transfer even
  when text-history transfer is disabled.

The .NET `Hmp1ClientOptions.ScrollbackHistoryRows` setting defaults to 10,000,
accepts 0 through 100,000, and uses `0` to opt out (omitting both JSON fields).
`Hmp1ClientOptions.EnableCommandMarkHistory` defaults to `true`; `false` omits
the command-mark capability field.

A server that does not receive a `ClientHello` frame within a short window of
the connection being established may time out and disconnect the client.

### Hello (0x01)

Sent once by the server after it has received the client's `ClientHello`.

**Payload:** UTF-8 JSON:

```json
{
  "version": 1,
  "width": 80,
  "height": 24,
  "peerId": "p3a1b2c4",
  "primaryPeerId": null,
  "scrollbackHistoryVersion": 1,
  "scrollbackHistoryRows": 10000,
  "commandMarkHistoryVersion": 1,
  "peers": [
    { "peerId": "p9f8e7d6", "displayName": "dashboard" }
  ]
}
```

- `version` — Protocol version (currently `1`). Clients **must** reject unknown versions.
- `width` / `height` — Current PTY dimensions (set by the most recent
  `RequestPrimary` from the current primary, or the producer's configured
  defaults if there is no primary).
- `peerId` — Server-assigned opaque identifier for this client, stable for the
  lifetime of the connection. See the [Peer IDs](#peer-ids) section for
  the full opacity contract.
- `primaryPeerId` — `peerId` of the current primary, or `null` if no peer
  currently holds the primary role.
- `peers` — Roster of *other* peers currently attached (excluding self), each
  with `peerId` and `displayName`.
- `scrollbackHistoryVersion` / `scrollbackHistoryRows` — Present only when
  history was negotiated: version `1` and an accepted positive row limit no
  greater than the client's request. The server acknowledges only a version-1
  request when the producer has scrollback storage and history transfer is
  enabled. An absent acknowledgement means no scrollback transfer, even if the
  client requested history. No scrollback frames may be sent without negotiation.
- `commandMarkHistoryVersion` — Present as `1` only when a version-1 request is
  accepted by an enabled server with an attached terminal. It does not require
  scrollback storage. Without acknowledgement, send no `CommandMarkState` frames.

`Hmp1ServerOptions.EnableScrollbackHistory` and the directly configured
`Hmp1PresentationAdapter.EnableScrollbackHistory` both default to `true`.
For multiple builder listeners sharing one adapter, the first listener's setting
wins, just like adapter-wide callback configuration; it is not per-listener.
Disabling transfer does not disable the producer's own scrollback retention.
`Hmp1ServerOptions.EnableCommandMarkHistory` and
`Hmp1PresentationAdapter.EnableCommandMarkHistory` likewise default to `true`,
with the first builder listener's setting captured for the shared adapter.
Missing or disabled command-mark support does not disable negotiated text history.

### StateSync (0x02)

Sent by the server immediately after the Hello frame. Contains a full snapshot of the current terminal screen as raw ANSI escape sequences, allowing the client to render the current display without waiting for incremental output.

**Payload:** Raw ANSI bytes (UTF-8). Includes clear-screen and cursor positioning sequences.

The payload may be empty if no screen content is available yet.

Viewport cell hyperlinks are replayed with their OSC 8 destinations and parameters,
including links spanning rows or wide characters. The active hyperlink is restored
after painting so subsequent output retains its original link state. Sixel damage
repaint also preserves cell links without changing that active state.

Every `StateSync` is immediately followed by `ActivityState`, including an empty
screen replay. When history is negotiated, `ScrollbackState` and all declared
`ScrollbackRows` immediately follow. If command marks are negotiated,
`CommandMarkState` follows those rows (or directly follows `ActivityState` when
scrollback is not negotiated). The receiver validates the complete
checkpoint before atomically applying screen, activity, history, and command marks as one state
transaction and exposing the connected baseline. This also applies to later
`StateSync` frames. A relay preserves these as control frames, not ordinary live
output. Without either optional negotiation, the transaction remains screen plus activity.

The raw ANSI includes a canonical OSC 9;4 sequence for the current progress
indicator, including explicit clear, so an ANSI-only/native presentation can
restore progress. An authoritative replica suppresses intermediate replay
notifications and uses the checkpoint for its final state. Shell phase is never
reconstructed by emitting OSC 133 marker chains.

If the snapshot contains graphics, the server queues KGP and Sixel replay after
the complete activity/history/command-mark checkpoint and before subsequent live output. The clear-screen sequence in
`StateSync` would erase graphics sent before it. Graphics use separate `Output`
frames because their encoded data may exceed the maximum size of one HMP frame.
An unfinished ANSI parser prefix follows graphics replay and precedes live output,
so a late-attaching replica can finish a fragmented OSC sequence without losing it.

KGP animation replay sends the root and all fully composed frames, timing gaps,
current-frame selection, placements, and playback controls. A subsequent
`KgpAnimationState` frame restores the captured loop progress and frame age.
There is no per-animation-tick retransmission of these pixels.

### ActivityState (0x0D)

This mandatory server-to-client UTF-8 JSON checkpoint is limited to **1,024 bytes**,
with required fields (including explicit nulls):

```json
{
  "progress": { "state": 1, "percentage": 42 },
  "shellIntegration": { "phase": 3, "lastExitCode": -1 }
}
```

Progress states are 0 (none), 1 (normal), 2 (error), 3 (indeterminate), and 4
(warning). States 0 and 3 require a null percentage; the others require an
integer from 0 through 100. Shell phases are 0 (unknown), 1 (prompt),
2 (command line), 3 (executing), and 4 (finished). `lastExitCode` is null or a
signed 32-bit integer; unknown phase requires null. Prompt, command-line, and
executing phases may preserve the latest reported completion result.

Defaults are none/null and unknown/null. Missing, malformed, oversized,
incomplete, or unpaired checkpoints fail the connection, rather than making a
partial screen baseline ready. Unknown or duplicate fields are rejected.

The checkpoint restores **current state**, not command history or execution
events. Identical resyncs do not publish changed-state notifications; later real
OSC 9;4 and OSC 133 output is parsed normally. Browser presentations may coalesce
transitions. Disconnect and workload exit retain the last reported state; they do
not synthesize a command completion or progress clear.

### ScrollbackState (0x0E) and ScrollbackRows (0x0F)

These frames are sent only after successful history negotiation, after **every**
`StateSync` → mandatory `ActivityState` pair. They transfer retained main-buffer
text as inert row data, **not ANSI to execute or replay through the parser**.
No other frame may interleave the header and its declared rows.

**`ScrollbackState` payload:** exactly 8 bytes, two signed little-endian int32s:

| Offset | Field | Meaning |
|--------|-------|---------|
| 0 | Available rows | Producer's retained physical-row count at capture, or `-1` for unavailable |
| 4 | Transferred rows | Exact number of rows in the following chunks |

- `available = -1, transferred = 0` means **unavailable**: do not perform an
  additional history replacement. Explicit history-clearing operations such as
  CSI 3 J or RIS in `StateSync` still have their normal effect. A relay uses this
  when forwarding a checkpoint from an
  upstream peer that did not negotiate history; it must not invent an empty
  authoritative history.
- `available >= 0` replaces local history with the transferred suffix.
  `available = 0, transferred = 0` explicitly **clears** local history.
  Transferred rows cannot exceed available rows or the accepted row limit.
- `available - transferred` exposes the number of producer rows omitted from
  an available checkpoint. Missing text is not replaced with fabricated rows.
  The receiver's storage capacity can reduce the retained suffix further.

Screen and detached history are captured together as a coherent checkpoint.
At capture, select the **newest contiguous suffix**, bounded by the accepted row
limit, **32 MiB** of aggregate `ScrollbackRows` payloads (including each row's
4-byte length, but excluding the 8-byte `ScrollbackState` header), and
**2,000,000 cells**. Byte/cell caps omit the oldest rows.
Emit selected rows in oldest-to-newest order. A single encoded row exceeding
**1 MiB minus 4 bytes**, or invalid/overlong fields, fails explicitly rather than
silently truncating that row.

**`ScrollbackRows` payload:** a nonempty sequence of
`[signed LE int32 rowByteLength][rowData]` entries, at most **1 MiB** per frame.
Lengths are positive; a row cannot cross a chunk boundary. Continue until the
exact transferred count is reached. A zero-count checkpoint has no row frames.
Extra rows, trailing data, malformed fields, missing/truncated chunks, or
interleaved frames fail the handshake or disconnect an established connection.
Do not publish partial screen/activity/history or partially replay history.
After the history rows, read `CommandMarkState` if independently negotiated,
then apply the complete checkpoint. Resume graphics replay, parser continuation,
and normal live output in their existing order.

#### Binary row format (extension version 1)

All multibyte values are little-endian. Each row begins with:

| Field | Encoding | Constraints |
|-------|----------|-------------|
| Original width | signed int32 | 1..16,384 columns |
| Capture timestamp | signed int64 | Valid .NET UTC ticks (100 ns since 0001-01-01 UTC) |
| Cell count | signed int32 | 1..16,384 cells |

Exactly `cellCount` cells follow, in column order. Each cell contains:

| Field | Encoding |
|-------|----------|
| Grapheme/text | String as defined below; an empty string preserves a continuation cell |
| Attributes | uint16 `CellAttributes` flags |
| Wide-wrap padding | byte, `0` or `1` |
| Foreground | Color as defined below |
| Background | Color as defined below |
| Underline color | Color as defined below |
| Underline style | byte, `0`..`5` |
| Hyperlink present | byte, `0` or `1` |
| Hyperlink URI, then parameters | Two strings, only when hyperlink-present is `1` |

A **string** is a signed int32 UTF-8 byte count followed by exactly that many
bytes. Counts are 0..65,536; UTF-8 must be strict/valid. A **color** starts with
a byte tag: `255` = null, `254` = default (neither has following bytes).
Otherwise the tag is `Hex1bColorKind`: `0` RGB, `1` standard, `2` bright,
or `3` indexed, followed by four bytes **R, G, B, palette index**.
RGB requires index `0`; standard/bright indices are 0..7; indexed uses 0..255.
Palette identity and fallback RGB components are retained.

Attribute bits 0..8 are bold, dim, italic, underline, blink, reverse, hidden,
strikethrough, and overline, respectively; bit 10 is soft-wrap and bit 11 is
protected. Bit 9 is retired; it and all other unknown bits are invalid.
Underline styles are none (`0`), single (`1`), double (`2`), curly (`3`),
dotted (`4`), and dashed (`5`). Graphemes, empty continuation cells, soft-wrap
metadata, wide-wrap padding, original row widths, palette colors, underline
details, and hyperlinks survive transfer. These row frames do not transfer
historical graphics or command-mark metadata. Retained OSC 133 command marks and
raw parameters use the separate `CommandMarkState` extension below.
Command text already visible in terminal cells remains ordinary text.

#### Retention, reconnects, and configuration

The receiver must **separately** configure `WithScrollback(capacity)`; negotiation
does not allocate storage or increase its capacity. That local capacity remains
authoritative. Each checkpoint **replaces**, never appends to, retained history,
so reconnects and repeated state synchronization do not duplicate rows. Normal
output afterward accumulates local scrollback as before.

History is installed into the **Hex1b emulator's** configured storage. It does
not hydrate a native terminal presentation's own scrollbar. Raw ANSI consumers
of `ReadOutputAsync` receive ordinary screen/output bytes, not historical rows
converted to ANSI; clients needing only native presentation can set
`ScrollbackHistoryRows = 0` to avoid requesting unused history, and
`EnableCommandMarkHistory = false` to avoid requesting unused command metadata.

Stored main-buffer history is transferred even while the alternate screen is
active. It remains hidden until returning to the main buffer. Alternate-screen
output does not create ordinary main-buffer history.

For example, these configuration snippets assume an existing workload builder
and a connected, ordered bidirectional stream:

```csharp
// Producer builder: retain history and allow negotiated transfer.
producerBuilder
    .WithScrollback(1000)
    .WithHmp1UdsServer("terminal.sock", options =>
        options.EnableScrollbackHistory = true);

// Receiver builder: request up to 10,000 rows, but retain at most 1,000 locally.
replicaBuilder
    .WithScrollback(1000)
    .WithHmp1Stream(stream, options => options.ScrollbackHistoryRows = 10_000);
```

Set the scrollback client option to `0` or the server option to `false` for
screen-only text replay; command marks remain independently negotiable.
With an existing, extension-unaware HMP1 peer, missing negotiation fields
provide the same fallback automatically: no additional history replacement is
performed, and normal screen/output processing determines local retention.
A new replica cannot recover
older producer history in that fallback mode. A direct producer-backed HWT1 view
reads shared retained history without needing this HMP1 extension.

### CommandMarkState (0x10)

This independently negotiated extension retains OSC 133 command marks when a
replica attaches late, reconnects, or resynchronizes. It does **not** change the
HMP1 version. Send exactly one binary `CommandMarkState` after every
`StateSync` → `ActivityState` and any negotiated `ScrollbackState` /
`ScrollbackRows`, before graphics, parser continuation, or live output.
No frame may interleave this checkpoint sequence. Without an acknowledged
`commandMarkHistoryVersion: 1`, neither side sends or expects this frame.

The maximum payload is **8 MiB**. All multibyte values are little-endian,
all integers below are signed, and every boolean is exactly one byte (`0` or
`1`; other values are invalid).

The first byte is **available**. A `0` payload is exactly one byte and means
no additional command-history replacement, as when relaying an unsupported
upstream checkpoint. Normal replay clearing and garbage collection still apply.
A `1` byte is followed by:

| Field | Encoding | Meaning |
|-------|----------|---------|
| History rows | int32 | Exact count of accompanying transferred scrollback rows, otherwise `0` |
| Width | int32 | 1..16,384; must match the current Hello/RoleChange geometry |
| Height | int32 | 1..16,384; must match that geometry |
| Alternate screen active | boolean | Must agree with the screen restored by StateSync |
| Last command ID | int64 | Nonnegative producer ID high-water mark, including IDs whose records were collected |
| Available marks | int32 | Total retained producer command-mark count before history/active-screen eligibility filtering and wire-count clipping |
| Transferred count | int32 | Eligible included count, 0..10,000 and no greater than available marks |

Exactly `transferredCount` mark records follow:

| Field | Encoding | Constraints |
|-------|----------|-------------|
| Command ID | int64 | Positive, unique in this checkpoint, no greater than last command ID |
| Alternate-buffer mark | boolean | May be true only when the active screen is alternate |
| Row | int32 | Zero-based position within the accompanying buffer content |
| Column | int32 | 0..backing-row width (up to 16,384); an end-of-row anchor is allowed |
| Phase | byte | Prompt `1`, command line `2`, executing `3`, finished `4` |
| Exit code present | boolean | May be true only for finished marks |
| Exit code | int32 | Present only when the preceding flag is true |
| Raw parameters present | boolean | Distinguishes null from an empty parameter string |
| Raw parameters | string | Present only when the preceding flag is true |

A parameter string is a signed int32 UTF-8 byte length (0..65,536) followed by
strict UTF-8 bytes. Parameters remain raw OSC 133 metadata, including encoded
command details; importing them does not execute commands or replay OSC chains.

Main-buffer row positions address transferred history first, then the active
main screen. Alternate-buffer positions address only the active alternate
screen. When the alternate screen is active, retained main **history** and its
marks can still transfer; saved main **screen** cells are not transferred, so
marks anchored there are omitted. Rows outside the accompanying content are
never fabricated. Original-width history rows determine their own column bounds.

Capture at most the newest **10,000 eligible** marks by walking backward through
retained records, then emit them **oldest-first**, preserving IDs, with the
screen/history snapshot. Omit marks whose backing text is absent.
`availableMarks - transferredCount` reports omission due to eligibility or count
limits. An oversized payload or parameter string fails explicitly rather than
silently truncating details. A relay sending fewer history rows remaps positions
and drops only marks backed by discarded content; active-screen marks survive.

Validate the entire checkpoint before publication. Malformed flags, geometry,
IDs, positions, strings, counts, trailing bytes, or missing/truncated frames fail
the handshake or disconnect without partial baseline publication. Available
checkpoints **replace**, never append to, the receiver's command history;
an available zero-count checkpoint clears it. Restore IDs and the high-water
mark even when the latest original record was collected, so later live OSC 133
marks continue with aligned IDs. Seeding retained records does not raise
`CommandMarkAdded` events.

Local `Hex1bTerminalOptions.CommandMarkHistoryCapacity` and scrollback capacity
remain authoritative. The command-mark capacity defaults to `int.MaxValue`
(text-lifetime retention); an explicit smaller count evicts oldest marks first
and zero disables retention. This does not change the checkpoint's 10,000-mark
or byte limits. Marks with locally discarded backing text are dropped,
and later redraw, eviction, or reset still collects expired anchors. Repeated
checkpoints do not duplicate retained marks or imply replayed execution events.
Custom/browser-owned markers and historical graphics are **not transferred**.
Missing command-mark negotiation leaves the existing locally observed behavior
without affecting separately negotiated scrollback.

### KgpAnimationState (0x0C)

This server-to-client JSON checkpoint follows the KGP replay `Output` frames,
in the same ordered stream. It supplements standard KGP commands with progress
that those commands cannot express. It is not an HWT1 frame or a new election
mechanism.

```json
{
  "images": [{
    "imageId": 1,
    "imageNumber": 0,
    "currentFrameNumber": 2,
    "playbackState": 3,
    "maximumLoops": 1,
    "completedLoops": 0,
    "elapsedTicks": 800000
  }]
}
```

Each entry addresses an already-replayed image by `imageId`, or by
`imageNumber` with `imageId: 0` for numbered images. `currentFrameNumber` is
one-based. `playbackState` is numeric: `1` stopped, `2` loading, `3` running.
`maximumLoops` and `completedLoops` restore the image store's loop counters;
`maximumLoops: 1` denotes infinite playback. `elapsedTicks` is the captured
frame age in 100-nanosecond ticks, or null when its presentation time is not
initialized.

Hex1b applies the checkpoint **after** the preceding pixel/control bytes have
been interpreted, never from the network reader ahead of queued output.
The checkpoint must reference valid replayed frames and playback counters;
invalid checkpoints fail instead of silently resetting the animation.
Byte-only consumers still receive standard KGP playback commands but do not
restore this additional progress. Captured frame age is restored relative to
the consumer's clock; this is not a transport-latency compensation or
cross-peer wall-clock synchronization guarantee.

### Output (0x03)

Incremental terminal output from the server's workload (e.g., PTY process). Sent continuously as the workload produces output.

**Payload:** Raw ANSI bytes (UTF-8).

For a terminal-backed producer, this is a **rendering stream**, not a byte-for-byte
capture of workload output. The producer answers supported terminal queries using
its own capabilities and state, even when no clients are connected. It consumes
DA1 (`CSI c` / `CSI 0 c`), XTWINOPS reports (`CSI 14/16/18 t`), supported status
and cursor-position reports (`CSI 5/6 n`), and recognized Kitty query commands
before broadcasting output. Clients must not answer these queries again.

State-changing Kitty commands still reach viewers, with their quiet control set
to `q=2` to suppress downstream success and error acknowledgements. The producer
processes the original quiet control and returns the appropriate response to the
workload. Other output retains its original bytes; unsupported or malformed
commands are not covered by this response-ownership guarantee.

Projection is streaming across Output boundaries. A partially received query is
not seeded into a newly attached client's parser; ordinary incomplete sequences
retain their projected continuation. CSI and Kitty control headers are bounded
to 64 KiB; exceeding the limit fails output processing with an error rather than
silently truncating the header. Graphics payloads are streamed, not buffered by
this header limit.

Hex1b HMP1 workload adapters also declare upstream ownership, suppressing locally
generated replies in replicas. This protects Hex1b replicas receiving queries
from older producers, but an external raw terminal can still answer queries
forwarded by an older producer. Upgrade the producer to prevent those replies.
A transport-only `Hmp1PresentationAdapter` with no attached `Hex1bTerminal`
continues to forward bytes unchanged: it has no terminal responsible for replies.

> **Important:** Output frames are stateful — ANSI escape sequences build on previous state (colors, cursor position, modes). Dropping or reordering Output frames will cause visual corruption. If a client falls behind, it should be disconnected and reconnected (which triggers a fresh Hello + StateSync).

### Input (0x04)

Keyboard input from the client to the server.

**Payload:** Raw input bytes (UTF-8). May include ANSI escape sequences for special keys (arrows, function keys, etc.).

### Resize (0x05)

Terminal resize notification. Bidirectional but with **asymmetric** semantics:

- **Client → Server:** A peer requests that the underlying workload be resized
  to its local dimensions. The server applies the resize **only if the sending
  peer is the current primary** (see `RequestPrimary` and the role state
  machine below). If the peer is not primary the frame is silently dropped
  (logged at debug level) and the workload's dimensions are unchanged.
- **Server → Client (broadcast):** The producer's PTY was resized (either by
  the primary's `Resize` or by a `RequestPrimary` from any peer). The producer
  echoes the new dimensions to **all** peers including the sender so every
  client treats the producer as the single source of truth for current
  dimensions.

**Payload:** 8 bytes:

```
+-------+--------+
| Width | Height |
| 4B LE | 4B LE  |
+-------+--------+
```

- `Width` — Terminal width in columns (4 bytes, little-endian)
- `Height` — Terminal height in rows (4 bytes, little-endian)

### Exit (0x06)

Sent by the server when the terminal session has ended (workload exited).

**Payload:** 4 bytes:

```
+----------+
| ExitCode |
| 4B LE    |
+----------+
```

- `ExitCode` — Process exit code (4 bytes, little-endian, signed)

### RequestPrimary (0x07)

Sent by a peer to ask the server to make this peer the **primary** at the
given dimensions. The server always grants the request in this iteration —
the previous primary (if any) is demoted, the PTY is resized to the requested
dimensions, and a `RoleChange` frame is broadcast to all peers (including the
new primary).

**Payload:** UTF-8 JSON:

```json
{
  "cols": 120,
  "rows": 40
}
```

- `cols` / `rows` — Dimensions the requester wants the producer to drive at.
  These become the new PTY size on success.

### RoleChange (0x08)

Broadcast by the server to all attached peers when the primary changes — both
on a successful `RequestPrimary` and on the involuntary "primary disconnected"
transition.

**Payload:** UTF-8 JSON:

```json
{
  "primaryPeerId": "p3a1b2c4",
  "width": 120,
  "height": 40,
  "reason": "RequestPrimary"
}
```

- `primaryPeerId` — `peerId` of the new primary, or `null` if the previous
  primary disconnected and no new primary has taken over.
- `width` / `height` — Current PTY dimensions after the role change.
- `reason` — Free-form short string indicating why the role changed
  (`"RequestPrimary"` or `"PrimaryDisconnected"` in this iteration).

### PeerJoin (0x09)

Broadcast by the server to all **other** attached peers when a new peer
joins the session. The newly joined peer learns about the existing roster
via its own `Hello.peers[]`, not via `PeerJoin`.

**Payload:** UTF-8 JSON:

```json
{
  "peerId": "pe5fefc1",
  "displayName": "aspire-cli"
}
```

### PeerLeave (0x0A)

Broadcast by the server to all remaining peers when a peer disconnects.
If the leaving peer was the primary, a `RoleChange` (with `primaryPeerId:
null`, `reason: "PrimaryDisconnected"`) is broadcast **before** `PeerLeave`,
so observers see the role transition in causal order.

**Payload:** UTF-8 JSON:

```json
{
  "peerId": "pe5fefc1"
}
```

## Connection Sequence

```
Client                              Server
  |                                    |
  |  ─ ─ ─ ClientHello ─ ─ ─ ─ ─ ─ →|  Display name + default role hint
  |  ← ─ ─ Hello ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ |  version + dims + peerId + roster
  |  ← ─ ─ StateSync ─ ─ ─ ─ ─ ─ ─ ─ |  Full screen snapshot
  |  ← ─ ─ ActivityState ─ ─ ─ ─ ─ ─ |  Mandatory activity baseline
  |  ← ─ ─ ScrollbackState ─ ─ ─ ─ ─ |  Only if negotiated
  |  ← ─ ─ ScrollbackRows ─ ─ ─ ─ ─ ─|  Zero or more chunks, exact row count
  |  ← ─ ─ CommandMarkState ─ ─ ─ ─ ─|  Only if independently negotiated
  |  ← ─ ─ Output / KgpAnimationState |  Graphics, then parser continuation
  |                                    |
  |  ← ─ ─ Output ─ ─ ─ ─ ─ ─ ─ ─ ─ |  Incremental output
  |  ─ ─ ─ Input ─ ─ ─ ─ ─ ─ ─ ─ ─ →|  Keyboard input
  |  ← ─ ─ Output ─ ─ ─ ─ ─ ─ ─ ─ ─ |
  |                                    |
  |  ─ ─ ─ RequestPrimary ─ ─ ─ ─ ─→|  Take control at local dims
  |  ← ─ ─ RoleChange (broadcast) ─ ─|  Reflects new primary + dims
  |  ← ─ ─ Resize (broadcast) ─ ─ ─ ─|  Echo of accepted dims
  |  ─ ─ ─ Resize ─ ─ ─ ─ ─ ─ ─ ─ →|  As primary, push dim updates
  |  ← ─ ─ Resize (broadcast) ─ ─ ─ ─|  Server echoes accepted dims
  |                                    |
  |  ← ─ ─ PeerJoin (broadcast) ─ ─ ─|  A new peer attached
  |  ← ─ ─ PeerLeave (broadcast) ─ ─ ─|  A peer disconnected
  |                                    |
  |  ← ─ ─ Exit ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ |  Workload has exited
  |                                    |
```

1. Client establishes a bidirectional stream (e.g., connects to a Unix domain socket).
2. Client sends **ClientHello** with its display name, optional role hint, and
   optional scrollback and command-mark requests.
3. Server replies with **Hello** carrying the protocol version, current PTY
   dimensions, the assigned `peerId`, the current `primaryPeerId`, and the
   roster of other attached peers, plus independent history/command-mark acknowledgements.
4. Server sends **StateSync** with the full current text screen content, followed
   by mandatory **ActivityState**, then negotiated **ScrollbackState** and
   **ScrollbackRows**, and independently negotiated **CommandMarkState**.
   The complete baseline is validated and atomically applied.
   Ordered graphics **Output** frames and, for KGP animation,
   **KgpAnimationState** checkpoints follow, then any parser continuation.
5. Normal operation: **Output** flows server → client; **Input** flows client → server.
6. To take control of the PTY size, a peer sends **RequestPrimary**. The
   server applies the resize and broadcasts **RoleChange**, including the
   accepted dimensions, to all peers.
7. While primary, a peer may send **Resize** frames; the server applies them
   and broadcasts the accepted dimensions back to all peers.
8. Roster changes are broadcast via **PeerJoin** / **PeerLeave**.
9. When the workload exits, the server sends an **Exit** frame and closes the stream.

## Multi-Client Behavior — Primary / Secondary

A single server can serve multiple clients simultaneously. Each client receives
its own `Hello + StateSync + ActivityState` and negotiated history/command marks on connection.
**Output** is multicast to all
connected clients. **Input** from any client is forwarded to the workload
without arbitration (so multiple peers may type into the same PTY at once —
that's a UX concern, not a protocol concern).

**Protocol replies.** The original producer is the answerer, not the primary
client. Attachment count, role changes, and disconnects do not transfer that
responsibility. The primary can change the producer's dimensions, so subsequent
size reports reflect those dimensions, but viewer capabilities do not replace
the producer's capability model. Ordinary input from secondaries remains enabled.

**Resize policy.** Exactly one peer at a time may hold the **primary** role.
Only the primary can drive the PTY's dimensions. There is no implicit primary;
the producer starts with `primaryPeerId = null` and a configurable default
PTY size, and the workload runs at those defaults until some peer explicitly
sends `RequestPrimary`.

```
   ┌─────────┐  RequestPrimary{cols, rows}    ┌────────────┐
   │  null   │ ─────────────────────────────► │  primary = │
   │ (no     │                                │  sender    │
   │ primary)│ ◄───────────────────────────── │            │
   └─────────┘   primary disconnects          └────────────┘
                                                    │ ▲
                                                    │ │ RequestPrimary
                                                    │ │ from another peer
                                                    │ │ (always granted —
                                                    │ │ producer demotes
                                                    │ │ old primary)
                                                    ▼ │
                                              ┌────────────┐
                                              │  primary = │
                                              │  new sender│
                                              └────────────┘
```

State machine:

- **No primary → Primary** on `RequestPrimary` from any peer. Producer applies
  the requested dimensions to the PTY and broadcasts `RoleChange` +
  echoed `Resize`.
- **Primary → Different primary** on `RequestPrimary` from another peer.
  Producer always grants. Old primary is demoted to secondary. Broadcasts
  `RoleChange` + echoed `Resize`.
- **Primary → No primary** when the current primary disconnects. PTY size
  is **not** reset; whatever dimensions were last applied stay in effect.
  Producer broadcasts `RoleChange { primaryPeerId: null,
  reason: "PrimaryDisconnected" }` followed by `PeerLeave`.

A `Resize` frame from a non-primary peer is silently dropped server-side
(no NACK frame in this iteration; consider adding `Status` in a future
revision).

A peer with `defaultRole: "secondary"` is **not** auto-promoted on first attach;
the role hint is purely a UX signal. Explicit `RequestPrimary` is always
required.

This iteration's policy is intentionally simple: every `RequestPrimary` is
granted. Future iterations may add (a) capability negotiation or take-over
denial driven by the *current* primary, (b) an activity guard that denies
take-overs within N seconds of input from the current primary, and (c) a
`Status` / NACK frame for explicit rejection.

## Transport

The protocol is transport-agnostic and works over any reliable, ordered, bidirectional byte stream:

- **Unix domain sockets** (recommended for local use)
- **TCP sockets**
- **Named pipes**
- **WebSocket** (via a stream adapter)

Security (encryption, authentication) is the transport's responsibility. Use TLS, SSH tunnels, or other transport-level security as needed.

## Versioning

The protocol version is communicated in the Hello frame. Clients **must** reject connections with an unsupported version. The current version is **1**.

> **Pre-v1 in-place updates.** Hex1b is pre-v1 and HMP1 has been extended in
> place rather than versioned. The `version` field still reads `1` after the
> multi-head primary/secondary additions; the protocol identity has not
> changed, but those baseline changes were incompatible with prior implementations.
> The optional scrollback extension instead uses capability fields in
> `ClientHello` and `Hello`. It preserves compatibility with the current
> mandatory-`ActivityState` baseline: without acknowledgement, neither peer
> expects history frames. It does not make pre-`ActivityState` peers compatible.

Future versions may add:
- Additional negotiated capabilities in the Hello frame
- Terminal mode replay (mouse tracking, bracketed paste, etc.) in StateSync
- An activity guard or NACK / `Status` frame for primary handoff
- Compression for Output frames

## Changelog

- **(optional, HMP1 version unchanged)** Independently negotiated retained OSC 133
  command marks, version `1`, with `CommandMarkState (0x10)`. Preserves raw details,
  status, phases, positions, IDs, and the ID high-water mark without replaying
  command events; custom markers remain view-owned.
- **(optional, HMP1 version unchanged)** Negotiated retained-text scrollback
  version `1`, with `ScrollbackState (0x0E)` and bounded binary
  `ScrollbackRows (0x0F)` following each activity checkpoint. Missing capability
  fields retain screen-only behavior with current HMP1 peers.
- **(in-place, pre-v1)** Multi-head primary / secondary roles. Adds
  `RequestPrimary (0x07)`, `RoleChange (0x08)`, `PeerJoin (0x09)`,
  `PeerLeave (0x0A)`, and `ClientHello (0x0B)`. Extends `Hello` payload
  with `peerId`, `primaryPeerId`, and `peers`. Tightens `Resize` semantics:
  client → server `Resize` is silently dropped from non-primary peers, and
  the server echoes accepted dimensions to **all** peers including the sender
  so the producer is the single source of truth.
- **(in-place, pre-v1)** Terminal mode replay in `StateSync`: mouse tracking,
  focus events, bracketed paste, `DECTCEM`, `DECCKM`, `DECKPAM`, `DECSCUSR`,
  mouse encoding plus alt-screen `DECSET` ordered before cell repaint. (See
  `Hmp1Protocol` `BuildStateSync*` helpers.)
- **(in-place, pre-v1)** KGP animation replay restores composed frames, gaps,
  current frame, and playback controls, followed by `KgpAnimationState (0x0C)`
  for completed-loop counters and captured frame age. Late viewers can advance
  a silent producer's animation without a per-tick output stream.
