# WebMuxerDemo

Demonstrates HMP1 (Hex1b Muxer Protocol v1) including the multi-head
primary / secondary protocol via two transports:

- **WebSocket** — browser tabs render the session via a vendored xterm.js KGP
  validation build and a pure-JS HMP1 client
  (`wwwroot/js/hmp1-client.js`).
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
- **One dedicated `kgp-bash` session on Unix.** This makes the graphics
  smoke test deterministic even when the user's login shell is zsh or fish.

## Kitty graphics path

The browser uses a matched, vendored snapshot of `@xterm/xterm`,
`@xterm/addon-fit`, and `@xterm/addon-image` from
[xterm.js PR #6098](https://github.com/xtermjs/xterm.js/pull/6098). This lets
the demo validate the PR's KGP placement lifecycle changes without mixing a
locally patched addon with CDN builds of core xterm.js and addon-fit. The
browser assets, exact source commit, package versions, and hashes are recorded
in `wwwroot/vendor/README.md`; the upstream MIT license is preserved alongside
them. Return to the upstream npm packages after the changes are released.

The end-to-end path is:

```text
xterm.js ImageAddon
  ↕ Kitty APC bytes and capability replies
WebSocket (binary HMP1 frames)
  ↕
Hex1b HMP1 presentation adapter
  ↕ raw PTY output/input
bash
```

The addon is loaded before `Terminal.open()` and the WebSocket connection.
It consumes Kitty APC sequences from `term.write(...)`; its capability and
status replies flow back through `term.onData(...)` to bash. The WebSocket
proxy and HMP1 output path preserve the original PTY bytes.

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

### KGP smoke test

1. Select `kgp-bash` and click **Connect**.
2. Click **KGP Smoke Test**. The browser sends a Bash `printf` command through
   HMP1; bash emits a two-by-two RGBA Kitty image back through the PTY, Hex1b,
   HMP1, and the WebSocket. xterm.js should display a 16-by-8-cell colored
   image.
3. For the full Hex1b widget exercise, take control and run this from the
   repository root:

   ```bash
   dotnet run --project samples/KgpDemo
   ```

   If the server was launched after `cd samples/WebMuxerDemo`, use
   `dotnet run --project ../KgpDemo` instead.

   In `KgpDemo`, choose **File > xterm.js Safe**. These entries use generated
   images and scaled copies of the sample photos. Every image is at most
   640-by-480 pixels and below 2 MiB of decoded RGBA data. They open in a
   modal preview. Drag the title bar to move it or the borders to resize it;
   Hex1b transmits the image once and updates its placement as the window
   changes. Press **Escape** to close the preview. The original
   generated-image and full-resolution photo entries remain available for
   native Kitty terminals and placement stress testing.

The full demo also verifies capability negotiation: `KgpDemo` sends Hex1b's
Kitty query, the browser addon returns `OK` through `term.onData`, and Hex1b
then selects its KGP widgets instead of their text fallbacks.

Known constraints of this prototype:

- Only direct transmission (`t=d`) is usable in a browser. File, temporary
  file, and shared-memory transmission refer to backend resources the browser
  cannot access.
- The vendored addon supports multiple named placements, placement replacement,
  and targeted deletion. Unicode placeholders, relative placement, animation,
  and several less common deletion selectors remain unsupported.
- HMP1 incremental output preserves Kitty sequences, but its current
  `StateSync` snapshot contains ANSI cell state rather than image payloads.
  Connect before displaying an image; a newly attached or role-switched viewer
  will not reconstruct an already-displayed image until the workload repaints.
- The demo caps one Kitty payload at 8 MiB, one image at roughly four million
  pixels, and image storage at 64 MiB to limit browser memory exposure.

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
