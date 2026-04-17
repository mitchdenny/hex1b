# Hex1b Muxer Protocol (HMP) v1

The Hex1b Muxer Protocol is a binary framing protocol for multiplexing terminal sessions over any bidirectional byte stream (Unix domain sockets, TCP, named pipes, etc.).

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
| Hello | `0x01` | Server → Client | Initial handshake with protocol version and dimensions |
| StateSync | `0x02` | Server → Client | Full screen snapshot (raw ANSI) |
| Output | `0x03` | Server → Client | Incremental terminal output (raw ANSI) |
| Input | `0x04` | Client → Server | Keyboard input (raw bytes) |
| Resize | `0x05` | Bidirectional | Terminal dimensions changed |
| Exit | `0x06` | Server → Client | Terminal session has ended |

### Hello (0x01)

Sent once by the server immediately after a client connects.

**Payload:** UTF-8 JSON:

```json
{
  "version": 1,
  "width": 80,
  "height": 24
}
```

- `version` — Protocol version (currently `1`). Clients **must** reject unknown versions.
- `width` — Current terminal width in columns.
- `height` — Current terminal height in rows.

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

Terminal resize notification. Can be sent in both directions:

- **Client → Server:** The client's terminal was resized. The server may use this to resize the underlying workload (e.g., send `SIGWINCH` to a PTY process).
- **Server → Client:** The server's terminal dimensions changed (e.g., due to another client's resize).

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

## Connection Sequence

```
Client                              Server
  |                                    |
  |  ← ─ ─ Hello ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ |  Protocol version + dimensions
  |  ← ─ ─ StateSync ─ ─ ─ ─ ─ ─ ─ ─ |  Full screen snapshot
  |  ─ ─ ─ Resize ─ ─ ─ ─ ─ ─ ─ ─ → |  Client's local dimensions
  |                                    |
  |  ← ─ ─ Output ─ ─ ─ ─ ─ ─ ─ ─ ─ |  Incremental output
  |  ─ ─ ─ Input ─ ─ ─ ─ ─ ─ ─ ─ ─ →|  Keyboard input
  |  ← ─ ─ Output ─ ─ ─ ─ ─ ─ ─ ─ ─ |
  |  ─ ─ ─ Resize ─ ─ ─ ─ ─ ─ ─ ─ →|  Client resized
  |  ← ─ ─ Resize ─ ─ ─ ─ ─ ─ ─ ─ ─ |  Server confirms new dimensions
  |                                    |
  |  ← ─ ─ Exit ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ |  Workload has exited
  |                                    |
```

1. Client establishes a bidirectional stream (e.g., connects to a Unix domain socket).
2. Server sends **Hello** frame with the protocol version and current terminal dimensions.
3. Server sends **StateSync** frame with the full current screen content.
4. Client sends a **Resize** frame with its local terminal dimensions (so the server can resize the workload if needed).
5. Normal operation: **Output** and **Input** frames flow bidirectionally. **Resize** frames can be sent by either side at any time.
6. When the workload exits, the server sends an **Exit** frame and closes the stream.

## Multi-Client Behavior

A single server can serve multiple clients simultaneously. Each client receives its own Hello + StateSync on connection. Output is multicast to all connected clients. Input from any client is forwarded to the workload.

**Resize policy:** When multiple clients are connected, the server uses the dimensions from the most recent Resize frame received from any client ("last wins"). Future protocol versions may add leader election.

## Transport

The protocol is transport-agnostic and works over any reliable, ordered, bidirectional byte stream:

- **Unix domain sockets** (recommended for local use)
- **TCP sockets**
- **Named pipes**
- **WebSocket** (via a stream adapter)

Security (encryption, authentication) is the transport's responsibility. Use TLS, SSH tunnels, or other transport-level security as needed.

## Versioning

The protocol version is communicated in the Hello frame. Clients **must** reject connections with an unsupported version. The current version is **1**.

Future versions may add:
- Capability negotiation in the Hello frame
- Terminal mode replay (mouse tracking, bracketed paste, etc.) in StateSync
- Leader election for multi-client resize
- Compression for Output frames
