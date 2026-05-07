# WebMuxerDemo

Demonstrates HMP1 (Hex1b Muxer Protocol v1) including the multi-head
primary / secondary protocol via two transports:

- **WebSocket** — browser tabs render the session via xterm.js + a pure-JS
  HMP1 client (`wwwroot/js/hmp1-client.js`).
- **Unix domain socket** — `webmuxerdemo connect` is a TUI viewer that
  attaches to a session over UDS, demonstrating the same multi-head
  protocol from a real terminal.

This is the smallest possible end-to-end exerciser of the Phase 10
multi-head additions across both consumer shapes.

## Architecture

```
[xterm.js tab #1]    [xterm.js tab #2]    [`webmuxerdemo connect` (TUI)]
     │ WS (HMP1)         │ WS (HMP1)           │ UDS (HMP1)
     ▼                   ▼                     ▼
[ASP.NET WebSocketProxy] x N                   │
     │ UDS                                     │
     └────────────┬────────────────────────────┘
                  ▼
[Hex1bTerminal "shell" — PTY workload + WithHmp1UdsServer]
                  │
                  ▼
                [bash / pwsh / cmd]
```

- **One `Hex1bTerminal` per session.** Owns a real PTY-backed shell and
  serves HMP1 over a Unix domain socket.
- **One `WebSocketProxy` per browser tab.** Pumps raw bytes between the
  WebSocket and the upstream UDS connection. Because HMP1 frames flow
  through unmodified, the producer's multi-head roster + role frames
  reach the browser transparently.
- **One TUI viewer per `webmuxerdemo connect`.** Connects directly to the
  per-session UDS — no WebSocket hop. Built on `Hex1bApp` with an
  embedded inner `Hex1bTerminal` rendering the live session.

## Discovery

The serve process exposes one UDS per session under a well-known root:

```
~/.hex1bsamples/webmuxerdemo/
    shell.sock
    cmd.sock
```

The TUI viewer enumerates `*.sock` in that directory to list sessions.
There is no registry file or PID tracking — filesystem-as-discovery,
matching the MuxerDemo sample's pattern.

## Running

### Web server (browser viewers)

```bash
cd samples/WebMuxerDemo
dotnet run
# or, equivalently:
dotnet run -- serve --urls "http://localhost:5198"
```

Open <http://localhost:5198> in two browser tabs against the same session
to see the multi-head behaviour:

- Each tab gets its own `peerId` from the producer.
- The peers panel shows both tabs.
- Click "Take Control" in either tab to become the primary (resizes the
  PTY to that tab's xterm dimensions).
- Open a second session (different shell) by changing the picker to
  exercise multi-session.

### TUI viewer

In a second shell, with the serve running:

```bash
# List discoverable sessions
dotnet run --project samples/WebMuxerDemo -- connect

# Attach as a viewer (no auto-promotion)
dotnet run --project samples/WebMuxerDemo -- connect --session shell

# Optionally label yourself in the multi-head roster
dotnet run --project samples/WebMuxerDemo -- connect \
    --session shell --display-name viewer-A
```

#### TUI hotkeys

The viewer uses tmux-style chord prefixes so chord keys don't conflict
with input forwarded to the embedded terminal when in primary mode.

| Chord     | Action                                                              |
|-----------|---------------------------------------------------------------------|
| `Ctrl+B T`| Take control: become primary at your current host terminal dims.    |
| `Ctrl+B D`| Detach and exit cleanly.                                            |

#### Render modes

Re-evaluated on every render (host SIGWINCH, `RoleChanged`, `PeerJoined`,
`PeerLeft`, producer `Resize` broadcast):

| Condition                                          | Display                                  |
|----------------------------------------------------|------------------------------------------|
| You hold the primary role                          | Embedded terminal at producer dims.      |
| You're a viewer **and** producer dims fit yours    | Embedded terminal (live secondary view). |
| You're a viewer **and** producer dims exceed yours | Centered "doesn't fit" panel offering to take control. |

### Multi-head end-to-end

```bash
# Terminal 1 — serve
dotnet run --project samples/WebMuxerDemo

# Terminal 2 — TUI viewer A
dotnet run --project samples/WebMuxerDemo -- connect --session shell --display-name viewer-A

# Terminal 3 — TUI viewer B (try a smaller window than viewer A)
dotnet run --project samples/WebMuxerDemo -- connect --session shell --display-name viewer-B

# Browser — open http://localhost:5198, pick `shell`
```

Now exercise the protocol:

- Both TUIs and the browser tab see `peers: 3` in their roster UI.
- Take control from viewer A (`Ctrl+B T`). Producer PTY resizes to A's
  dims; B and the browser observe `RoleChange`. If B is smaller than A,
  B switches to the "doesn't fit" panel.
- Take control from viewer B. A and browser observe `RoleChange`. If A
  is smaller than B's new dims, A switches to "doesn't fit".
- Detach a primary (`Ctrl+B D`) — producer goes to `primaryPeerId: null`
  but keeps last-known dims. Other peers can take over.

## What this proves

This demo is a working prototype of two pieces of the parent Aspire
`WithTerminal` workstream:

1. **Phase 12 — dashboard architecture.** The browser path mirrors what
   the dashboard would host: WebSocket-tunneled HMP1, with the JS
   client here being the same code shape that would land in
   `src/Aspire.Dashboard/wwwroot/js/xterm/hmp1-addon.ts` later (ported
   to TypeScript and packaged as an xterm.js addon).
2. **CLI viewer architecture.** The `connect` subcommand is a direct
   ancestor of `aspire terminal <resource>`: same UDS-based discovery
   shape (`*.sock` files in a known dir), same `Hex1bApp`-with-embedded
   -terminal pattern, same multi-head hotkey UX. The Aspire CLI will
   discover sockets through the AppHost backchannel instead, but the
   render and input plumbing is identical.

