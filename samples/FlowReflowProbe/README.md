# FlowReflowProbe

A small interactive probe that validates whether the host terminal cooperates
with the design proposed for fixing Hex1b's Flow-app resize behaviour.

## Why this exists

Hex1b Flow apps render in the **normal buffer** (not the alternate buffer)
because they want to preserve the user's shell prompt above and feed
completed-step output ("tombstones") into the terminal's scrollback. This
puts the runner in a tracking bind: when the user resizes the terminal,
content reflows, but the runner has no way to query "where did my old
content end up" — standard VT only lets us query the cursor position, not
arbitrary buffer state.

The current shipped behaviour is "tombstones are append-only, active step
is bottom-anchored, accept any visual overlap on shrink." The next step
of the design relies on a single observable: **does the host terminal
move the cursor with reflowed logical lines?** If yes, the runner can
query the cursor with DSR (`ESC[6n`) immediately after a resize event
and recover the post-reflow tombstone-bottom row.

`src/Hex1b/Reflow/` already encodes per-terminal reflow strategies for
the **emulator side** of the codebase. The probe surfaces that knowledge
as an empirical visual check: it predicts what your terminal should do
based on Hex1b's recorded behaviour, then asks you to confirm by eye.

## How to run

```powershell
dotnet run --project samples/FlowReflowProbe
```

## What the screen shows

- A header with the auto-detected reflow strategy and what to expect.
- A long single logical line (the "tombstone") that should reflow on
  horizontal resize.
- A bright YELLOW reference line directly past the tombstone — the
  cursor is positioned at column 0 of this line on every redraw.
- Five MAGENTA "step row" lines below, written with cell positioning,
  representing what an active flow step looks like on screen.

## What to do

1. Note where the cursor block sits (should be at column 0 of the
   YELLOW reference line).
2. Resize the terminal **horizontally** — shrink several columns,
   then expand back. Do NOT press R yet.
3. Watch:
   - **Cursor moves with the YELLOW reference line.** ✓ Terminal
     reflows the cursor along with logical content. The DSR-anchor
     design is achievable on this terminal.
   - **Cursor drifts away from the YELLOW line.** ✗ Terminal does
     not reflow the cursor (or reflow misbehaves). DSR-anchor cannot
     give an exact answer here; we'd have to fall back to the
     current bottom-anchor design.
   - **MAGENTA `[step row N]` lines stay at the same terminal rows
     they started at.** ✓ Cell-positioned content does not reflow.
   - **MAGENTA lines wrap or relocate.** ✗ Cell-positioned content
     is also being moved by the terminal — more invasive design
     would be needed.
4. Press `R` to redraw the full layout at the new size (useful after
   you've inspected what changed). Press `Q` to quit.

## What the predictions are based on

The classifier in `Program.cs` mirrors the strategies in
`src/Hex1b/Reflow/`:

| Strategy                         | Reflows logical lines | Cursor follows | DECSC reflowed | Anchors cursor row |
|----------------------------------|-----------------------|----------------|----------------|--------------------|
| `WindowsTerminalReflowStrategy`  | Yes                   | Yes            | No             | No (bottom-fill)   |
| `ITerm2ReflowStrategy`           | Yes                   | Yes            | No             | No (bottom-fill)   |
| `KittyReflowStrategy`            | Yes                   | Yes            | No             | Yes                |
| `WezTermReflowStrategy`          | Yes                   | Yes            | No             | Yes                |
| `GhosttyReflowStrategy`          | Yes                   | Yes            | Yes            | Yes                |
| `VteReflowStrategy`              | Yes                   | Yes            | Yes            | Yes                |
| `FootReflowStrategy`             | Yes                   | Yes            | Yes            | Yes                |
| `XtermReflowStrategy`            | No                    | N/A            | N/A            | N/A                |
| `AlacrittyReflowStrategy`        | No                    | N/A            | N/A            | N/A                |

If a terminal shows behaviour different from what the table predicts,
that's a finding worth capturing — either Hex1b's recorded strategy is
out of date, or the terminal's behaviour has changed across versions.

## What this probe is NOT

- It does not actually use DSR programmatically. The "does the cursor
  follow reflow" check is purely visual. A future enhancement could
  add a programmatic DSR roundtrip.
- It does not exercise the active-step render path of Flow apps. It
  only validates the underlying terminal behaviour that the next
  iteration of the resize fix would depend on.
