# Hex1b Muxer Protocol (HMP) v1

The Hex1b Muxer Protocol is a binary framing protocol for multiplexing terminal sessions over any bidirectional byte stream (Unix domain sockets, TCP, named pipes, etc.).

> **In-place update — no protocol version bump.** Hex1b is pre-v1 and currently
> has a single coordinated consumer (Aspire). HMP1 has been extended in-place
> with multi-head primary/secondary semantics and four new frame types
> (`RequestPrimary`, `RoleChange`, `PeerJoin`, `PeerLeave`) plus an extended
> `Hello` payload and a new client-emitted `ClientHello`. Old binaries cannot
> speak the updated HMP1 — all builds upgrade together.

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
- generate or invent peer IDs (the only field a client supplies in
  `ClientHello` is an optional human-readable `displayName`);
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
its `Hello`**. Lets the client declare a friendly display name and a default
role hint. Both fields are optional.

**Payload:** UTF-8 JSON:

```json
{
  "displayName": "aspire-cli",
  "defaultRole": "viewer"
}
```

- `displayName` — Free-form label that other peers see in roster snapshots
  (`Hello.peers[*].displayName`, `PeerJoin.displayName`). May be `null` or
  omitted; the server uses an empty string in that case.
- `defaultRole` — `"viewer"` or `"interactive"`. A hint to the consumer's UX
  (e.g. CLI viewers default to `"viewer"` so they don't promote themselves to
  primary on attach). The server does **not** auto-promote on the basis of
  this field in the current iteration; explicit `RequestPrimary` is always
  required.

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

### StateSync (0x02)

Sent by the server immediately after the Hello frame. Contains a full snapshot of the current terminal screen as raw ANSI escape sequences, allowing the client to render the current display without waiting for incremental output.

**Payload:** Raw ANSI bytes (UTF-8). Includes clear-screen and cursor positioning sequences.

The payload may be empty if no screen content is available yet.

### Output (0x03)

Incremental terminal output from the server's workload (e.g., PTY process). Sent continuously as the workload produces output.

**Payload:** Raw ANSI bytes (UTF-8).

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
2. Client sends **ClientHello** with its display name and (optional) role hint.
3. Server replies with **Hello** carrying the protocol version, current PTY
   dimensions, the assigned `peerId`, the current `primaryPeerId`, and the
   roster of other attached peers.
4. Server sends **StateSync** with the full current screen content.
5. Normal operation: **Output** flows server → client; **Input** flows client → server.
6. To take control of the PTY size, a peer sends **RequestPrimary**. The
   server applies the resize, broadcasts **RoleChange** to all peers, and
   broadcasts **Resize** carrying the accepted dimensions.
7. While primary, a peer may send **Resize** frames; the server applies them
   and broadcasts the accepted dimensions back to all peers.
8. Roster changes are broadcast via **PeerJoin** / **PeerLeave**.
9. When the workload exits, the server sends an **Exit** frame and closes the stream.

## Multi-Client Behavior — Primary / Secondary

A single server can serve multiple clients simultaneously. Each client receives
its own `Hello + StateSync` on connection. **Output** is multicast to all
connected clients. **Input** from any client is forwarded to the workload
without arbitration (so multiple peers may type into the same PTY at once —
that's a UX concern, not a protocol concern).

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

A peer with `defaultRole: "viewer"` is **not** auto-promoted on first attach;
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
> changed, but the wire format is incompatible with prior implementations.
> A future iteration may either bump to `version: 2` or introduce a
> capability-negotiation field — to be decided when there are two consumer
> codebases that need coordination.

Future versions may add:
- Capability negotiation in the Hello frame
- Terminal mode replay (mouse tracking, bracketed paste, etc.) in StateSync
- An activity guard or NACK / `Status` frame for primary handoff
- Compression for Output frames

## Changelog

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
