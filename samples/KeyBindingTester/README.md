# KeyBindingTester

A manual portability harness: a one-test-at-a-time checklist of expected
key/mouse combinations that ticks off as the user presses each one.

## Why

Hex1b's unit tests cover the *internal* dispatch path with synthetic
`Hex1bKeyEvent`s — they prove the wiring is correct, but they can't prove
that any *given terminal emulator* actually delivers the bytes the wiring
expects. Different terminals (and operating systems) make different choices
about what they emit for `Ctrl+Shift+A`, `Alt+Ctrl+F5`, `Shift+Click`, and
so on, and many terminals **intercept** common combos for their own
features (see "Combos commonly intercepted" below).

Run this sample on every terminal you target — Windows Terminal, ConPTY,
iTerm2, GNOME Terminal, xterm, ssh sessions from PuTTY/MobaXterm, etc. —
to gather portability data per-platform.

## How

```bash
dotnet run --project samples/KeyBindingTester
```

The sample shows **one expected combo at a time** with a progress counter.
Press the combo to advance to the next test. Hot-keys:

| Key            | Action                                                     |
|----------------|------------------------------------------------------------|
| (the combo)    | Pass — advance to the next test                            |
| `F`            | **Fail** — this combo doesn't fire on this terminal        |
| `S`            | Skip — advance without testing (no judgement recorded)     |
| `R`            | Reset the checklist                                        |
| `C`            | Copy the markdown report to the clipboard                  |
| `Esc`/`Ctrl+C` | Exit                                                       |

`F` and `S` are different on purpose: `F` records "I tried this and the
terminal/OS does not deliver the bytes" — that's the data the harness exists
to gather. `S` records "I haven't tested this" — useful when you want to
focus on a specific category. The report distinguishes them.

When all tests are complete, you'll see a results screen with a
**Copy report to clipboard** button and a **Restart** button. The report
is also printed to stdout on exit, so you can copy it from the terminal
scrollback even if your terminal doesn't support OSC 52 clipboard access.

If you press a tested combo while waiting for a different one, the screen
shows `(You pressed X — still waiting for Y.)` so you can confirm input is
being received.

## What's tested

~50 combos across:

- **Arrows** — full modifier matrix on `←` (8 combos), plus `→/↑/↓` with
  `Shift`, `Ctrl`, and `Ctrl+Shift` (the matrix that drove issue #293)
- **Navigation** — `Home`, `End`, `PageUp`, `PageDown` × modifiers
- **Function keys** — `F1`, `F12` × modifiers
- **Editing** — `Backspace`, `Delete` × `Ctrl`
- **Letters** (with `†` caveat) — `Ctrl+A`, `Ctrl+Shift+A`, `Ctrl+Z`,
  `Ctrl+Shift+Z` — most terminals can't deliver `Ctrl+Shift+letter`
- **Mouse** — `Left`/`Right` click and `Scroll Up`/`Down` × modifiers

> Note on `Ctrl+Backspace`: at the byte level the standard ANSI/xterm
> protocol cannot distinguish `Ctrl+Backspace` from `Ctrl+H` — both are
> `0x08`. Most terminals send `0x08` for `Ctrl+Backspace` and `0x7F` for
> plain `Backspace`. A Hex1b binding on `Hex1bKey.Backspace + Control`
> therefore won't fire on a standard terminal. The kitty keyboard
> protocol disambiguates this; supporting it is a separate piece of
> work. The `Ctrl+Backspace` row is included in this harness so you can
> see the failure on your terminal explicitly.

## Letter-key caveat (the † rows)

Most terminals cannot distinguish `Ctrl+Shift+letter` from `Ctrl+letter`:
applying `Ctrl` strips ASCII bit 6 of the letter, and `Shift` gets dropped
entirely. So a binding like `Ctrl+Shift+A` may never fire on a standard
xterm/VT-mode terminal — that's a terminal limitation, not a Hex1b bug.

For terminals that support `xterm`'s `modifyOtherKeys=2` mode (most modern
ones), `Ctrl+Shift+letter` does deliver explicitly — so a missing tick on
those rows tells you whether `modifyOtherKeys` is engaged on your terminal.

Special keys (arrows, Fn, `Home`/`End`/`PgUp`/`PgDn`) deliver `Ctrl+Shift`
reliably because their CSI sequences carry an explicit modifier code. Mouse
buttons in SGR mode also include explicit modifier bytes.

## Combos commonly intercepted by terminals/OSes

If a tested combo never fires, the terminal or OS is likely intercepting it
before the app sees the bytes. See **[Keybinding Portability](https://hex1b.dev/guide/keybinding-portability)**
on hex1b.dev for the authoritative per-terminal interception matrix
(Windows Terminal, ConPTY, macOS Terminal.app, iTerm2, Ghostty, kitty,
GNOME Terminal, Ptyxis, xterm, tmux, ssh — verified against each terminal's
defaults). A short summary:

| Terminal / OS                | Intercepted combos                                  |
| ---------------------------- | --------------------------------------------------- |
| Windows Terminal (default)   | `Alt+Shift+arrow` (resize pane), `Ctrl+Shift+↑/↓` (scroll), `Ctrl+Shift+Home/End` (scroll-to), `Ctrl+Shift+T/N/W/D/F/P/M/A/C/V/K` (UI), `Shift+Click` / `Ctrl+Shift+Click` (extend selection — non-configurable) |
| Windows OS                   | `Alt+Space` (system menu), `Alt+F4` (close), `Win+<key>` (OS-reserved) |
| macOS Terminal.app / iTerm2  | All `Cmd+*` (Hex1b never sees `Cmd`); `Option+<key>` produces glyphs unless "Use Option as Meta" is enabled |
| Ghostty (Linux/Windows)      | `Ctrl+Shift+T/N/W/F/C/V/Q/I/O/E/Enter`, `Alt+1..9` |
| kitty                        | All `Ctrl+Shift+*` (the default `kitty_mod`) — change `kitty_mod` to free them |
| GNOME Terminal / Ptyxis      | `Ctrl+Shift+T/N/W/F/C/V`, `Ctrl+PgUp/PgDn` (tab switch) |
| GNOME shell                  | `Ctrl+Alt+arrow` (workspace switch on some distros)  |
| tmux                         | All combos sent through the prefix key (default `Ctrl+B`) |

You can usually disable terminal-side bindings in the terminal's settings
to free those combos for the app — but if you ship Hex1b widgets that rely
on combos in the table above, document the interception risk in your app's
help text.

Press **`F`** to record that the combo doesn't fire on this terminal — the
report will mark it as `❌ failed (terminal/OS does not deliver this combo)`.
Use **`S`** instead if you simply want to skip without recording a judgement
(e.g. you're focusing on a specific category).
