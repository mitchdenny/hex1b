# KeyBindingTester

A manual portability harness: a checklist of expected key/mouse combinations
that ticks off as the user presses each one.

## Why

Hex1b's unit tests cover the *internal* dispatch path with synthetic
`Hex1bKeyEvent`s — they prove the wiring is correct, but they can't prove that
any *given terminal emulator* actually delivers the bytes the wiring expects.
Different terminals make different choices about what they emit for
`Ctrl+Shift+A`, `Alt+Ctrl+F5`, `Shift+Click`, and so on.

Run this sample on every terminal you target — Windows Terminal, ConPTY,
iTerm2, GNOME Terminal, xterm, ssh sessions from PuTTY/MobaXterm, etc. — to
see which combos are actually deliverable on each platform. Useful for
documenting platform support and for choosing default bindings that will work
everywhere.

## How

```bash
dotnet run --project samples/KeyBindingTester
```

Press each combo on the checklist. Entries tick off as they fire. Press `R` to
reset the checklist (handy when re-running on a different terminal in the
same session). Press `Esc` or `Ctrl+C` to exit.

The bottom shows the label of the last binding that fired — useful for
spotting cases where a key combo arrived with unexpected modifiers.

## Letter-key caveat (the † rows)

Most terminals cannot distinguish `Ctrl+Shift+letter` from `Ctrl+letter`:
applying `Ctrl` strips ASCII bit 6 of the letter, and `Shift` gets dropped
entirely. So a binding like `Ctrl+Shift+A` may never fire on standard
xterm/VT-mode terminals — that's a terminal limitation, not a Hex1b bug.

Special keys (arrows, function keys, Home/End/PgUp/PgDn) deliver `Ctrl+Shift`
reliably because their CSI sequences carry an explicit modifier code. Mouse
buttons in SGR mode also include explicit modifier bytes. The letter rows
exist to make this caveat visible — they're flagged with `†` and are
expected to be unreliable.

For terminals that support `xterm`'s `modifyOtherKeys=2` mode (most modern
ones), `Ctrl+Shift+letter` does deliver explicitly — so a missing tick on
those rows tells you whether `modifyOtherKeys` is engaged on your terminal.
