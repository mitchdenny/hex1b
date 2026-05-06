# WebMuxerDemo

Demonstrates HMP1 (Hex1b Muxer Protocol v1) over a **WebSocket** transport,
including the multi-head primary / secondary protocol. This is the smallest
possible end-to-end exerciser of the Phase 10 multi-head additions.

## Architecture

```
[xterm.js tab #1]                [xterm.js tab #2]
     │ WS (HMP1)                       │ WS (HMP1)
     ▼                                 ▼
[ASP.NET WebSocketProxy]         [ASP.NET WebSocketProxy]
     │ UDS                             │ UDS
     └────────────┬────────────────────┘
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
- **Browser speaks HMP1 directly** via `wwwroot/js/hmp1-client.js` — a
  ~300 LOC pure-JS port of the protocol.

## Running

```bash
cd samples/WebMuxerDemo
dotnet run
```

Open <http://localhost:5198> in two browser tabs against the same session
to see the multi-head behaviour:

- Each tab gets its own `peerId` from the producer.
- The peers panel shows both tabs.
- Click "Take Control" in either tab to become the primary (resizes the
  PTY to that tab's xterm dimensions).
- Open a second session (different shell) by changing the picker to
  exercise multi-session.

## What this proves

This demo is a working prototype of the Phase 12 architecture from the
parent Aspire `WithTerminal` workstream: the dashboard would host a
similar setup where the browser speaks HMP1 over WebSocket and the
server side bridges to the terminal host's HMP1 UDS endpoint.

The JavaScript HMP1 client here is the same code shape that would land
in `src/Aspire.Dashboard/wwwroot/js/xterm/hmp1-addon.ts` later (just
ported to TypeScript and packaged as an xterm.js addon).
