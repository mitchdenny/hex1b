# Keybinding portability across terminals

> **TL;DR.** Hex1b can fire any binding the terminal actually delivers. But many
> terminals (and operating systems) intercept popular combos before the app sees
> a single byte — and a few combos can't be encoded by the standard ANSI
> protocol at all. This page lists the known offenders **per terminal** so you
> can pick bindings that work everywhere your app ships.
>
> Want to confirm what your terminal does? Run the
> [`KeyBindingTester`](https://github.com/mitchdenny/hex1b/tree/main/samples/KeyBindingTester)
> sample.

## Why combos go missing

Three independent layers can swallow a key before Hex1b sees it:

1. **The terminal emulator.** Most terminals own a small set of UI shortcuts
   (`Ctrl+Shift+T` for new tab, `Ctrl+Shift+C` for copy, `Ctrl+Shift+↑`/`↓` for
   scroll, etc.). When you press an intercepted combo, the terminal handles it
   and sends *nothing* to the running app.
2. **The OS / window manager.** Windows reserves `Win+<key>`, GNOME may steal
   `Ctrl+Alt+<arrow>` for workspace switching, the Mac dock owns `Cmd+<...>`
   entirely. macOS doesn't even forward `Cmd` to terminal apps under any
   circumstance.
3. **The ANSI protocol itself.** A few combos are *fundamentally
   indistinguishable* in the byte stream:
   - `Ctrl+Backspace` and `Ctrl+H` are both `0x08`.
   - `Ctrl+Shift+<letter>` and `Ctrl+<letter>` are both the same control byte
     (`Ctrl` strips ASCII bit 6; `Shift` then has no effect).
   - `Ctrl+I` and `Tab` are both `0x09`; `Ctrl+M` and `Enter` are both `0x0D`.

   The [kitty keyboard protocol](https://sw.kovidgoyal.net/kitty/keyboard-protocol/)
   solves all of these by sending explicit `keypress` events with full modifier
   bitmasks, but it must be enabled per-terminal and Hex1b doesn't yet speak
   it (tracked separately).

Special keys (arrows, function keys, `Home`/`End`/`PgUp`/`PgDn`) carry their
modifiers explicitly in CSI parameters — those *are* unambiguous on the wire
and only fail when a terminal/OS intercepts them.

## Windows Terminal

Windows Terminal's [default `keybindings`](https://github.com/microsoft/terminal/blob/main/src/cascadia/TerminalSettingsModel/defaults.json)
intercept the following combos. Bindings can be cleared/remapped in
**Settings → Actions** (or by editing your `settings.json`); for combos you
need in your Hex1b app, instruct users to add `{ "command": null, "keys": "..." }`
to free the keys.

### Keyboard

| Combo                 | Windows Terminal action                 | Workaround                            |
| --------------------- | --------------------------------------- | ------------------------------------- |
| `Alt+Shift+←/→/↑/↓`   | Resize active pane                      | Unbind `Terminal.ResizePane*`         |
| `Alt+←/→/↑/↓`         | Move pane focus                         | Unbind `Terminal.MoveFocus*`          |
| `Ctrl+Alt+←/→`        | Move focus previous/next                | Unbind `Terminal.MoveFocusPrevious`   |
| `Ctrl+Shift+↑/↓`      | Scroll up/down by line                  | Unbind `Terminal.ScrollUp`/`Down`     |
| `Ctrl+Shift+PgUp/PgDn`| Scroll up/down by page                  | Unbind `Terminal.ScrollUpPage`/`DownPage` |
| `Ctrl+Shift+Home/End` | Scroll to top/bottom of buffer          | Unbind `Terminal.ScrollToTop`/`Bottom`|
| `Ctrl+Shift+T`        | New tab                                 | Unbind `Terminal.OpenNewTab`          |
| `Ctrl+Shift+W`        | Close pane                              | Unbind `Terminal.ClosePane`           |
| `Ctrl+Shift+N`        | New window                              | Unbind `Terminal.OpenNewWindow`       |
| `Ctrl+Shift+D`        | Duplicate tab                           | Unbind `Terminal.DuplicateTab`        |
| `Ctrl+Shift+F`        | Find                                    | Unbind `Terminal.FindText`            |
| `Ctrl+Shift+P`        | Command palette                         | Unbind `Terminal.ToggleCommandPalette`|
| `Ctrl+Shift+M`        | Mark mode                               | Unbind `Terminal.ToggleMarkMode`      |
| `Ctrl+Shift+K`        | Clear buffer                            | Unbind `Terminal.ClearBuffer`         |
| `Ctrl+Shift+A`        | Select all                              | Unbind `Terminal.SelectAll`           |
| `Ctrl+Shift+C/V`      | Copy / paste                            | Unbind copy/paste actions             |
| `Ctrl+Shift+1..9`     | Open profile N                          | Unbind `Terminal.OpenNewTabProfileN`  |
| `Ctrl+Shift+Tab`      | Previous tab                            | Unbind `Terminal.PrevTab`             |
| `Ctrl+Tab`            | Next tab                                | Unbind `Terminal.NextTab`             |
| `Ctrl+Alt+1..9`       | Switch to tab N                         | Unbind `Terminal.SwitchToTabN`        |
| `Ctrl+,`              | Open settings UI                        | Unbind `Terminal.OpenSettingsUI`      |
| `Ctrl++` / `Ctrl+-`   | Increase / decrease font                | Unbind font-size actions              |
| `Ctrl+0`              | Reset font size                         | Unbind `Terminal.ResetFontSize`       |
| `Alt+F4`              | Close window                            | OS-level too — usually leave alone    |
| `Alt+Enter` / `F11`   | Toggle full-screen                      | Unbind `Terminal.ToggleFullscreen`    |
| `Alt+Space`           | Open system menu                        | OS-level — usually leave alone        |

> **Subtle case — `Ctrl+Shift+←/→`.** Windows Terminal's [Selection docs](https://learn.microsoft.com/en-us/windows/terminal/selection)
> bind these to *"expand existing selection by word"*. The combos are only
> consumed when a selection exists; otherwise they fall through to the app.
> This is why a Hex1b binding on `Ctrl+Shift+←` *appears* to work — it does,
> until the user happens to have a terminal selection active.

### Mouse

Windows Terminal's [Selection docs](https://learn.microsoft.com/en-us/windows/terminal/selection)
explain the mouse interceptions:

| Combo              | Windows Terminal action                            | Workaround                       |
| ------------------ | -------------------------------------------------- | -------------------------------- |
| `Shift+Click`      | Extend selection to the click point                | Hold `Shift` is "magic" — no app-side override |
| `Ctrl+Shift+Click` | Multi-select extend                                | Same as above                    |
| `Alt+Click`        | Block (column) selection                           | Same as above                    |
| `Right Click`      | Copy if selection exists, else paste               | Disable in Settings → Interaction|
| `Ctrl+Click`       | Open URL **only when** the cell has a detected URL | Otherwise forwarded to the app   |

The selection-related Shift/Ctrl+Shift mouse behaviour is *non-configurable*
per the official docs — those combos cannot be reclaimed. Plan your bindings
around them.

## ConPTY / `conhost.exe` (legacy Windows console)

The "old" Windows console (the black box that hosts `cmd.exe` when Windows
Terminal isn't available) is much more permissive — most arrow/function-key
combos pass through cleanly. Known caveats:

| Combo               | Behaviour                                          |
| ------------------- | -------------------------------------------------- |
| `Alt+Space`         | Opens the system menu (OS, can't override)         |
| `Alt+F4`            | Closes the window (OS, can't override)             |
| `Alt+Enter`         | Toggle full-screen on older Windows                |
| `Ctrl+Shift+letter` | Falls into the universal ANSI ambiguity (see top)  |

When Hex1b runs through `Hex1b.WindowsConsoleDriver` (the direct driver), it
reads from the Win32 console API and sees explicit modifier bits, so it
synthesises CSI sequences that include the modifiers. Bindings like
`Ctrl+Shift+↑` *do* work end-to-end on legacy `conhost`.

## macOS Terminal.app (Apple's built-in)

The shipped-with-macOS terminal handles most modifier combos cleanly, but
because **macOS itself never forwards `Cmd` to terminal apps**, every
`Cmd+<key>` is consumed by Terminal.app or the OS:

| Combo                          | Terminal.app action                                |
| ------------------------------ | -------------------------------------------------- |
| `Cmd+N`                        | New window                                         |
| `Cmd+T`                        | New tab                                            |
| `Cmd+W`                        | Close tab                                          |
| `Cmd+Shift+W`                  | Close window                                       |
| `Cmd+Shift+]` / `Ctrl+Tab`     | Next tab                                           |
| `Cmd+Shift+[` / `Ctrl+Shift+Tab` | Previous tab                                     |
| `Cmd+1..9`                     | Switch to tab N                                    |
| `Cmd+D`                        | Split window vertically                            |
| `Cmd+Shift+D`                  | Split window horizontally                          |
| `Cmd+K`                        | Clear screen (keeps scrollback)                    |
| `Cmd+F`                        | Find                                               |
| `Cmd+C` / `Cmd+V` / `Cmd+A`    | Copy / paste / select-all                          |
| `Cmd+,`                        | Open Preferences                                   |
| `Cmd++` / `Cmd+-` / `Cmd+0`    | Font size (in/out/reset)                           |
| `Cmd+↑/↓` / `Cmd+PgUp/PgDn`    | Scroll one line / page                             |
| `Cmd+Q`                        | Quit Terminal.app                                  |

Plus OS-level interceptions that affect every terminal on macOS:

| Combo                          | OS action                                          |
| ------------------------------ | -------------------------------------------------- |
| `Cmd+Tab` / `Cmd+~`            | App / window switcher                              |
| `Cmd+Space`                    | Spotlight (configurable)                           |
| `Ctrl+Cmd+Q`                   | Lock screen                                        |
| `Ctrl+↑/↓`                     | Mission Control / show desktop (if enabled)        |
| `Ctrl+←/→`                     | Move between Spaces (if enabled)                   |

Notable Terminal.app quirk: `Option` is **not** sent as a modifier by
default — instead `Option+<key>` produces a Mac-specific glyph (e.g.
`Option+E` types `é`'s combining accent). To get xterm-style `Alt+<key>`
delivery, enable **Settings → Profiles → Keyboard → Use Option as Meta key**.
Without that toggle, none of Hex1b's `Alt+<arrow>` / `Alt+<letter>` bindings
will fire on Terminal.app.

## Ghostty (cross-platform)

[Ghostty](https://ghostty.org/) ships with a fairly large default keybinding
set, **and the macOS bindings differ from the Linux/Windows bindings** because
macOS Ghostty uses `Cmd+<key>` while Linux/Windows Ghostty uses
`Ctrl+Shift+<key>`. Both sets are intercepted before they reach the running
app. The complete list lives in `ghostty +list-keybinds --default`; the most
common offenders:

| Action                  | Linux / Windows         | macOS                |
| ----------------------- | ----------------------- | -------------------- |
| New window              | `Ctrl+Shift+N`          | `Cmd+N`              |
| New tab                 | `Ctrl+Shift+T`          | `Cmd+T`              |
| Close tab / surface     | `Ctrl+Shift+W`          | `Cmd+W`              |
| Previous / next tab     | `Ctrl+Shift+Tab` / `Ctrl+Tab` | `Cmd+Shift+[` / `Cmd+Shift+]` |
| Tab 1..9                | `Alt+1..9`              | `Cmd+1..9`           |
| New split (right/down)  | `Ctrl+Shift+O` / `Ctrl+Shift+E` | `Cmd+D` / `Cmd+Shift+D` |
| Focus split             | `Ctrl+Alt+arrows`       | `Cmd+Option+arrows`  |
| Resize split            | `Ctrl+Super+Shift+arrows` | `Cmd+Ctrl+arrows`  |
| Toggle fullscreen       | `Ctrl+Enter` / `F11`    | `Cmd+Enter`          |
| Copy / paste            | `Ctrl+Shift+C/V`        | `Cmd+C/V`            |
| Find                    | `Ctrl+Shift+F`          | `Cmd+F`              |
| Open / reload config    | `Ctrl+,` / `Ctrl+Shift+,` | `Cmd+,` / `Cmd+Shift+,` |
| Inspector               | `Ctrl+Shift+I`          | `Cmd+Option+I`       |
| Quit                    | `Ctrl+Shift+Q`          | `Cmd+Q`              |
| Scroll to top / bottom  | `Shift+Home` / `Shift+End` | `Cmd+Home` / `Cmd+End` |

Note that Ghostty's Linux defaults overlap heavily with GNOME Terminal /
Ptyxis (`Ctrl+Shift+T/N/W/F/C/V`) — so an app that targets "Linux terminals"
should treat that whole row as off-limits regardless of which terminal the
user runs.

Ghostty supports the **kitty keyboard protocol** (opt-in via
`keybind = ...:disambiguate-escape-codes` and friends), which makes the
cross-cutting ANSI ambiguities below resolvable on this terminal — but
Hex1b doesn't yet negotiate it.

## kitty (cross-platform)

[kitty](https://sw.kovidgoyal.net/kitty/) gates almost every binding behind
a single configurable modifier called `kitty_mod`. The default value is
`Ctrl+Shift`, which means **the entire `Ctrl+Shift+<letter/key>` keyspace is
owned by kitty itself** out of the box. Users who want apps to receive these
combos need to either change `kitty_mod` (commonly to `Super` or `Cmd`) or
unbind the specific combos they want.

Default `kitty_mod` (= `Ctrl+Shift`) bindings:

| Combo                   | kitty action                              |
| ----------------------- | ----------------------------------------- |
| `Ctrl+Shift+C/V`        | Copy / paste                              |
| `Ctrl+Shift+Enter`      | New OS window                             |
| `Ctrl+Shift+T`          | New tab                                   |
| `Ctrl+Shift+W`          | Close window                              |
| `Ctrl+Shift+]` / `[`    | Next / previous tab                       |
| `Ctrl+Shift+1..9`       | Go to tab N                               |
| `Ctrl+Shift+L`          | Next layout                               |
| `Ctrl+Shift+F`          | Toggle fullscreen                         |
| `Ctrl+Shift+,` / `.`    | Move tab left / right                     |
| `Ctrl+Shift+arrows`     | Move window focus between splits          |
| `Ctrl+Shift+S`          | Selection mode                            |
| `Ctrl+Shift+Q`          | Show all key bindings                     |
| `Ctrl+Shift+Backspace`  | Clear scrollback                          |
| `Ctrl+Shift+R`          | Interactive Unicode input                 |
| `Ctrl+Shift+E`          | Edit current command line in `$EDITOR`    |
| `Ctrl+Shift++` / `-`    | Font bigger / smaller                     |
| `Ctrl+Shift+/`          | Search                                    |

kitty's saving grace: it implements (and pioneered) the
[kitty keyboard protocol](https://sw.kovidgoyal.net/kitty/keyboard-protocol/),
which sends explicit modifier bitmasks for *every* key. When an app
negotiates the protocol via `CSI > 1 u`, every cross-cutting limit at the
bottom of this page goes away — `Ctrl+Backspace`, `Ctrl+Shift+<letter>`,
`Ctrl+I` vs `Tab`, etc. all become unambiguous. Hex1b doesn't negotiate the
protocol today, so kitty currently behaves like any other terminal as far as
those limits go.

## iTerm2 (macOS)

| Combo                          | iTerm2 action                                      | Workaround                |
| ------------------------------ | -------------------------------------------------- | ------------------------- |
| `Cmd+<anything>`               | iTerm2 / macOS handles it; never reaches the app   | macOS doesn't forward `Cmd` to terminal apps. Use a different modifier. |
| `Cmd+T`/`Cmd+W`/`Cmd+N`        | New tab / close / new window                       | n/a                       |
| `Cmd+C`/`Cmd+V`                | Copy / paste                                       | n/a                       |
| `Option+<arrow>`               | Word jump (sent to app as `Esc+<arrow>`)           | Works as `Alt+<arrow>` in Hex1b |
| `Ctrl+Tab`                     | Cycle tabs (Profiles → Keys → Show/Hide tabs)      | Remap in iTerm2 prefs     |

iTerm2 also supports `modifyOtherKeys=2` and the kitty keyboard protocol —
when those are enabled, `Ctrl+Shift+<letter>` and friends *do* deliver
unambiguously. Hex1b doesn't yet enable either, so today the ANSI caveats
apply.

## GNOME Terminal

| Combo                | GNOME Terminal action                            | Workaround                 |
| -------------------- | ------------------------------------------------ | -------------------------- |
| `Ctrl+Shift+C/V`     | Copy / paste                                     | Preferences → Shortcuts → unset |
| `Ctrl+Shift+T`       | New tab                                          | Same                       |
| `Ctrl+Shift+N`       | New window                                       | Same                       |
| `Ctrl+Shift+W/Q`     | Close tab / window                               | Same                       |
| `Ctrl+Shift+F`       | Find                                             | Same                       |
| `Ctrl+PgUp`/`PgDn`   | Switch tabs                                      | Same                       |
| `Ctrl+Shift+letter`  | Various menu shortcuts                           | Same                       |

GNOME Terminal also overlaps with the GNOME shell:

| Combo                | Shell action                                     |
| -------------------- | ------------------------------------------------ |
| `Ctrl+Alt+<arrow>`   | Switch workspace (some distros)                  |
| `Ctrl+Alt+T`         | Open new terminal (some distros)                 |
| `Alt+F2`             | Run dialog                                       |

## Ptyxis (Ubuntu 25.10+ / 26.04 LTS default)

Starting with [Ubuntu 25.10](https://itsfoss.com/news/ubuntu-25-10-default-terminal-image-viewer/),
the default terminal is no longer GNOME Terminal — it's **[Ptyxis](https://gitlab.gnome.org/chergert/ptyxis)**
(a GTK4/Libadwaita terminal built around Podman/Toolbox containers, also the
default on Fedora). For binding-interception purposes Ptyxis behaves
near-identically to GNOME Terminal: the same `Ctrl+Shift+<letter>` family is
owned by the terminal UI.

| Combo                | Ptyxis action                                    |
| -------------------- | ------------------------------------------------ |
| `Ctrl+Shift+T`       | New tab                                          |
| `Ctrl+Shift+N`       | New window                                       |
| `Ctrl+Shift+W`       | Close tab                                        |
| `Ctrl+Shift+C` / `V` | Copy / paste                                     |
| `Ctrl+Shift+F`       | Find                                             |
| `Ctrl+Shift+,`       | Open Preferences (Settings)                      |
| `Ctrl+PgUp` / `PgDn` | Switch tabs                                      |
| `Ctrl+Tab`           | Next tab                                         |
| `F11`                | Toggle fullscreen                                |

GNOME shell-level interceptions (workspace switching, etc.) listed under
[GNOME Terminal](#gnome-terminal) apply equally to Ptyxis, since they're
owned by the desktop environment, not the terminal.

If your app targets Ubuntu and you previously planned around GNOME
Terminal's defaults, **the binding picture is essentially unchanged on
Ubuntu 26.04 LTS** — but the *application* the user runs is now Ptyxis by
default, so error messages and screenshots in your docs should reflect that.

## xterm

`xterm` is the closest you can get to a "pure" baseline. Almost every combo
passes through. The notable exceptions are the universal ANSI limits at the
top of this page, plus:

- `Ctrl+Click`, `Shift+Click`, `Right Click` open the xterm popup menus by
  default (configurable in `~/.Xresources`).
- The classic `xterm` defaults don't enable `modifyOtherKeys` — so
  `Ctrl+Shift+<letter>` collapses to `Ctrl+<letter>` unless you set
  `XTerm.VT100.modifyOtherKeys: 2` in your X resources.

## tmux

When running inside `tmux`, **everything** is filtered through tmux's own
binding system. The leader key (default `Ctrl+B`) sits in front of every key
and tmux owns its own list of bindings on top. Some implications:

- `Ctrl+B` itself never reaches the app.
- tmux can be configured to enable [extended-keys mode](https://github.com/tmux/tmux/wiki/Modifier-Keys)
  (`set -g extended-keys always` and `set -as terminal-features ',*:extkeys'`)
  which forwards xterm-style modifier sequences to the inner app — without
  that, modified-keys handling depends on tmux version.
- The mouse mode toggle (`set -g mouse on`) is required for any mouse events
  to reach Hex1b through tmux.

## ssh sessions (PuTTY, MobaXterm, etc.)

Over ssh, you're at the mercy of the *client-side* terminal emulator (PuTTY,
MobaXterm, the Windows Terminal hosting `ssh.exe`, the Mac Terminal hosting
`ssh`, etc.) — every interception the client does still happens before the
bytes hit the wire. Additional client-specific quirks:

- **PuTTY** rebinds `Ctrl+Tab`/`Ctrl+Shift+Tab` (window-switching), and its
  defaults for `Backspace` (DEL vs BS) are configurable per-session.
- **MobaXterm** intercepts a long list of combos for its own MobaXterm
  features (search, log, etc.).

## Cross-cutting limitations (every terminal)

These three are protocol-level — no terminal can deliver them through the
standard ANSI/xterm encoding regardless of settings:

| Combo                       | Why it fails                                 |
| --------------------------- | -------------------------------------------- |
| `Ctrl+Backspace`            | Encoded as `0x08`, identical to `Ctrl+H`     |
| `Ctrl+H` vs `Backspace`     | `0x08` vs `0x7F` — tagged as Backspace either way in Hex1b's tokenizer |
| `Ctrl+I` vs `Tab`           | Both `0x09`                                  |
| `Ctrl+M` vs `Enter`         | Both `0x0D`                                  |
| `Ctrl+[` vs `Esc`           | Both `0x1B`                                  |
| `Ctrl+Shift+<letter>`       | `Ctrl` strips ASCII bit 6; `Shift` has no remaining effect (unless the terminal speaks `modifyOtherKeys=2` or kitty keyboard protocol) |

Special keys (arrows, function keys, `Home`/`End`/`PgUp`/`PgDn`, `Insert`,
`Delete`) carry their modifier as an explicit CSI parameter, so
`Ctrl+Shift+<arrow>` etc. *are* unambiguous and reliable when the terminal
forwards them.

## Recommendations for Hex1b authors

1. **Default to special keys + modifiers.** `Ctrl+Shift+<arrow>`,
   `Alt+<arrow>`, `Ctrl+Home/End`, `F<n>+<modifier>` are the most portable
   shortcuts.
2. **Avoid `Ctrl+Shift+<letter>` for primary bindings** — pair it with a
   fallback (e.g. also bind a function key or arrow combo) for the same
   action.
3. **Avoid Windows Terminal's table above** if your app primarily targets
   Windows Terminal users — or document that they need to unbind these in
   their `settings.json`.
4. **Treat `Ctrl+Backspace` as unsupported on standard ANSI terminals** until
   Hex1b adds the kitty keyboard protocol.
5. **Run [`KeyBindingTester`](https://github.com/mitchdenny/hex1b/tree/main/samples/KeyBindingTester) on every
   terminal you target** and ship the resulting markdown report alongside your
   app's release notes.

## See also

- [`samples/KeyBindingTester/README.md`](https://github.com/mitchdenny/hex1b/tree/main/samples/KeyBindingTester)
  — the manual portability harness that produced the data behind this page.
- microsoft/terminal `defaults.json` — authoritative source for the Windows
  Terminal column above.
- [Windows Terminal Selection docs](https://learn.microsoft.com/en-us/windows/terminal/selection)
  — authoritative source for the Windows Terminal mouse table.
- [kitty keyboard protocol](https://sw.kovidgoyal.net/kitty/keyboard-protocol/)
  — the standardised escape extension that disambiguates the protocol limits
  listed in the cross-cutting table.
