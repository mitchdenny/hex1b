# Sixel Terminal Behavior Contract

> **Status**: Evolving contract for [#445](https://github.com/mitchdenny/hex1b/issues/445)
> **First executable stage**: [#448](https://github.com/mitchdenny/hex1b/issues/448)
> **Independent graphics state**: [#451](https://github.com/mitchdenny/hex1b/issues/451)
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
| Graphics state | Own placements independently of text cells | Required for overlap, screen ownership, scrolling, resize, and reflow — implemented by [#451](https://github.com/mitchdenny/hex1b/issues/451) |
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
| Partial final band | Raster height may end within a six-row band | Painted bounds record the exact final row while the data extent stays band-rounded; the logical canvas is never resampled by the aspect ratio | Active rasterizer tests |
| Aspect application | `Pan`/`Pad` scale the displayed image, not its stored rows | Store six logical rows per band unscaled and expose the aspect-scaled result as a separate rendered extent; no eager resampling | Active rasterizer tests |
| Pathological ratios/extents | Modern terminals impose implementation bounds | Enforce centralized limits before allocation and report resource rejection as an explicit geometry-only result that preserves geometry and diagnostics | Limits centralized in `SixelCompatibilityPolicy`; exact values remain an implementation decision |
| Large declared canvases | Terminals must survive very large `Ph`/`Pv` hints | Store pixels in lazily allocated tiles so a large declared canvas allocates in proportion to painted area, not declared area; materialize densely only on consumer request and within policy | Active rasterizer tests |
| Fractional cell metrics | Modern terminals can report non-integral pixel metrics | Retain the best available metric and apply deterministic outward rounding for occupied cells | Harness records fractional width now |

Windows Terminal reports an undocumented VT330 behavior in which DECGRA also
performs a graphics carriage return. DEC does not document this. Differential
testing in [#457](https://github.com/mitchdenny/hex1b/issues/457) must decide
whether it belongs in an optional profile.

## Color and palette

| Behavior | DEC and reference terminals | Hex1b default | Status or unresolved work |
|---|---|---|---|
| RGB | Three 0-100% components | Clamp valid components to the DEC domain and convert deterministically to 8-bit RGB with nearest rounding (`(percent * 255 + 50) / 100`) | Active rasterizer tests |
| HLS | Hue 0 is blue, 120 is red, and 240 is green; lightness and saturation are percentages | Use the DEC hue wheel, not the CSS hue wheel; hue wraps modulo 360 and lightness/saturation clamp to 0-100 | Active rasterizer tests |
| Default palette | DEC VT340 ships 16 hardware colors; modern terminals extend selection beyond them | Registers 0-15 are the VT340 defaults expressed in the DEC 0-100 domain; registers 16-255 extend them with the conventional 6x6x6 cube and grayscale ramp so selection without definition is defined for every register inside policy | Centralized in `SixelDefaultPalette`; exact values remain a [#457](https://github.com/mitchdenny/hex1b/issues/457) target |
| Register count | DEC VT340 exposes 16; modern terminals commonly expose 256 | 256 terminal-scoped registers; selection or definition outside the policy is rejected explicitly with a diagnostic and never silently wrapped | Centralized in `SixelCompatibilityPolicy` |
| Register persistence | DEC has a shared palette; xterm, WezTerm, foot, Windows Terminal, and xterm.js persist by default | Share palette state between sequences on the same terminal; definitions apply in command order even when rasterization degrades to geometry only | Active rasterizer and terminal tests |
| Private registers | xterm mode 1070 and some terminal options provide per-image palettes | Shared by default; any private mode must be an explicit compatibility option expressed through `SixelCompatibilityPolicy.PaletteScope` | Support and reset details unresolved |
| RIS | WezTerm resets its shared color map; other reviewed behavior is incomplete | Reset the Sixel palette to terminal defaults; placement reset remains later-stage work | Palette reset active; placements in [#453](https://github.com/mitchdenny/hex1b/issues/453) |
| Alternate screen | Reviewed terminals keep one shared color map across screen buffers | Preserve palette registers across alternate-screen transitions | Active terminal tests |
| DECSTR | Reference behavior is not sufficiently established | Preserve palette unless differential testing demonstrates a stable DEC-compatible reset rule | Explicitly unresolved for #457 |

### Background

For `P2=1`, unpainted pixels preserve the underlying graphics or text result.

For `P2` 0 or 2, Hex1b fills unpainted pixels across the final logical extent
with **the terminal background color captured when the graphic was created**.
The captured value is fixed at creation time, so a later SGR background change
never retroactively repaints an existing graphic. When the terminal background
is unset, the fill is a deterministic black (`#000000FF`) so identical byte
streams always produce identical rasters.

This replaces the earlier provisional choice of Sixel palette register 0, which
matched xterm and WezTerm. The selected behavior instead matches foot and
xterm.js, and it keeps the opaque fill independent of a payload that redefines
register 0 for its own drawing. DEC describes the "current background color"
without resolving the distinction, so the alternative remains expressible
through `SixelCompatibilityPolicy.BackgroundSource` rather than as a hidden
branch, and stays a
[#457](https://github.com/mitchdenny/hex1b/issues/457) differential-testing
target.

Because the captured background and the persistent palette both change how an
identical payload rasterizes, tracked Sixel deduplication keys on the payload
*and* a raster-state identity rather than on payload content alone.

## Placement, cursor, and modes

### DECSDM and mode 8452

| Mode | Selected Hex1b behavior | Evidence and divergence |
|---|---|---|
| Default | Sixel scrolling enabled | DEC VT340 hardware reports and the manual identify scrolling as the normal behavior |
| `CSI ? 80 h` | Enable Sixel scrolling: start at the active text position, scroll when needed, and update the cursor | DEC manual and hardware-tested foot behavior |
| `CSI ? 80 l` | Disable Sixel scrolling: use graphics-page origin and leave the text cursor unchanged | DEC manual and WezTerm's non-scrolling placement behavior |
| `CSI ? 8452 l` | In scrolling mode, leave the cursor at its original column below the graphic | xterm extension reset/default behavior |
| `CSI ? 8452 h` | Compatibility option to leave the cursor to the right | Confirmed only in xterm/RLogin; do not enable by default |

DECSDM polarity is the most significant compatibility issue. Current xterm
documentation and implementation interpret set/reset in the opposite direction
from the VT340 manual and hardware tests. Foot changed its polarity after
testing real VT340 hardware. Hex1b selects the DEC interpretation, and the
xterm-compatible inversion lives in `SixelCompatibilityPolicy.DecsdmPolarity`
rather than in terminal detection. Which reference profile a terminal should
select stays a [#457](https://github.com/mitchdenny/hex1b/issues/457) target.

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

Three cursor concepts stay distinct. The *Sixel graphics cursor* lives inside the
raster and never escapes the parser. The *anchor* is the text cursor position the
placement is pinned to when the DCS sequence starts. The *final text cursor* is
where the terminal leaves the cursor once the sequence completes.

| Situation | Implemented behavior |
|---|---|
| Ordinary completion, scrolling mode | Anchor at the active text position; final cursor is one row below the occupied rows, at the anchor column |
| One-row image | Final cursor is on the row immediately below the anchor row |
| Multi-row image | Final cursor is `anchor row + occupied rows` |
| Partial final band | The partial band rounds up to a whole cell row before the cursor moves |
| Declared extent, no painted pixels | The declared extent still occupies cells; occupancy never collapses below one cell |
| Image exceeds the viewport | Occupancy keeps the full source geometry; only painted cells are clipped |
| Completion at or below the bottom margin | The region scrolls just enough to fit the image and the cursor row; a taller-than-region image keeps its bottom edge on the last region row |
| Followed by text, CR, LF, CUP, or another Sixel | Each applies from the final cursor, so a second Sixel stacks below the first |
| Non-scrolling mode | Anchor is the graphics-page origin, the full page is used instead of text margins, nothing scrolls, and the text cursor is unchanged |

A completed sequence also clears any deferred wrap, exactly like a line feed.
Occupancy is `ceil(renderedPixelExtent / cellMetric)` per axis, computed from
protocol cell metrics rather than from font metrics. Those metrics are captured
once, when the placement is created, and are recorded on the placement together
with their source and reliability, so a later metric change cannot retroactively
rewrite an existing placement. Discovering real metrics from an upstream
presentation is owned by
[#455](https://github.com/mitchdenny/hex1b/issues/455); until then metrics are
derived from terminal capabilities, reported as estimated, and injectable.

`CSI ? 80` and `CSI ? 8452` are reset to their defaults by both RIS and DECSTR.
Save/restore of these private modes (`CSI ? Pm s` and `CSI ? Pm r`) is not
implemented, because Hex1b has no private-mode save/restore machinery to extend.

This is the terminal-model direction of the data flow: how `Hex1bTerminal`
interprets an incoming Sixel sequence. In the opposite direction, when Hex1b
emits its own managed output, it never relies on where an upstream terminal
leaves the cursor after a Sixel image; it always repositions explicitly with
CUP before writing anything that follows.

## Ownership, overlap, and erasure

| Operation | Selected Hex1b behavior | Unresolved details |
|---|---|---|
| Sixel over Sixel | Both placements are retained independently; presentation composites in placement sequence order. Painted pixels from a later placement cover earlier pixels; unpainted/transparent pixels leave earlier placements visible. | Protocol translation into native downstream graphics remains #458; broader cross-terminal visual comparison remains #457. |
| Text over Sixel | Any text-cell write destructively damages the Sixel pixels projected into the overwritten cell. A space, styled background write, combining-cluster update, wide-character leading cell, or wide-character continuation cleanup is still a text write for graphics damage. Destroyed Sixel pixels do not reappear if the text is later erased. | Damage is modeled at bounded cell granularity rather than sub-cell glyph-shape granularity. |
| ED/EL/ECH/DECERA/DECSERA | Erase graphics in the same clipped cell region that text erasure affects. Selective erase preserves graphics only where the underlying terminal cell is protected. Full ED/RIS remove active placements; partial erases damage only intersecting placement cells. | Implemented; scrolling/reflow projection across the scrollback boundary is implemented by [#452](https://github.com/mitchdenny/hex1b/issues/452) (see below). |
| Insert/delete characters, columns, and lines | Character/column/line edits damage every overwritten destination or blank-fill cell in their clipped edit region, while Sixel placements themselves do not shift with ordinary text edits unless the existing scroll integration explicitly moves/drops them. | History/reflow projection is implemented by [#452](https://github.com/mitchdenny/hex1b/issues/452). |
| Scroll-region operations | Move, clip, split into history, or erase placements using the same region semantics as text rows, including partial vertical/horizontal margins under DECSTBM/DECLRMM | Full-fidelity scrolling/reflow projection across the scrollback boundary is implemented by [#452](https://github.com/mitchdenny/hex1b/issues/452); see "Independent Sixel scrolling, history, and reflow (#452)" below |
| RIS | Clear main and alternate placements, reset Sixel modes, reset the palette, clear saved screen state, and leave previously captured snapshots valid. | Implemented for lifecycle; native downstream redraw protocol remains #458. |
| DECSTR | Reset modes, including DECSDM and mode 8452; preserve palette, placements, cursor position, and snapshots. | Broader terminal comparison remains #457, but Hex1b's compatibility choice is centralized and deterministic. |

Foot has the clearest reviewed prior art for compositing independent placements.
Hex1b's existing KGP graphics state provides the closest internal model. Sixel
must not remain represented only by references attached to text cells.

## Independent graphics state (#451)

[#451](https://github.com/mitchdenny/hex1b/issues/451) replaces the earlier
per-cell ownership model — a `CellAttributes.Sixel` flag plus origin and
continuation cells that acted as reference-counted anchors for a
`TrackedObjectStore`-managed `SixelData` — with `SixelGraphicsState`, an
internal type family in `src/Hex1b/Sixel/` modeled after the mature
`KgpTerminalGraphicsState` (see `src/Hex1b/Kgp/`) but deliberately smaller and
protocol-neutral:

- `SixelGraphicsState` owns two independent `SixelScreenGraphicsState`
  instances (main and alternate). Re-entering the alternate screen while
  already active resets only the alternate instance; RIS is the only
  operation that clears both.
- Each `SixelScreenGraphicsState` owns a `SixelImageStore` (the raster
  resources, deduplicated by content hash — payload plus captured
  background/palette identity), a live `Placements` list, and — main screen
  only — a `HistoryPlacements` partition keyed by stable scrollback row
  identity.
- `SixelPlacement` anchors a `SixelData` image at a cell position and retains
  the anchor/occupied cell span, the painted-crop geometry (offset and count,
  relative to the anchor so scrolling can shift the anchor without recomputing
  the crop), the creation-time write sequence used to order overlapping
  placements, creation timestamp, and a bounded sparse set of destructively
  damaged cells. The damage set is capped by the placement's painted cell
  count, so repeated edits cannot fragment a placement without bound.
  `SixelData` itself (unchanged from
  earlier stages) carries the authoritative decoded raster or geometry-only
  outcome, logical/rendered/declared/painted extents, creation-time
  `SixelCellMetrics`, source and captured background, aspect state, a stable
  content hash for dedup, and parser diagnostics.
- **Lifetime is reachability-based, not manually reference-counted.** Mirroring
  `KgpImageStore`, every placement-removing mutation recomputes the set of
  content hashes reachable from `Placements ∪ HistoryPlacements` and sweeps any
  image no longer in that set. A `SixelPlacement`'s image is never released
  just because one text cell it covered was overwritten — only when no
  visible cell remains in any placement (live, historical, or held by an
  existing snapshot). Snapshots decouple entirely:
  `Hex1bTerminalSnapshot` copies its own `SixelPlacement`/`SixelData`
  references and damage state, kept alive by ordinary garbage collection
  independent of the live store.
- Geometry-only placements (the rasterizer refused pixel allocation) are
  always retained as placements — never silently dropped — so their occupied
  cell span and diagnostics remain inspectable.
- A finalized Sixel sequence creates one anonymous image plus one placement
  anchored at the position #450's cursor/metric logic already determines; this
  stage reuses `SixelParser`/`SixelRasterizer` and that cursor logic unchanged
  and does not duplicate any parsing, rasterization, or estimation logic.
- `TrackedObjectStore` no longer participates in Sixel image lifetime at all;
  it retains its unrelated duties (hyperlinks, and the Surface/widget path's
  own use of `GetOrCreateSixel`, which is untouched by this stage).
  Compatibility surfaces like `ContainsSixelData()`/`GetSixelDataAt()` are
  preserved, now backed by the placement/image model.

Managed presentation adapters receive Sixel placement and damage deltas through
`AppliedToken.GraphicsImpacts` independently of `AppliedToken.CellImpacts`.
Consumers that maintain their own raster cache should process those regions in
token order: add/replace on `SixelAdded`, and remove or requery covered fragments
on `SixelDamaged`. A graphics-only delta is still a render-invalidating change
even when no text cell value changed.

**Extracted from KGP as genuinely protocol-neutral primitives:** raster
content ownership by content hash, placement/source-crop geometry (anchor +
occupied span + painted crop), reachability-based lifetime accounting,
screen/history partitioning, and simple scroll/clip geometry helpers. **Kept
deliberately KGP-only, not extracted:** public image/placement IDs,
image-number addressing, explicit delete selectors, relative placement
graphs, Unicode placeholders, z-index, and chunked uploads — none of these
concepts exist in the Sixel protocol. The two graphics states are separate
types with no compile-time coupling; only the underlying *strategy* (mark and
sweep over a screen/history partition) is shared conceptually. All existing
KGP tests (state, deletion, scrolling/reflow, snapshot) continue to pass
unmodified.

**Explicitly deferred past this stage** (see the tables above and
`tests/Hex1b.Tests/Sixel/SixelScrollingTests.cs` for the still-`[Ignore]`d
placeholders that name them):

- Full scrolling/reflow integration — projecting a single placement across
  the visible/history boundary, and reflow-driven re-anchoring — is
  implemented by [#452](https://github.com/mitchdenny/hex1b/issues/452); see
  "Independent Sixel scrolling, history, and reflow (#452)" below.
- Native presentation protocol translation (removing/replacing damaged
  downstream rasters in terminals that are not using Hex1b's managed
  presentation model) — [#458](https://github.com/mitchdenny/hex1b/issues/458).
- `SixelWidget`/`Surface`-produced Sixel and widget sizing changes are
  untouched by this stage.

## Independent Sixel scrolling, history, and reflow (#452)

[#452](https://github.com/mitchdenny/hex1b/issues/452) layers scrolling,
main-screen scrollback history, viewport clipping/pruning, resize, and
anchor-based reflow onto #451's placement/image model, mirroring
`KgpTerminalGraphicsState`'s scrolling/history/reflow fidelity with the same
deliberate protocol-neutral simplifications — no public placement IDs, no
delete selectors, no z-index. KGP's own scroll/history/reflow code paths are
untouched; every behavior below is implemented purely in `SixelGraphicsState`,
`SixelScreenGraphicsState`, `SixelPlacement`, and the new
`SixelHistoryPlacement`.

- **Scrolling.** LF and IND at the bottom margin, RI and SD (scroll down) at
  the top margin, and the explicit `CSI Ps S`/`CSI Ps T` sequences all drive
  the same `SixelGraphicsState.AdjustActivePlacementsForScroll`/
  `MoveMainPlacementsIntoHistory` pair `Hex1bTerminal.ScrollUp`/`ScrollDown`
  already use for KGP. Full, partial, and horizontal (DECLRMM) margins are all
  supported: a placement is only shifted, cropped, or moved into history when
  it is wholly contained in the current scroll region (`IsWhollyContained`);
  a placement that straddles the region's boundary, or lies wholly outside
  it, is left completely untouched, matching real hardware's row-local
  scrolling semantics. Repeated scroll-up progressively and irreversibly
  crops a departing placement one row at a time until nothing remains
  ("progressive crop"); reverse scrolling (RI/SD) only ever shifts what is
  still active and can never resurrect a row that has already departed into
  history on an earlier forward scroll ("no resurrection"). DECSDM changes
  where a finalized Sixel graphic is initially anchored, not whether later
  ordinary scrolling moves it — the two behaviors are independent.
- **Main-screen history.** `SixelScreenGraphicsState.HistoryPlacements`
  partitions history entries by the same stable scrollback row identity
  (`rowId`) the terminal's own text scrollback buffer assigns, so a placement
  spanning the visible/history boundary keeps an independently-clippable copy
  on each side (`SixelPlacement.SliceHistoryRows`), cut from the placement's
  *current* painted window, never its original declared geometry — the
  invariant that makes "no resurrection" possible. `PruneMainHistoryRow`
  evicts exactly the placement portions no longer owned by a retained row
  (partial-crop/transfer-to-successor-row fidelity, mirroring KGP).
  `CaptureActiveSnapshot` projects history and viewport placements into one
  unified coordinate space and supports both `ScrollbackWidth.CurrentTerminal`
  (the live terminal's current width) and `ScrollbackWidth.Original` (each row's
  width at capture time) projections, matching the text scrollback buffer's
  own dual-width contract. Entering the alternate screen creates a fully
  independent `SixelScreenGraphicsState` with no `HistoryPlacements`
  partition at all — alternate-screen scrolling can never create or observe
  main-screen history, and leaving the alternate screen restores the
  untouched main-screen state exactly as #451 already guarantees.
- **Resize and reflow.** `ClipActivePlacementsToViewport`/
  `ClipActiveScreenToViewport` implement plain viewport-only resize: a
  placement's own painted window and creation-time `SixelCellMetrics` are
  never mutated by a smaller viewport, so widening the viewport back out
  reveals previously off-screen rows/columns unchanged; a placement is
  dropped only once its bounding box no longer intersects the viewport at
  all. `PrepareActiveReflow`/`ApplyActiveReflow` implement optional
  line-oriented reflow using the same `TerminalReflowAnchor`/
  `ReflowHelper.PerformReflowWithAnchors` machinery as KGP and text (Sixel's
  anchors use negative ids so they can be merged into one combined reflow
  call without colliding with KGP's positive ids): each placement moves
  atomically to wherever its single anchor point was mapped — reflow itself
  never splits a placement across rows — and is then re-partitioned into
  history vs. live viewport, and (when its anchor lands inside a
  history/discarded window) split via the same non-destructive
  `SliceHistoryRows` projection used by ordinary scrolling ("projection-only
  splitting"). A placement whose anchor could not be represented in the
  reflowed layout at all (its row was consumed elsewhere, or falls in a
  discarded/unrepresentable window) is dropped outright rather than left in
  an inconsistent state — the explicit safe behavior the issue requires.
  Sixel protocol metric changes (a `CSI Ps ; Ps ; Ps ; Ps ; Ps S` geometry
  query response, for example) never resize the terminal on their own and
  leave every existing placement's occupied footprint untouched; only an
  actual `Hex1bTerminal.Resize`/reflow call changes what is on-screen.
- **Damage persistence (#453).** Destructive text-damage state recorded on a
  `SixelPlacement` (its bounded sparse damaged-cell set) travels unchanged
  through every operation above: scroll shift, history split/crop,
  eviction, resize clip, reflow, and snapshot projection all copy or slice
  the placement without ever clearing or reinitializing its damage set, so a
  cell damaged before a scroll stays damaged after the scroll, after a
  scrollback round trip, and in a captured snapshot.
- **Partial-vertical-margin history fix.** KGP's own history-transfer gate
  (`createsKgpHistory` in `Hex1bTerminal.ScrollUp`) intentionally requires the
  scroll region to span the *entire* physical screen height before treating a
  departing row as history-worthy — a deliberate, unchanged KGP behavior.
  Sixel cannot reuse that same gate: DECSTBM lets a program declare a
  vertical margin strictly smaller than the physical terminal (a "partial
  vertical margin"), and the terminal's own text scrollback buffer already
  captures the departing row in that case. Before this stage, a Sixel
  placement that fully departed such a region in a single scroll step (for
  example, a one-row-tall placement anchored at the region's top row) was
  simply deleted with no history transfer and no cropped remainder — a
  silent, permanent data loss the existing full-height-only test fixtures
  never exercised. The fix introduces a separate, less-restrictive
  `createsSixelHistory` condition (the scrollback-capture guard, without the
  full-height requirement) and makes `SixelGraphicsState.MoveMainPlacementsIntoHistory`
  accept the active `SixelScrollRegion` and gate each placement on
  `IsWhollyContained` before shifting it — exactly the same containment test
  `AdjustActivePlacementsForScroll` already used, so a placement outside a
  partial region is still left untouched. KGP's `createsKgpHistory` gate and
  `KgpTerminalGraphicsState.MoveMainPlacementsIntoHistory` call are completely
  unchanged by this fix.
- **Extracted vs. kept KGP-only:** the same split #451 established still
  holds. Scroll/clip/history-partition/reflow-anchor *mechanics* are shared
  conceptually with KGP (mirrored, not inherited — the two graphics states
  remain separate types with no compile-time coupling); public IDs, delete
  selectors, relative placement graphs, and z-index remain genuinely
  KGP-only concepts with no Sixel equivalent. All existing KGP scrolling,
  history, reflow, and snapshot tests continue to pass unmodified.

## Screens, scrollback, resize, and reflow

| Area | Selected Hex1b contract | Status or unresolved work |
|---|---|---|
| Main/alternate screen | Each screen owns independent placements; leaving the alternate screen restores the unchanged main-screen graphics | Implemented by [#451](https://github.com/mitchdenny/hex1b/issues/451) |
| Scrollback | Scrolling placements remain anchored to logical row lineage and can span visible and history rows; a placement is split across the visible/history boundary via a non-destructive crop of its *current* painted window | Implemented by [#452](https://github.com/mitchdenny/hex1b/issues/452); foot provides verified prior art |
| History eviction | Remove only the placement portions no longer owned by retained row lineage | Implemented by [#452](https://github.com/mitchdenny/hex1b/issues/452), with KGP's partial-crop/transfer-to-successor-row fidelity |
| Resize | Clip to the viewport without destroying source pixels; reveal them again when space returns | Implemented by [#451](https://github.com/mitchdenny/hex1b/issues/451)/[#452](https://github.com/mitchdenny/hex1b/issues/452): a placement is dropped only once it is wholly outside the new bounds; a partially-visible placement keeps its full underlying raster and geometry, so it reappears in full when space returns |
| Reflow | Re-anchor through the same row-lineage plan as text and KGP placements; atomic per-anchor movement, with projection-only splitting when an anchor lands in a history/discarded window | Implemented by [#452](https://github.com/mitchdenny/hex1b/issues/452) |
| Cell-metric change | Recompute occupied cells from stable pixel geometry using deterministic outward rounding; a protocol metric-query response never resizes the terminal or its placements on its own | Implemented by [#452](https://github.com/mitchdenny/hex1b/issues/452); reference-terminal behavior needs #457 testing |

No reviewed reference provided a complete answer for resize/reflow or
main/alternate-screen ownership. These decisions intentionally align Sixel with
Hex1b's protocol-neutral terminal model and existing KGP reflow machinery while
remaining explicit differential-testing targets.

## Explicitly unresolved decisions

The following decisions must remain visible until
[#457](https://github.com/mitchdenny/hex1b/issues/457) provides executable
reference-terminal evidence:

1. Which DECSDM polarity profile a given reference terminal should select. The
   inversion knob exists; the per-terminal selection does not.
2. Whether an optional palette-register-0 opaque background profile is needed
   alongside the selected captured-background behavior.
3. DECGRA's undocumented carriage-return behavior and aspect-scaled DECGNL.
4. Exact Sixel-over-Sixel compositing and partial-cell text/erase damage.
5. DECSTR effects on placements.
6. Private-mode save/restore (`CSI ? Pm s` and `CSI ? Pm r`) for Sixel modes.
7. Exact default palette values for registers 16-255 and private-register
   behavior across modern terminals.

## Evidence and running the contract

The test fixtures are small ASCII payloads embedded from
`tests/Hex1b.Tests/TestData/Sixel/`. Expected data is independently authored and
does not use `SixelEncoder`. `tests/Hex1b.Tests/Sixel/SixelPlacementLifetimeTests.cs`
is the dedicated regression suite for #451's independent placement/image
storage and lifetime accounting (multi-cell spans, dedup, overlap, geometry-only
retention, origin-cell overwrite, snapshot-held survival past active-screen
removal, and main/alternate/RIS independence).
`tests/Hex1b.Tests/Sixel/SixelScrollHistoryReflowTests.cs` is the dedicated
regression suite for #452's scrolling, main-screen history, resize, and reflow
integration (LF/IND/RI/SU/SD equivalence, full/partial vertical margins,
DECLRMM horizontal margins, progressive crop, no-resurrection reverse
scrolling, DECSDM independence, capacity-one/two history pruning, both resize
directions, fractional-pixel crops, protocol metric-change independence,
alternate-screen isolation, current/original-width scrollback projection,
#453 damage persistence across scroll/history/snapshot, and final-reference
release) — including the partial-vertical-margin history transfer fixed by
this stage.

```bash
dotnet test tests/Hex1b.Tests/Hex1b.Tests.csproj \
  --filter "FullyQualifiedName~Hex1b.Tests.Sixel."
```

The terminal-first demo sends independently authored raw Sixel bytes through
`Hex1bTerminal`. It does not use `SixelWidget` or `SixelEncoder`.
`samples/SixelTerminalDemo/RawGraphicsStateScenes.cs` includes scenes
demonstrating #451's independent placement ownership: two placements sharing
identical raster content, overlapping placements that both survive,
main/alternate screen isolation, and geometry-only placement retention. The
shared-raster scene separates its two placements by row because native Sixel
renderers do not all preserve multiple same-row graphics consistently; the
overlap scene covers same-row ordering as a deliberate, separate contract.
`samples/SixelTerminalDemo/RawScrollHistoryReflowScenes.cs` adds #452's
scrolling/history/crop/prune/margin/resize/reflow/alternate/damage scenes
using the same raw-DCS convention. Its headless output is the authoritative
evidence for these scenes: it reports scrollback-line, active-placement, and
tracked-image counts, each active placement's declared vs. painted geometry,
the current viewport's observed rows/columns after clipping, and — for
placements that live purely in history — their own origin-cell coverage, so
#453 damage persisting into a scrolled-past row is directly inspectable
without an interactive terminal. Because interactive terminals render Sixel
graphics as an overlay independent of the text grid, an interactive session of
these scenes can only show the *current* screen's pixels; the headless model
above is authoritative for history/crop/reflow geometry that has already
scrolled out of view or been reflowed, and the differences between what an
interactive terminal can show live versus what the headless evidence reports
are called out explicitly in each scene's description.

The demo presents one subject per screen. Each screen clears the display, resets
margins, origin mode, and DECSDM so it cannot inherit state from the screen
before it, then draws its subject and waits. Enter or Space advances, `p` goes
back, and `q` quits. Screens are numbered so a specific one can be named in
review and reopened directly.

The description is drawn below the image, after the graphic has been placed. A
Sixel graphic is painted at the cursor, and some screens deliberately anchor it
at the page origin, so a description above the image would be overpainted.
Each description states the expected colour and the size in both pixels and
cells, so a screen can be checked against what is actually on the terminal.

```bash
dotnet run --project samples/SixelTerminalDemo
dotnet run --project samples/SixelTerminalDemo -- --screen 17
dotnet run --project samples/SixelTerminalDemo -- --scene "Declared extent"
dotnet run --project samples/SixelTerminalDemo -- --scene "Scrolling"
dotnet run --project samples/SixelTerminalDemo -- --headless
```

`--headless` prints the numbered screen list together with the parsed model and
the observed cursor, mode, and margin results, so the same screen numbers can be
checked without a Sixel-capable terminal.

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
