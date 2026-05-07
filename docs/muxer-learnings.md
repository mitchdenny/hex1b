# Building a multi-headed terminal client on Hex1b — design learnings

This doc captures the design decisions and gotchas we hit while building
`samples/WebMuxerDemo` — a producer process plus a browser viewer plus a
TUI/CLI viewer, all sharing one PTY through HMP1. It's intended as an
implementation guide for the next consumer (Aspire's `aspire terminal
<resource>` experience). The framing is "what HMP1 / `Hmp1WorkloadAdapter` /
the Hex1b widgets give you for free" vs "what you have to build yourself".

The companion doc [`muxer-protocol.md`](./muxer-protocol.md) covers the wire
format. Read that first.

---

## TL;DR

- **The protocol gives you a transport, not a client.** HMP1 frames + the
  `Hmp1WorkloadAdapter` deliver bytes, role events, and dimension updates.
  Everything else — render-mode selection, host SIGWINCH re-broadcast,
  bounded teardown, layout, and the chord UX — is your problem.
- **The producer is the long-lived "session". Clients come and go.** Discover
  producers by well-known UDS paths. The client must always start in viewer
  mode and let the user opt into the primary role explicitly.
- **Subscribe to adapter events instead of polling for producer dims.**
  `Connected`, `RoleChanged`, `RemoteResized`, `PeerJoined`, `PeerLeft`,
  and `Disconnected` cover everything HMP1 surfaces. Configure them
  declaratively via `WithHmp1UdsClient(path, opt => …)` /
  `WithHmp1Stream(stream, opt => …)` so the wiring runs before the first
  frame is read. Host-side dims (TTY SIGWINCH) you still poll from
  `Console.WindowWidth/Height` because they're not in scope of HMP1.
- **`TerminalWidget` is greedy and transparent by default.** Pin it with
  `FixedWidth`/`FixedHeight` so `Align` can centre it, and call
  `.Background(color)` so it doesn't bleed through whatever container is
  underneath.

---

## 1. The architectural shape

```
┌──────────────────────────────────────────────────────────────────────┐
│  Producer process (long-lived "serve")                               │
│  ┌──────────────────────┐    ┌──────────────────────┐                │
│  │  Hex1bTerminal (PTY) │ -> │  Hex1bMuxerHost      │ <- UDS sockets │
│  │  bash/pwsh/dotnet… │    │  (HMP1 server)       │                │
│  └──────────────────────┘    └──────────────────────┘                │
└─────────────────────────────────────┬────────────────────────────────┘
                                      │ N concurrent clients
                  ┌───────────────────┼─────────────────────────────┐
                  │                   │                             │
        ┌─────────▼────────┐ ┌────────▼─────────┐ ┌─────────────────▼──────┐
        │ Browser (xterm)  │ │ TUI/CLI viewer   │ │ aspire terminal <res>  │
        │ via WebSocket    │ │ via UDS          │ │ (future)               │
        └──────────────────┘ └──────────────────┘ └────────────────────────┘
```

**Process boundaries that matter:**

- The **producer** is the source of truth for the PTY. It owns the
  `Hex1bTerminal` (which owns the child process) and the muxer host.
  It's the only process that survives across client connect/disconnect cycles.
- A **client** is a thin shell around `Hmp1WorkloadAdapter` plus rendering
  glue. It can come and go any time without affecting the workload.
- Clients are peers, not in any hierarchy. The protocol elects exactly
  one of them at a time as the **primary** — that peer's host dimensions
  drive the producer's PTY size. All other peers are **viewers** that
  observe whatever the primary's dims produce.

For Aspire, the producer is most naturally a DCP-managed sidecar (one per
terminal-enabled resource), the dashboard is the browser client, and
`aspire terminal <resource>` is the TUI/CLI client. Producer lifetime is
tied to the resource's lifecycle, not any individual client.

---

## 2. What HMP1 + the Hex1b builder give you

The easy path is `WithHmp1UdsClient(...)` / `WithHmp1Stream(...)` /
`WithHmp1Client(...)` with an `Action<Hmp1ClientOptions>` callback.
Inside the callback you set hooks; the builder constructs the
underlying adapter for you and delivers an `IHmp1ConnectionHandle`
to your `OnConnected` hook. You should never need to type
`Hmp1WorkloadAdapter` for normal consumer code.

`IHmp1ConnectionHandle` (in `Hex1b`) is the runtime surface for
multi-head consumers:

| Surface | Notes |
|---|---|
| `LocalDisplayName` | The display name we sent in `ClientHello` (auto-generated base36 if you didn't set one). |
| `DefaultRole` | The `Hmp1Role?` (Primary/Secondary) we declared at handshake — a hint, not authoritative. |
| `RemoteWidth`, `RemoteHeight` | Producer's current PTY dims. Updated as `Resize` frames arrive. |
| `IsPrimary` | True iff this peer currently holds the role. |
| `Peers` | Roster of other peers (excluding self). Updated on `PeerJoin`/`PeerLeave`. |
| `Connected` event | Fires once after the handshake. Carries `Connection` (the handle), `PeerId`, `PrimaryPeerId`, `Peers`, and the producer's current dims. |
| `RoleChanged` event | Fires on every `RoleChange` broadcast — even ones that don't affect us — and always carries the current dims. |
| `RemoteResized` event | Fires when the producer's PTY dims change — either because we requested it as primary or because some other peer is now driving them. **Use this instead of polling `RemoteWidth/Height`.** |
| `PeerJoined` / `PeerLeft` | Roster churn. |
| `Disconnected` event | Producer socket closed or the read pump errored. |
| `RequestPrimaryAsync(w, h, ct)` | The single mutation primitive. Re-broadcasts the new dims even if you're already primary — see §4.3. |

The corresponding `Hmp1ClientOptions.On*` delegates are 1:1 wrappers
that fire alongside the events — wire either, not both, or you'll get
double dispatch. Options-callback delegates are the easy path; events
are useful when you need to attach handlers later than `Build()` time.

The under-the-hood `Hmp1WorkloadAdapter` class implements
`IHmp1ConnectionHandle` directly. Reach for it only in the **advanced
two-step pattern** (see §4.4) where you need the connection established
before the surrounding `Hex1bTerminal` is built.

Things HMP1 does **not** give you:

- A way to "ask" the producer for the current state outside of `Hello`.
  `StateSync` is server-pushed at well-defined moments (e.g. role change),
  not on demand.
- A "resize without taking primary" call. If you want the producer's PTY
  to follow your dims, you have to be primary.
- Any concept of "session name". Names live in your discovery layer
  (UDS path / DNS / DCP catalog) and never appear on the wire.
- Peer ids with structure. Treat them as opaque — see
  [muxer-protocol.md](./muxer-protocol.md) §"Peer ids".

---

## 3. The three render modes

A multi-head client has three visual states that depend on `IsPrimary` and
on whether the producer's grid fits in the host:

| Mode | Condition | What you render |
|---|---|---|
| **primary** | `IsPrimary == true` | Embedded terminal at the producer's dims (which equal the host's, modulo chrome). User input flows. |
| **viewer-fit** | `!IsPrimary && producer ≤ host` | Embedded terminal centred in the host. Read-only. |
| **viewer-too-small** | `!IsPrimary && producer > host` | A "doesn't fit" message offering to take control or detach. The terminal is hidden. |

`WebMuxerDemo`'s browser client doesn't have the third mode because the
browser can scale a `<canvas>` arbitrarily. The TUI client always has all
three because the host TTY's grid is fixed and not under your control.

The mode transitions you have to handle:

1. Initial state: viewer or "doesn't fit" depending on producer dims at
   handshake time.
2. `RoleChanged` → recompute mode (and inner terminal grid — see §4.1).
3. Producer `Resize` without role change → recompute "fits" check.
4. Host SIGWINCH while we're primary → re-broadcast new dims (§4.3).
5. Host SIGWINCH while we're a viewer → recompute "fits" check.

You'll find yourself wanting a single `Render()` callback that:

- Reads `_adapter.IsPrimary`, `_adapter.RemoteWidth/Height`,
  `Console.WindowWidth/Height`.
- Picks one of the three sub-trees.
- Wraps it with the `InfoBar` and the chord input bindings.

That's the shape of `CliViewerApp.Render()` in `WebMuxerDemo`.

---

## 4. State sync: events vs polling

Producer dims are event-driven (`Connected`, `RoleChanged`,
`RemoteResized`). Host TTY dims and the embedded terminal's grid are
not — Hex1b currently exposes no Resized event for either, so you check
`Console.WindowWidth/Height` from `Render()` and reconcile against your
own `_innerWidth/_innerHeight` locals. Your sync loop runs there.

### 4.1 Embedded terminal grid

The embedded `Hex1bTerminal` has its own grid dims, and `Hex1bTerminal`
doesn't expose them. Track them locally:

```csharp
private int _innerWidth;
private int _innerHeight;

private void EnsureInnerSize(int w, int h)
{
    if (_innerWidth == w && _innerHeight == h) return;
    _embedded?.Resize(w, h);
    _innerWidth = w;
    _innerHeight = h;
}
```

Call `EnsureInnerSize(_adapter.RemoteWidth, _adapter.RemoteHeight)` once
per render. It's an O(1) compare; the resize is a no-op when sizes
already match. This catches silent producer `Resize` frames that arrived
without a `RoleChange` (e.g. the current primary resized their host).

### 4.2 Producer dim drift via `RemoteResized` (and `RoleChanged`)

Two events tell you when the producer's PTY dims changed:

- **`RemoteResized`** — fires whenever the dims actually moved, regardless
  of whether the role transition that triggered it touched us. Use this
  as your primary subscription point: wire its handler to
  `EnsureInnerSize(e.Width, e.Height)` and `Invalidate()`. Carries
  `CausedByLocalPrimary` so you can suppress your own resize echo if
  needed.
- **`RoleChanged`** — fires on every `RoleChange` broadcast (including
  ones with unchanged dims). Use this to recompute the render mode
  (primary vs viewer) and to reset re-broadcast trackers — see §4.3.

Both events are wired declaratively through the options callback:

```csharp
IHmp1ConnectionHandle? connection = null;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHmp1UdsClient(socketPath, opt =>
    {
        opt.DisplayName     = "viewer-cli";
        opt.DefaultRole     = Hmp1Role.Secondary;
        opt.OnConnected     = e =>
        {
            connection = e.Connection;             // grab the handle for hotkeys
            EnsureInnerSize(e.Width, e.Height);
        };
        opt.OnRemoteResized = e => { EnsureInnerSize(e.Width, e.Height); _app?.Invalidate(); };
        opt.OnRoleChanged   = e => OnRoleChanged(e);
        opt.OnPeerJoined    = _ => _app?.Invalidate();
        opt.OnPeerLeft      = _ => _app?.Invalidate();
    })
    .WithScrollback()
    .WithTerminalWidget(out var handle)
    .Build();

// later, from a hotkey handler:
await connection!.RequestPrimaryAsync(width, height, ct);
```

If you need the connection established **before** the surrounding
`Hex1bTerminal` is built — for example because you want to seed
`.WithDimensions(adapter.RemoteWidth, adapter.RemoteHeight)` from the
producer's reported size — drop down to the advanced two-step pattern
in §4.4. That's the only reason to type `Hmp1WorkloadAdapter` yourself.

### 4.3 Host SIGWINCH while we're primary

This is the subtlest case. When the user resizes the host TTY (Windows
Terminal, iTerm2, tmux pane), the producer's PTY does **not** automatically
follow — you have to broadcast a new `RequestPrimary` frame.

We do it from `Render()`:

```csharp
if (isPrimary && (availW != _lastBroadcastWidth || availH != _lastBroadcastHeight))
{
    BroadcastResize(availW, availH);
}
```

`BroadcastResize` records the target up-front (so subsequent renders
don't loop) and uses a single-flight gate so SIGWINCH bursts (typical of
mouse-drag resizing) collapse to one in-flight call:

```csharp
private int _resizeInFlight; // 0 = idle, 1 = a request is in flight

private void BroadcastResize(int width, int height)
{
    _lastBroadcastWidth = width;
    _lastBroadcastHeight = height;

    if (Interlocked.CompareExchange(ref _resizeInFlight, 1, 0) != 0) return;

    _ = Task.Run(async () =>
    {
        try { await _adapter.RequestPrimaryAsync(width, height, CancellationToken.None); }
        catch { /* best-effort */ }
        finally
        {
            Volatile.Write(ref _resizeInFlight, 0);
            _app?.Invalidate();
        }
    });
}
```

The final `Invalidate()` ensures that if the host kept resizing while we
were broadcasting, the next render fires another broadcast. The drift
check at the top of `Render()` means we re-broadcast iff the target
moved.

When you lose the role (someone else took primary), reset the trackers to
`-1` so a future re-take triggers a fresh sync:

```csharp
private void OnRoleChanged(object? sender, RoleChangedEventArgs e)
{
    EnsureInnerSize(e.Width, e.Height);
    if (!_adapter.IsPrimary)
    {
        _lastBroadcastWidth = -1;
        _lastBroadcastHeight = -1;
    }
    _app?.Invalidate();
}
```

And after a successful `TakeControl`, seed the trackers so the first
post-take render doesn't double-broadcast:

```csharp
await _adapter.RequestPrimaryAsync(availW, availH, CancellationToken.None);
_lastBroadcastWidth = availW;
_lastBroadcastHeight = availH;
```

**Asymmetry warning.** Without this re-broadcast, host *shrink* tends to
"look like it works" because `FixedWidth`/`FixedHeight` clipping happens
to mask the missing resize. Host *grow* exposes the bug — the terminal
stays pinned at the old dims with empty padding around it. Test both
directions explicitly.

### 4.4 The advanced two-step pattern (direct adapter)

The easy path constructs the adapter for you, but it can only hand you
the connection **after** the surrounding `Hex1bTerminal` has been
built and `RunAsync` is in flight (via `OnConnected`). A few scenarios
need the connection earlier — typically because they want to seed
`.WithDimensions(adapter.RemoteWidth, adapter.RemoteHeight)` from the
producer's reported size before `Build()`. For those cases, drop down
to the explicit pattern:

```csharp
var adapter = new Hmp1WorkloadAdapter(new Hmp1ClientOptions
{
    StreamFactory   = ct => Hmp1Transports.ConnectUnixSocket(socketPath, ct),
    StreamTransform = DemoTls.AuthenticateAsClientAsync, // optional
    DefaultRole     = Hmp1Role.Secondary,
});
await adapter.ConnectAsync(CancellationToken.None);

EmbeddedTerminal = Hex1bTerminal.CreateBuilder()
    .WithDimensions(adapter.RemoteWidth, adapter.RemoteHeight)
    .WithWorkload(adapter)               // adapter implements IHex1bTerminalWorkloadAdapter
    .WithScrollback()
    .WithTerminalWidget(out var handle)
    .Build();

// adapter implements IHmp1ConnectionHandle, so runtime calls work the same:
await adapter.RequestPrimaryAsync(width, height, ct);
```

This is the same wire shape as the easy path; the only difference is
that you own `ConnectAsync` and the adapter reference. It's how
`MuxerDemo`, `EncryptedMuxerDemo`, and the `Hex1b.Tests` white-box
suite stitch their workloads together — and how you'd write a real
viewer that needs the producer's dims before it knows its own grid.

There is deliberately **no** `WithWorkload(IHmp1ConnectionHandle)`
overload and **no** `Hmp1Connection.ConnectAsync(...)` static helper.
If you're calling `WithWorkload`, you've opted into the workload
abstraction; the adapter is the right type to pass.

---

## 5. Hex1b widget gotchas

### 5.1 `TerminalWidget.MeasureCore` is greedy

`TerminalNode.MeasureCore` returns the full bounded constraint regardless
of the handle's current grid dims. Inside an `Align`/`Center` widget this
means the terminal claims the full bounded area and `Align` centring
becomes a no-op — the grid paints at top-left with blank padding.

Fix: pin the widget to its grid dims so `AlignNode` can layout it
correctly:

```csharp
ctx.Align(Alignment.Center,
    ctx.Terminal(handle)
       .FixedWidth(handle.Width)
       .FixedHeight(handle.Height))
   .Fill();
```

### 5.2 `TerminalWidget` is transparent by default

`TerminalNode.Render` clears its bounds with `null`-bg cells, and
`RenderRow` emits no SGR-48 code when `cell.Background == null`. During
surface composition any container painted underneath
(`BackgroundPanelWidget`, theme global background) bleeds through every
default-bg cell, making the terminal indistinguishable from its frame.

Fix (added during this work): call `.Background(color)` on `TerminalWidget`
to give it its own opaque surface inside the terminal's bounds. The
surrounding container colour shows only in the framing area outside the
terminal.

```csharp
ctx.Terminal(handle)
   .Background(Hex1bColor.FromRgb(0x0d, 0x11, 0x17))  // terminal bg
   .FixedWidth(handle.Width)
   .FixedHeight(handle.Height);
```

Match this against the parent `BackgroundPanelWidget` colour and you get
the "terminal card sitting in a panel" idiom that the web client uses.

### 5.3 `Align`/`Center` only expand if you `.Fill()` them

`AlignWidget` returns the child's natural size in `MeasureCore`, so
without a hint it shrinks to the child. Wrap with `.Fill()` so it
expands to the parent and the child is actually centred in real estate:

```csharp
ctx.Align(Alignment.Center, child).Fill();
```

The same applies to `Center(...)`.

### 5.4 `VStack(body, infoBar)` for "InfoBar at the bottom"

`InfoBar` takes its natural one-row height. Make `body` use `.Fill()` so
the VStack hands it all the remaining space and the InfoBar lands on the
last row of the host — including in the "doesn't fit" sub-tree, which
otherwise renders the panel mid-screen with the InfoBar awkwardly
attached.

### 5.5 No public Resized event on `Hex1bTerminal` / `Hex1bApp`

The internal `_presentation.Resized` is hooked privately. Only public
events on `Hex1bTerminal` are `WindowTitleChanged` / `IconNameChanged`.
For both host-side SIGWINCH and producer-side dim updates you have to
poll inside `Render()`. See §4.

---

## 6. Adapter lifecycle gotchas

### 6.1 Use the new `ConnectAsync` CT contract for handshake bounds

`Hmp1WorkloadAdapter.ConnectAsync(ct)` honours `ct` for the handshake
only. Once the handshake completes successfully, the read pump is
governed by `DisposeAsync` instead of the supplied CT. This means the
"obvious" pattern is also the right one:

```csharp
using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(appCt);
handshakeCts.CancelAfter(TimeSpan.FromSeconds(5));
await adapter.ConnectAsync(handshakeCts.Token);   // bounded by 5s
// read pump now runs until DisposeAsync — not until handshakeCts fires.
```

(Older drafts of this doc warned against passing a short-lived CT here
because the read pump used to capture it. Fixed in the multihead PR
together with the rest of the API clean-up; the warning is preserved
here as historical context.)

### 6.2 `DisposeAsync()` can hang on the read pump

If the read pump is mid-read when you dispose, `DisposeAsync()` waits for
it. Always wrap teardown in a bounded `Task.WhenAny` so a wedged
producer doesn't hang your client process:

```csharp
var disposeTask = adapter.DisposeAsync().AsTask();
await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(2)));
```

The trade-off — a stuck read pump leaks a thread for the rest of the
process lifetime — is acceptable for a CLI client that's about to exit
anyway. For a long-running consumer (a daemon hosting many adapters),
the upstream Hex1b `DisposeAsync` may need a hard cancellation path.

### 6.3 Always start in viewer mode

Even if you're "the only client", connect as a viewer and let the user
explicitly take primary. This preserves the multi-active-client property:
two CLI viewers can sit side-by-side observing the same producer, and
either one can `take` whenever they want. Auto-promoting on connect
breaks this.

---

## 7. Local discovery via well-known UDS paths

For a single-machine producer + clients setup, the simplest discovery
mechanism is a well-known directory:

| OS | Convention used in `WebMuxerDemo` |
|---|---|
| Windows | `%USERPROFILE%\.hex1bsamples\webmuxerdemo\<session>.sock` |
| macOS / Linux | `~/.hex1bsamples/webmuxerdemo/<session>.sock` |

The producer creates the file at start; clients enumerate the directory
to list available sessions and connect by filename. UDS path length
limits (~108 bytes on Linux, less on macOS) make this slightly fragile —
for Aspire we'll likely need a DCP-mediated catalog instead, with the
UDS path opaque to the user.

Stale sockets from crashed producers need a one-time `connect()` probe
to detect (the path remains in the filesystem after process exit on most
platforms).

---

## 8. UX glue you'll write every time

These are not technical challenges, but they're consistent across all
muxer clients:

### 8.1 Chord prefix instead of bare hotkeys

A muxer client forwards keystrokes to the producer in primary mode, so
single-key shortcuts collide with normal input. Use a tmux-style chord
(`Ctrl+B` then a letter). `Ctrl+B` is convention; **avoid `Ctrl+H`** —
it's ASCII 0x08 (Backspace) and the input pipeline collapses both
sequences before the chord matcher sees them.

In `WebMuxerDemo`:

- `Ctrl+B T` — take primary
- `Ctrl+B D` — detach (close the client without affecting the producer)

### 8.2 Persistent InfoBar at the bottom

Even in viewer mode, show role / peer count / dims somewhere visible.
Users need to know whether they're driving the PTY or just watching, and
how many other clients are connected. `WebMuxerDemo` puts this in a
single-row `InfoBar` pinned to the bottom of the host.

### 8.3 "Take control" UX

In primary mode the producer's PTY is sized to your host. Be explicit
about this in the UI ("Press Ctrl+B T to take control — resizes producer
to your terminal"). It's the only feedback the user gets that their
action will affect other connected clients.

---

## 9. Notes for the Aspire retrofit

Mapping `WebMuxerDemo` onto Aspire:

| `WebMuxerDemo` | Aspire equivalent |
|---|---|
| `serve` process | DCP-managed terminal sidecar, one per terminal-enabled resource |
| Session name | Resource name |
| `~/.hex1bsamples/webmuxerdemo/<name>.sock` | DCP-mediated UDS path (opaque to the user) |
| Web viewer | Aspire Dashboard `/terminal/{resource}` |
| `webmuxerdemo connect --session <name>` | `aspire terminal <resource>` |
| `Ctrl+B` chord | Same — keep the convention |

Things to address that `WebMuxerDemo` doesn't have:

1. **Auth.** UDS file mode 0600 is enough for local same-user; if the
   dashboard talks remotely, terminate TLS in front of the muxer.
2. **Session lifecycle tied to resource lifecycle.** When the resource
   stops, the producer should drop existing connections cleanly and not
   accept new ones. Reuse the producer's `Exit` frame (`0x06`) for this.
3. **Producer scrollback survival across client connections.** The
   producer keeps the `Hex1bTerminal` running, so scrollback persists by
   default. Confirm this is the desired UX (some teams expect "fresh
   terminal per connection" instead).
4. **Dashboard ↔ CLI symmetry on take-primary.** Both clients should
   show clearly when *another* client has taken primary, so the user
   doesn't think the dashboard or CLI is broken when keystrokes stop
   working.
5. **`RequestPrimaryAsync` while already primary.** We rely on this as
   the resize broadcast path, but it's an undocumented behaviour of the
   server today. If we make it official, document it in
   [`muxer-protocol.md`](./muxer-protocol.md). If we don't, add a
   dedicated "BroadcastResize" frame and update `Hmp1WorkloadAdapter`.

---

## 10. References

- [`muxer-protocol.md`](./muxer-protocol.md) — wire format
- [`samples/WebMuxerDemo/Cli/CliViewerApp.cs`](../samples/WebMuxerDemo/Cli/CliViewerApp.cs) —
  reference implementation of every pattern in this doc
- [`samples/WebMuxerDemo/Cli/CliViewerCommand.cs`](../samples/WebMuxerDemo/Cli/CliViewerCommand.cs) —
  UDS discovery + handshake timeout + bounded teardown
- [`src/Hex1b/Widgets/TerminalWidget.cs`](../src/Hex1b/Widgets/TerminalWidget.cs) —
  the `Background()` API
- [`src/Hex1b/Nodes/TerminalNode.cs`](../src/Hex1b/Nodes/TerminalNode.cs) —
  `MeasureCore` greediness and `FillBackground` interaction
