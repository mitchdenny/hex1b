# Sixel Terminal Behavior Contract

> **Status**: Evolving contract for [#445](https://github.com/mitchdenny/hex1b/issues/445)
> **First executable stage**: [#448](https://github.com/mitchdenny/hex1b/issues/448)
> **Baseline**: DEC VT340

## Purpose

This document defines the terminal-side Sixel behavior that Hex1b tests and
implements. It is a contract for `Hex1bTerminal`, not for `SixelWidget` or the
Sixel encoder.

The DEC VT340 is the baseline. Modern-terminal differences are recorded here
and must be represented by explicit compatibility policy when unavoidable.
Hex1b must not branch on terminal names.

The tests under `tests/Hex1b.Tests/Sixel/` establish the executable form of this
contract before the parser, graphics state, or cell tracking is replaced.
Ignored tests name the later issue that owns the missing behavior.

## Governing decisions

| Area | Selected Hex1b contract | Rationale |
|---|---|---|
| Baseline | DEC VT340 semantics | Stable public baseline from #445 |
| Output framing | Always `ESC P ... ESC \` | Portable 7-bit representation |
| Input framing | Accept 7-bit framing and C1 `DCS`/`ST` bytes | DEC compatibility; C1 support is input-only |
| Native presentation | Forward bytes immediately and exactly | A capable terminal must receive the application's original sequence |
| Parsing | Recognize Sixel only when the DCS final byte is `q` | Other DCS protocols must remain independently dispatchable |
| Palette | Shared registers persist between images | Matches DEC and the prevailing modern default |
| Graphics state | Own placements independently of text cells | Required for overlap, screen ownership, scrolling, resize, and reflow |
| Compatibility | Central policy/profile, never terminal-name checks | Keeps deviations reviewable and testable |

## Framing and dispatch

| Behavior | DEC and reference terminals | Hex1b default | Status or unresolved work |
|---|---|---|---|
| 7-bit DCS/ST | DEC defines `ESC P` and `ESC \`; all reviewed terminals accept them | Required input and output form | Active split-boundary and exact-byte tests |
| 8-bit C1 DCS/ST | DEC permits `0x90` and `0x9c`; modern parsers generally accept them | Accepted input; normalized only in parsed state, never in native passthrough | [#446](https://github.com/mitchdenny/hex1b/issues/446) |
| Arbitrary chunks | Framing has no transport-boundary semantics | Every byte boundary produces the same model and presentation bytes | Active exhaustive single-split tests |
| Sixel dispatch | The DCS final byte `q` selects Sixel | Other complete DCS sequences are not graphics | [#446](https://github.com/mitchdenny/hex1b/issues/446) |
| Cancellation | `CAN` or `SUB` aborts the current control string | Cancel without producing a placement; parsing resumes after the cancel byte | [#446](https://github.com/mitchdenny/hex1b/issues/446) |
| Incomplete DCS | No placement exists before termination | Buffer parser state while still forwarding raw bytes to native presentation | [#446](https://github.com/mitchdenny/hex1b/issues/446) |

Native passthrough is deliberately independent of parser success. When the
presentation adapter can consume Sixel directly, Hex1b forwards the original
bytes without waiting for a complete frame, decoding UTF-8, or reconstructing
the sequence.

### Output ownership matrix

Raw workload output has exactly one DCS framing owner. Presentation delivery is
selected independently so observing a control string never adds latency to a
native terminal.

| Path | Byte owner | Framing owner | Presentation behavior |
|---|---|---|---|
| Raw workload, raw presentation | Workload read | Incremental byte framer | Each original read is forwarded before framing or UTF-8 decoding |
| Raw workload with workload filters | Workload read | Incremental byte framer | Raw presentation still receives each read first; observers receive the resulting token stream |
| Presentation filters | Filtered token stream | Incremental byte framer | Filters own serialization, so C1 input may be normalized to standard `ESC` framing |
| Impact-aware presentation | Applied token stream | Incremental byte framer | Receives one structured DCS token and its terminal impacts |
| Pre-tokenized `Hex1bApp` output | App-provided tokens and matching bytes | App token stream at a framing boundary; an existing raw frame retains ownership across item boundaries | Raw presentation receives the supplied bytes; structured dispatch validates each DCS once |
| Headless | Internal terminal model | Incremental byte framer | No native display dependency; framing and dispatch still occur before text decoding |

The framer bounds retained DCS content to 1 MiB by default. It continues
counting and scanning for cancellation or ST after that limit. Its incremental
Sixel observer also continues bounded grammar and geometry parsing, but reports
a limit-downgraded outcome and stops retaining raster command events. The
original bytes still flow to native presentations before either parser runs.

## Grammar and raster model

### DCS parameters

The introducer is `DCS P1 ; P2 ; P3 q`.

| Parameter | DEC VT340 | Selected Hex1b behavior | Modern differences |
|---|---|---|---|
| `P1` aspect macro | Omitted, 0, 1, 5, or 6 select 2:1; 2 selects 5:1; 3 or 4 select 3:1; 7, 8, or 9 select 1:1 | Use the DEC table; unsupported values use the 2:1 default | Windows Terminal uses 1:1 for unsupported values; xterm, WezTerm, and foot use 2:1 |
| `P2` background | 0 or 2 is opaque; 1 leaves unpainted pixels unchanged | Only 1 is transparent; all other values are opaque | The source of the opaque color differs; see Background |
| `P3` horizontal grid size | Ignored by VT300 | Parse for syntax and diagnostics, but do not change geometry | xterm, WezTerm, and foot also ignore it for rendering |

### Data and commands

| Command | Contract |
|---|---|
| `?` through `~` | Subtract `0x3f`; bit 0 is the top pixel and bit 5 is the bottom pixel |
| `! Pn sixel` (DECGRI) | Repeat the following sixel column `Pn` times; omitted or zero means one |
| `$` (DECGCR) | Return to the left edge of the current sixel band without changing its vertical position |
| `-` (DECGNL) | Return left and advance by one six-row band after aspect scaling |
| `" Pan ; Pad ; Ph ; Pv` (DECGRA) | Set pixel aspect and declare horizontal (`Ph`) and vertical (`Pv`) extents |
| `# Pc` (DECGCI) | Select color register `Pc` |
| `# Pc ; 1 ; H ; L ; S` | Define and select `Pc` using DEC HLS coordinates |
| `# Pc ; 2 ; R ; G ; B` | Define and select `Pc` using 0-100% RGB coordinates |

DEC DECGCI definition also selects the register. WezTerm currently records a
definition without selecting it and continues with an initial green paint
color. The native demo follows each definition with an explicit `# Pc` so its
visual result remains portable; the Hex1b model contract retains DEC behavior.

Malformed commands must produce an explicit parser outcome. Resource rejection
must stop raster allocation but continue enough metadata parsing to find the
terminator and report final geometry. Silent exception swallowing is not part
of the terminal contract.

## Aspect and extents

| Behavior | DEC and reference terminals | Hex1b default | Status or unresolved work |
|---|---|---|---|
| `Pan`/`Pad` override | DECGRA aspect overrides the `P1` macro | Last valid DECGRA aspect controls the sequence | Active incremental parser tests |
| `Ph`/`Pv` orientation | `Ph` is width and `Pv` is height | Horizontal then vertical, without transposition | Active incremental parser and terminal tests |
| Declared extent | Allocation hint, not a clipping rectangle | Final extent is the maximum of declared and data/painted extents | Active incremental parser tests |
| Partial final band | Raster height may end within a six-row band | Preserve the declared pixel extent separately from band-rounded data geometry | Parser model active; final raster semantics remain in [#449](https://github.com/mitchdenny/hex1b/issues/449) |
| Pathological ratios/extents | Modern terminals impose implementation bounds | Enforce centralized limits before allocation and report resource rejection | Exact limits remain an implementation decision |
| Fractional cell metrics | Modern terminals can report non-integral pixel metrics | Retain the best available metric and apply deterministic outward rounding for occupied cells | Harness records fractional width now |

Windows Terminal reports an undocumented VT330 behavior in which DECGRA also
performs a graphics carriage return. DEC does not document this. Differential
testing in [#457](https://github.com/mitchdenny/hex1b/issues/457) must decide
whether it belongs in an optional profile.

## Color and palette

| Behavior | DEC and reference terminals | Hex1b default | Status or unresolved work |
|---|---|---|---|
| RGB | Three 0-100% components | Clamp valid components to the DEC domain and convert deterministically to 8-bit RGB | Basic decoding active; parser work in #449 |
| HLS | Hue 0 is blue, 120 is red, and 240 is green; lightness and saturation are percentages | Use the DEC hue wheel, not the CSS hue wheel | Current automation defect captured for #449 |
| Register persistence | DEC has a shared palette; xterm, WezTerm, foot, Windows Terminal, and xterm.js persist by default | Share palette state between sequences on the same screen session | [#449](https://github.com/mitchdenny/hex1b/issues/449) |
| Private registers | xterm mode 1070 and some terminal options provide per-image palettes | Shared by default; any private mode must be an explicit compatibility option | Support and reset details unresolved |
| RIS | WezTerm resets its shared color map; other reviewed behavior is incomplete | Reset Sixel palette and placements to terminal defaults | [#453](https://github.com/mitchdenny/hex1b/issues/453) |
| DECSTR | Reference behavior is not sufficiently established | Preserve palette unless differential testing demonstrates a stable DEC-compatible reset rule | Explicitly unresolved for #457 |

### Background

For `P2=1`, unpainted pixels preserve the underlying graphics or text result.
For other values, Hex1b fills unpainted pixels with Sixel palette register 0.
This matches xterm and WezTerm.

Foot and xterm.js instead use the live terminal background. DEC describes the
"current background color" without resolving this distinction. The selected
register-0 behavior remains an explicit
[#457](https://github.com/mitchdenny/hex1b/issues/457) differential-testing
target.

## Placement, cursor, and modes

### DECSDM and mode 8452

| Mode | Selected Hex1b behavior | Evidence and divergence |
|---|---|---|
| Default | Sixel scrolling enabled | DEC VT340 hardware reports and the manual identify scrolling as the normal behavior |
| `CSI ? 80 h` | Enable Sixel scrolling: start at the active text position, scroll when needed, and update the cursor | DEC manual and hardware-tested foot behavior |
| `CSI ? 80 l` | Disable Sixel scrolling: use graphics-page origin and leave the text cursor unchanged | DEC manual and WezTerm's non-scrolling placement behavior |
| `CSI ? 8452 l` | In scrolling mode, leave the cursor at its original column below the graphic | xterm extension reset/default behavior |
| `CSI ? 8452 h` | Compatibility option to leave the cursor to the right | Confirmed only in xterm/RLogin; do not enable by default |

DECSDM polarity is the most significant unresolved compatibility issue. Current
xterm documentation and implementation interpret set/reset in the opposite
direction from the VT340 manual and hardware tests. Foot changed its polarity
after testing real VT340 hardware. Hex1b selects the DEC interpretation and
must keep an xterm-compatible inversion, if later required, in centralized
policy rather than terminal detection.

### Cursor, margins, and origin

In scrolling mode, placement starts at the active text position. Origin mode
and active margins determine that position and the scrolling region. Placement
may be clipped by a margin or viewport without changing its source raster.
After the sequence, the default cursor is at its original column below the
occupied cell rows; mode 8452 may select the right-side outcome.

In non-scrolling mode, placement starts at the graphics-page origin and restores
the text cursor exactly. Windows Terminal explicitly uses the full page instead
of text margins in this mode. Exact margin clipping across references remains a
[#457](https://github.com/mitchdenny/hex1b/issues/457) test target.

Implementation belongs to [#450](https://github.com/mitchdenny/hex1b/issues/450).

## Ownership, overlap, and erasure

| Operation | Selected Hex1b behavior | Unresolved details |
|---|---|---|
| Sixel over Sixel | Composite painted pixels over existing graphics; transparent holes preserve prior content | Alpha/compositing details across xterm, Windows Terminal, WezTerm, mintty, and xterm.js need #457 testing |
| Text over Sixel | Damage only the pixels covered by newly written text cells | Exact behavior for wide/combining cells needs implementation tests |
| ED/EL | Erase graphics in the affected cell/pixel region along with text | Boundary behavior at partially covered cells needs #457 testing |
| Scroll-region operations | Move, clip, or erase placements using the same region semantics as text rows | Owned by #452/#453 |
| RIS | Clear all placements and reset Sixel modes and palette | Owned by #453 |
| DECSTR | Reset modes; preserve palette and placements provisionally | Palette and placement behavior remains unresolved |

Foot has the clearest reviewed prior art for compositing independent placements.
Hex1b's existing KGP graphics state provides the closest internal model. Sixel
must not remain represented only by references attached to text cells.

## Screens, scrollback, resize, and reflow

| Area | Selected Hex1b contract | Status or unresolved work |
|---|---|---|
| Main/alternate screen | Each screen owns independent placements; leaving the alternate screen restores the unchanged main-screen graphics | [#453](https://github.com/mitchdenny/hex1b/issues/453) |
| Scrollback | Scrolling placements remain anchored to logical row lineage and can span visible and history rows | [#452](https://github.com/mitchdenny/hex1b/issues/452); foot provides verified prior art |
| History eviction | Remove only the placement portions no longer owned by retained row lineage | Exact partial-eviction policy needs #457 testing |
| Resize | Clip to the viewport without destroying source pixels; reveal them again when space returns | [#452](https://github.com/mitchdenny/hex1b/issues/452) |
| Reflow | Re-anchor through the same row-lineage plan as text and KGP placements | [#452](https://github.com/mitchdenny/hex1b/issues/452) |
| Cell-metric change | Recompute occupied cells from stable pixel geometry using deterministic outward rounding | Reference-terminal behavior needs #457 testing |

No reviewed reference provided a complete answer for resize/reflow or
main/alternate-screen ownership. These decisions intentionally align Sixel with
Hex1b's protocol-neutral terminal model and existing KGP reflow machinery while
remaining explicit differential-testing targets.

## Explicitly unresolved decisions

The following decisions must remain visible until
[#457](https://github.com/mitchdenny/hex1b/issues/457) provides executable
reference-terminal evidence:

1. DECSDM compatibility polarity for an optional xterm profile.
2. Whether mode 8452 should be implemented beyond its default reset behavior.
3. Palette register 0 versus live terminal background for opaque `P2`.
4. DECGRA's undocumented carriage-return behavior and aspect-scaled DECGNL.
5. Exact Sixel-over-Sixel compositing and partial-cell text/erase damage.
6. DECSTR effects on palettes and placements.
7. Main/alternate-screen, history eviction, resize, and reflow edge behavior.
8. Default palette values and private-register behavior across modern terminals.

## Evidence and running the contract

The test fixtures are small ASCII payloads embedded from
`tests/Hex1b.Tests/TestData/Sixel/`. Expected data is independently authored and
does not use `SixelEncoder`.

```bash
dotnet test tests/Hex1b.Tests/Hex1b.Tests.csproj \
  --filter "FullyQualifiedName~Hex1b.Tests.Sixel."
```

The terminal-first demo sends independently authored raw Sixel bytes through
`Hex1bTerminal`. It does not use `SixelWidget` or `SixelEncoder`.

```bash
dotnet run --project samples/SixelTerminalDemo
dotnet run --project samples/SixelTerminalDemo -- --headless
```

## Primary references

- [DEC VT3xx Graphics Programming, Chapter 14](https://vt100.net/docs/vt3xx-gp/chapter14.html)
- [xterm Control Sequences: Sixel Graphics](https://invisible-island.net/xterm/ctlseqs/ctlseqs.html#h3-Sixel-Graphics)
- [xterm `graphics_sixel.c`](https://github.com/ThomasDickey/xterm-snapshots/blob/master/graphics_sixel.c)
- [Windows Terminal `SixelParser`](https://github.com/microsoft/terminal/tree/main/src/terminal/adapter)
- [WezTerm Sixel parser](https://github.com/wezterm/wezterm/blob/main/wezterm-escape-parser/src/parser/sixel.rs)
- [WezTerm Sixel terminal state](https://github.com/wezterm/wezterm/blob/main/term/src/terminalstate/sixel.rs)
- [foot `sixel.c`](https://codeberg.org/dnkl/foot/src/branch/master/sixel.c)
- [mintty `sixel.c`](https://github.com/mintty/mintty/blob/master/src/sixel.c)
- [xterm.js image add-on](https://github.com/xtermjs/xterm.js/tree/master/addons/addon-image/src)
