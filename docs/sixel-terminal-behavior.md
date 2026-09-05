# Sixel Terminal Behavior Contract

> **Status**: Evolving contract for [#445](https://github.com/mitchdenny/hex1b/issues/445)
> **First executable stage**: [#448](https://github.com/mitchdenny/hex1b/issues/448)
> **Independent graphics state**: [#451](https://github.com/mitchdenny/hex1b/issues/451)
> **Snapshots, exports, recording, and replay**: [#456](https://github.com/mitchdenny/hex1b/issues/456)
> **Capability discovery and protocol cell metrics**: [#455](https://github.com/mitchdenny/hex1b/issues/455)
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

## Immutable snapshots, export, recording, and replay (#456)

[#456](https://github.com/mitchdenny/hex1b/issues/456) makes Sixel graphics
first-class in `Hex1bTerminalSnapshot`, SVG/HTML export, and HMP1 state
synchronization/recording/replay, analogous in capability to KGP's existing
snapshot/export/replay support but exposing no KGP-only protocol concept (no
image IDs, no delete selectors, no z-index — see "Independent graphics state
(#451)" above for the full extracted/kept-KGP-only split, which this stage
does not revisit). No parser, rasterizer, cursor, graphics-state, damage,
history, or reflow behavior from #448-#453 changes; this stage only adds
read paths over that authoritative state.

- **Snapshot model.** `Hex1bTerminalSnapshot.SixelPlacements` (an
  `IReadOnlyList<SixelPlacement>`) and `SixelImages` (an
  `IReadOnlyDictionary<byte[], SixelData>` keyed by content hash) are now
  public, mirroring the shape of the existing `KgpPlacements`/KGP image
  surfaces. `SixelPlacement` and the `SixelData` properties it exposes
  (`Image`, `Row`, `Column`, `WidthInCells`/`HeightInCells`,
  `PaintedRowOffset`/`PaintedRowCount`/`PaintedColumnOffset`/`PaintedColumnCount`
  and their derived `PaintedTop`/`PaintedBottom`/`PaintedLeft`/`PaintedRight`,
  `Sequence`, `CreatedAt`, `IsGeometryOnly`, `HasPaintedExtent`,
  `HasVisiblePaintedCells`, `CoversCell`, `IsCellDamaged`, `GetVisiblePixels`,
  and the new `GetPaintedPixels`) were promoted from `internal` to `public`
  for this stage; everything else on both types remains internal. `SixelData`
  itself gained public `ContentHash`, `Outcome`, `Diagnostics`,
  `BackgroundMode`, `RasterStatus`, `RasterDiagnostics`, `Extents`, and
  `CellMetrics` so a consumer can inspect a placement's authoritative parser
  outcome and geometry without reaching into internals. Whether a placement
  is a viewport or history placement is derived, not stored: a placement's
  `Row` is unified with the text scrollback buffer's own numbering (a `Row`
  at or above `ScrollbackLineCount` is history, below it is viewport),
  exactly the coordinate space `CaptureActiveSnapshot` already established
  for #452. `CreateSnapshot(scrollbackLines:)` therefore continues to select
  viewport-only (`0`), history-inclusive (`>0`), and
  `ScrollbackWidth.CurrentTerminal`/`ScrollbackWidth.Original` projections
  with no Sixel-specific parameter — the same call already used for text and
  KGP.
- **Retention and disposal.** `SixelData` has no reference-counting
  mechanism of its own (unlike `TrackedHyperlink`, which the snapshot does
  release in `Dispose()`) — it is a plain garbage-collected object, and its
  raster is retained once per referenced image, never per covered cell,
  exactly as the live `SixelImageStore` already guarantees (see "Independent
  graphics state (#451)" above). A snapshot's placements reference the same
  `SixelData` instance as the live placements they were captured from, and
  two independently captured snapshots that both reference the same content
  hash share that same instance; disposing one snapshot has no effect on
  another snapshot's (or the live terminal's) access to that instance, and
  `Hex1bTerminalSnapshot.Dispose()` is idempotent, so it can never
  double-release Sixel state because there is nothing Sixel-specific to
  release. `tests/Hex1b.Tests/Sixel/SixelSnapshotSharingTests.cs` is the
  dedicated regression suite for this contract.
- **Automation API.** `ContainsSixelData()` and the other existing
  compatibility surfaces remain, now backed entirely by the placement/image
  model above rather than any separate bookkeeping — there is exactly one
  authoritative source for "does this cell show Sixel graphics." Deterministic
  assertions over image/placement counts, dimensions, RGBA pixels
  (`GetVisiblePixels`/`GetPaintedPixels`), anchor/occupied cells, source crop,
  history-vs-viewport (via unified `Row`), parser outcome/geometry-only
  downgrade (`IsGeometryOnly`, `Image.Outcome`, `Image.Diagnostics`,
  `Image.RasterStatus`, `Image.RasterDiagnostics`), and cursor/graphics-state
  correspondence are all available directly from the public surface above; no
  new bespoke assertion type was introduced.
- **SVG/HTML export.** `TerminalRegionSvgExtensions`/
  `TerminalRegionHtmlExtensions` render Sixel using
  `SixelPlacement.GetPaintedPixels()` — the exact snapshot pixels within the
  placement's current painted/visible crop rectangle (never the full
  declared image, so scrolling, margin clipping, and history eviction that
  cropped the placement are reflected exactly), mapped through the image's
  own `SixelCellMetrics` so cell geometry matches the snapshot precisely. A
  geometry-only placement (the rasterizer could not produce pixels) renders
  an explicit dashed-outline placeholder with a `<title>` diagnostic built
  from `Image.Outcome`/`Image.RasterDiagnostics`/`Image.Diagnostics` —
  `#456` forbids silently omitting it — occupying the same painted cell
  rectangle a rasterized placement would, so overall export geometry never
  depends on whether an image happened to rasterize. HTML export's
  interaction payload reports the same `geometryOnly`/`outcome` metadata per
  cell. Both exporters reuse the snapshot's already-decoded `SixelData`
  directly; neither reparses, redecodes, or rehashes the payload. Repeated
  export of the same snapshot is byte-identical
  (`SvgExport_RepeatedExportOfSameSnapshot_IsByteIdentical`,
  `HtmlExport_RepeatedExportOfSameSnapshot_IsByteIdentical`). KGP's own
  export output, layering, and z-order behavior are unmodified by this
  stage — the two placement kinds are painted through the same z-ordered
  loop they already shared, keyed by each placement's own `Sequence`.
- **Recording, state sync, and replay.** Two independent mechanisms exist,
  matching the pre-existing KGP split:
  - `Hmp1SixelStateReplay` (internal) extends the live HMP1 state-sync path
    exactly as `Hmp1KgpStateReplay` already does for KGP: it writes plain
    cursor-position + Sixel DCS escape sequences that a freshly joining
    peer's own terminal parses through the ordinary live path, so replay
    reconstructs state through the same authoritative parser/rasterizer used
    for live processing rather than a separate code path. Because Sixel
    placements (unlike KGP's) own the character cells they occupy, replay
    also emits a trailing "damage patch" that restores exactly the damaged
    cells' original content after placement recreation would otherwise
    re-blank them. Rasterized placements are replayed via a fresh,
    self-contained re-encode of their already-decoded pixels
    (`SixelExactEncoder`, not `Hex1b.Surfaces.SixelEncoder`, which is
    lossy/quantizing and reserved for widget authoring) rather than the
    placement's original payload, because that payload may depend on
    persistent color-register state the joining peer's terminal never saw;
    a geometry-only placement has no decoded pixels and is safe to replay
    verbatim, since its outcome is a deterministic function of the payload's
    own declared extents. Only the viewport is replayed here, matching
    `Hex1bTerminal.CreateSnapshot()`'s existing zero-scrollback state-sync
    scope.
  - `Hmp1SixelRecording` (internal) is a new, versioned binary format for
    the explicit record/serialize/replay/compare scenarios the plain-bytes
    wire replay above cannot express (truncation, unsupported version,
    missing references, invalid geometry, resource limits). It serializes
    an `IReadOnlyList<SixelPlacement>` — the same type the live snapshot and
    live wire replay use — into a `SXRC`-tagged, versioned
    (`Hmp1SixelRecording.CurrentVersion = 1`) stream: an image table
    deduplicated by `SixelData.ContentHash` (content-addressed, so a raster
    shared by multiple placements is never repeated in the stream), followed
    by placements referencing that table by index, each carrying its
    geometry, painted crop, sequence, creation time, and anchor-relative
    damaged-cell offsets. Rasterized images are re-encoded byte-exact via
    `SixelExactEncoder`; geometry-only images retain their original payload
    verbatim, for the same reason `Hmp1SixelStateReplay` does. Deserializing
    validates every field explicitly and throws
    `Hmp1SixelRecordingException` (never returns a success-shaped partial
    result, never a broad catch) tagged with a specific
    `Hmp1SixelRecordingFailureReason`: `Malformed` (bad magic marker,
    negative counts, unrecognized raster status), `UnsupportedVersion`,
    `Truncated` (stream ends before declared data), `MissingImageReference`
    (a placement's image index is out of range), `InvalidGeometry`
    (non-positive cell dimensions or negative painted extents), or
    `ResourceLimitExceeded` (`MaxPlacementCount`/`MaxImageCount` = 4096,
    `MaxPayloadLength` = 64 MiB, `MaxDamagedCellCount` = 2^20). Recorded
    placements/images (`Hmp1SixelRecordedPlacement`/`Hmp1SixelRecordedImage`)
    are plain immutable data carriers, not `SixelPlacement`/`SixelData`
    themselves, keeping the recording format decoupled from the live
    graphics-state types' internal invariants.
- **Coverage.** In addition to the export/sharing/recording suites named
  above: `tests/Hex1b.Tests/Hmp1/Hmp1SixelRecordingTests.cs` round-trips
  single and multi-placement recordings (including image-table
  deduplication, anchor-relative damage offsets, geometry-only payload
  preservation, main/alternate-screen isolation, and distinct
  history-vs-viewport unified row offsets across a scroll), replays a
  recording's escape-sequence reconstruction into a fresh terminal and
  compares resulting pixels, and exercises every `Hmp1SixelRecordingException`
  failure mode named above. `tests/Hex1b.Tests/Hmp1/Hmp1SixelStateReplayTests.cs`
  covers the existing live wire replay path, including the damage-patch
  restoration. All pre-existing KGP snapshot/export/replay and terminal
  automation tests continue to pass unmodified, confirming this stage adds
  a parallel Sixel path without touching KGP's.
- **Explicitly out of scope for this stage** (see the exclusions in
  [#456](https://github.com/mitchdenny/hex1b/issues/456)): capability probing
  ([#455](https://github.com/mitchdenny/hex1b/issues/455)), native
  presentation protocol translation
  ([#458](https://github.com/mitchdenny/hex1b/issues/458)), and any
  `SixelWidget`/`Surface`-side preview generation.

## Capability discovery and protocol cell metrics (#455)

[#455](https://github.com/mitchdenny/hex1b/issues/455) answers a question no
earlier stage needed to ask: given an effective upstream presentation, can it
actually turn Sixel bytes into pixels a human can see, and if so, what protocol
cell size should occupancy math use? This stage adds no raster decoding, no
Sixel-to-KGP/iTerm2 translation ([#458](https://github.com/mitchdenny/hex1b/issues/458)),
and no `SixelWidget`/`Surface`-side fallback logic — it only discovers and
reports, safely and without ever consuming, reordering, or duplicating a byte
of user input or a terminal's own query response.

### Support vs. parser capability

Hex1b's Sixel *parser* always understands Sixel DCS sequences, unconditionally,
regardless of what sits downstream — that has been true since
[#448](https://github.com/mitchdenny/hex1b/issues/448) and does not change here.
`SixelPresentationSupport` (`Hex1b.Sixel`) is a deliberately separate question:
can the *effective presentation* render those bytes at all?

| Value | Meaning |
|---|---|
| `Unknown` | Discovery has not yet run, timed out, or could not be completed. Nothing is known either way. This is the enum's default value (numeric `0`), so an unconfigured `TerminalCapabilities.SixelSupport` reads as "unknown" rather than as a false claim of "confirmed unsupported." |
| `None` | Discovery ran and positively determined the effective presentation cannot render Sixel (for example, DA1 replied without declaring parameter 4, or an adapter explicitly declared no support). Distinct from `Unknown` — see "Unknown vs. unsupported" below. |
| `Native` | A real, Sixel-understanding terminal sits behind the presentation and receives Hex1b's Sixel DCS bytes unmodified (raw passthrough). |
| `Translated` | Sixel is rendered by converting Hex1b's raster into a different image protocol before it reaches the presentation. [#458](https://github.com/mitchdenny/hex1b/issues/458) implements the KGP conversion; an iTerm2 conversion is deferred behind the same protocol-neutral raster sink until [#430](https://github.com/mitchdenny/hex1b/issues/430)/[#433](https://github.com/mitchdenny/hex1b/issues/433) land — see "Routing, translation, and managed presentation (#458)" below. |
| `Headless` | There is no real display; `Hex1bTerminal`'s own graphics-state model (from [#451](https://github.com/mitchdenny/hex1b/issues/451)/[#452](https://github.com/mitchdenny/hex1b/issues/452)/[#456](https://github.com/mitchdenny/hex1b/issues/456)) is the sole, authoritative source of truth. |

`TerminalCapabilities.SixelSupport` carries this value; the older
`TerminalCapabilities.SupportsSixel` boolean remains for back-compatibility and
must be kept consistent with it (`true` only when `SixelSupport` is `Native`,
`Translated`, or `Headless` — never for `Unknown` or `None`) by any adapter
that participates in discovery. Workload-facing feature reporting (the DA1
reply below) advertises Sixel to a hosted workload only when the effective
path is `Native`, `Translated`, or an authoritative `Headless` model — parser
capability alone is never sufficient, and both `Unknown` and `None` always mean
"do not advertise." Advertisement logic is written as an allowlist
(`is Native or Translated or Headless`) rather than a `!= None` denylist,
precisely so that adding `Unknown` to the enum could not silently start being
treated as advertisable.

### Unknown vs. unsupported

Two different kinds of "no" must never collapse into one, and — unlike an
earlier draft of this stage — that distinction lives in the capability model
itself, not only in optional, adapter-specific diagnostics:

- **Unknown** (`SixelPresentationSupport.Unknown`) — discovery has not run,
  timed out, or could not be completed. Nothing is known either way.
- **Unsupported** (`SixelPresentationSupport.None`) — discovery ran and
  positively determined the presentation cannot render Sixel (for example, DA1
  replied without declaring parameter 4).

Both values still reach the same workload-facing answer ("do not advertise
Sixel"), but they are separately observable from `TerminalCapabilities.SixelSupport`
alone, without also needing to inspect an adapter's probe diagnostics.
`ConsolePresentationAdapter` additionally exposes
`SixelCapabilityProbeDiagnostics.Da1DeclaresSixel` (`bool?`) for a finer-grained
view of *why*: `null` means DA1 never answered or answered unparseably (and
`SixelSupport` is `Unknown`), `false` means it answered and declared no Sixel
support (and `SixelSupport` is `None`), `true` means it declared support (and
`SixelSupport` is `Native`). The same nullable-first discipline applies to cell
metrics: `TerminalCapabilities.SixelCellMetrics` (`Sixel.SixelCellMetrics?`) is
`null` for "unknown," never a silent `SixelCellMetrics.Unknown` (the documented
10x20 fallback) — that fallback is applied only once, at the moment a
placement is actually created, via `SixelCellMetrics.FromCapabilities`/the
terminal's own `SixelCellMetrics` accessor. Discovery itself never invents a
number it did not obtain or derive.

### Discovery precedence

Cell-metrics discovery consults sources in strict precedence order and stops
as soon as a sufficient, authoritative answer exists. `ConsolePresentationAdapter`
implements exactly this order in `ResolveSixelCapabilities`:

1. **Direct declaration.** `ConsolePresentationAdapter.WithSixelSupport(support, metrics)`
   lets a host that already knows the answer (from its own configuration)
   report it outright. This is the highest-precedence source and pre-empts
   probing entirely — no DA1 or XTWINOPS query is sent once it has been
   called.
2. **`CSI 16 t`** (XTWINOPS "report cell size in pixels"), replying
   `CSI 6 ; height ; width t`. Preferred for Sixel over any physical/OSC
   value, even when they disagree, because it is the value xterm and Windows
   Terminal derive specifically for the Sixel/character-cell grid rather than
   a font metric.
3. **`OSC 1337;ReportCellSize`** (iTerm2), where supported.
4. **`CSI 14 t`** (text-area size in pixels) divided by **`CSI 18 t`**
   (rows/columns) — both queried, then the pixel extents divided by the grid
   to derive a fractional per-cell size. Both replies must arrive and parse
   before this tier can produce a value.
5. **`TIOCGWINSZ`** pixel fields (a local syscall, no round trip), used only
   when the driver reports nonzero, trustworthy values and only as the last
   resort before falling back to "unknown."
6. **Environment variables** are never consulted as evidence of support or
   metrics anywhere in this precedence chain — they exist only as
   terminal-identification hints elsewhere in the codebase (for example
   reflow-strategy auto-detection) and must not influence Sixel discovery.

Every dimension a source reports is validated before acceptance: zero,
negative, non-finite (`NaN`/`Infinity`), and implausibly large values (window
pixel extents above one million, cell extents above the adapter's plausible
cell-dimension ceiling) are explicitly rejected, never silently clamped or
substituted. A response that parses correctly but fails this plausibility
check is recorded with outcome `Rejected`; a response that cannot be parsed at
all is recorded `Malformed`; a source that never replies within the bounded
probe deadline is `TimedOut`; a source deliberately skipped because a
higher-precedence source already produced a sufficient answer is
`NotAttempted`. These four outcomes are `SixelMetricsProbeOutcome`, and one
`SixelMetricsProbeAttempt` per source (in precedence order) is recorded in
`SixelCapabilityProbeDiagnostics.Attempts` regardless of which source
ultimately won, so a caller can always see what every tier reported — not just
the winner. When two or more sources are independently accepted but disagree
by more than half a pixel in either dimension,
`SixelCapabilityProbeDiagnostics.MetricsDisagreement` is set and
`DisagreementDetail` names both values and which one the documented precedence
selected; the disagreement is surfaced as a diagnostic, never silently
resolved by an undocumented tie-break.

A parser limitation is worth naming explicitly because it shapes what
`Malformed` can and cannot mean for the `CSI 16/14/18 t` tiers specifically:
`TryConsumeWindowOperationResponse`'s response scanner only continues through
bytes that are digits or `;`. The instant it meets any other byte before the
terminating `t`, it treats the candidate as "not a window-operation reply at
all" and leaves the buffered bytes untouched, rather than classifying it as
malformed. This is a deliberate safety choice, not an oversight: it is the
only way to guarantee a workload's ordinary keyboard input that merely
*begins* like a window-op reply (for example, the Delete key's `CSI 3 ~`) can
never be misconsumed as a truncated or garbled probe response. Two
consequences follow: a genuinely non-numeric reply (`"abc"` where a number was
expected) can never be diagnosed as `Malformed` for these three tiers — it
simply never matches, and the tier eventually reports `TimedOut` — and a
negative window-op value can never arrive as a recognized reply either, since
`-` falls outside the same digit/semicolon character class (real terminals do
not emit negative window-op values, so this is not a practical limitation).
`Malformed` for `CSI 16/14/18 t` is reachable only for structurally-valid-but-
unparseable content, such as an empty parameter field (`CSI 6;;20 t`).
OSC 1337's payload has no such character-class pre-filter — it captures the
full payload verbatim up to the string terminator — so implausible values
including negative numbers are parsed successfully and then rejected on
plausibility, exercising the `Rejected` outcome distinctly from `Malformed`.

### Sixel support discovery

Support itself (as opposed to metrics) is discovered through Primary Device
Attributes (DA1): `ConsolePresentationAdapter` sends a bare `CSI c` probe and
parses the reply for DEC conformance parameter `4` among the reported
attributes. Only replies carrying the `?` private-parameter marker — which
every DA1 reply this library targets includes — are treated as DA1 responses
at all, so a workload's own bare `CSI c` query can never be misinterpreted as
a probe reply. Support is a strict tri-state, mirrored in the diagnostics'
`Da1DeclaresSixel` as `bool?` and in `SixelPresentationSupport` itself: DA1
timed out or replied unparseably → unknown (`SixelSupport = Unknown`,
`Da1DeclaresSixel = null`); DA1 replied without parameter 4 → confirmed
unsupported (`SixelSupport = None`, `Da1DeclaresSixel = false`); DA1 replied
with parameter 4 → confirmed native support (`SixelSupport = Native`,
`Da1DeclaresSixel = true`). A direct declaration via `WithSixelSupport` skips
this probe entirely, including on paths declared `None` or `Unknown` where
sending a visible DA1 query would be unnecessary and, on some terminals,
produce a visible response.

### Caching and invalidation

The probe runs at most once per `ConsolePresentationAdapter` instance, the
first time `EnterRawModeAsync` executes it (or never, if a direct declaration
pre-empted it). Results are cached on `Capabilities` for the adapter's
lifetime. Two invalidation triggers exist:

- **Resize.** A resize can change a physical terminal's real cell pixel size,
  but the adapter cannot re-run a live query probe mid-session without risking
  disruption to the workload's own output stream. Instead, `SixelCellMetrics`
  derived from window-pixel/grid division (`SixelCellMetricsSource.Derived`)
  is invalidated back to `null` ("unknown") on every resize, so a stale
  derived value is never trusted after the geometry it was computed from has
  changed; `Native`/`Csi16`/`Osc1337`-sourced metrics are left untouched, since
  those already describe the protocol grid directly rather than being
  recomputed from window pixels. Sixel *support* itself (`SixelSupport`) is
  never invalidated by resize — a terminal's fundamental Sixel capability does
  not change when its window is resized.
- **Presentation replacement / reconnect.** Creating a new
  `ConsolePresentationAdapter` (or any other presentation adapter) starts with
  a fresh, unprobed capability set; there is no cross-instance cache to
  invalidate. A caller that reconnects by constructing a new adapter gets a
  fresh discovery pass on its next `EnterRawModeAsync`.

Either trigger affects only **future** placements. `TerminalCapabilities` is
read live (never cached inside `Hex1bTerminal`) at the moment a new placement
is created, but an already-created `SixelPlacement`'s recorded
`SixelData.CellMetrics` is a permanent, immutable snapshot from its creation
time — this invariant predates this stage (`SixelCellMetrics`'s own
documentation) and is exercised end-to-end by the pre-existing
`SixelScrollHistoryReflowTests.ProtocolMetricChange_WithoutResize_LeavesExistingPlacementUnaffected`,
together with this stage's own resize-invalidation coverage in
`SixelCapabilityDiscoveryTests`.

### Query ownership

A hosted workload queries its terminal for DA1/window-operation information
the same way any application does: by writing the query's raw escape sequence
to its own output stream, exactly as if it were talking directly to a real
terminal. Exactly one side must answer each query — never zero, never two:

| Presentation | Who answers | Why |
|---|---|---|
| `ConsolePresentationAdapter` (native raw upstream) | The real terminal, directly | Raw bytes flow through to it unmodified; a synthetic Hex1b reply would arrive as an unwanted duplicate in the workload's input. |
| `HeadlessPresentationAdapter` | `Hex1bTerminal`, synthesized from its own authoritative model | There is no real terminal to answer at all. |
| `WebSocketPresentationAdapter` (managed browser presentation) | `Hex1bTerminal`, synthesized | The browser side is not an independent terminal emulator that autonomously answers VT queries; Hex1b owns the reply. |
| A future translated (`Translated`) raster-graphics presentation | `Hex1bTerminal`, synthesized | Same reasoning as WebSocket: the real answering party is Hex1b's own graphics model, translated for display, not an independent terminal emulator. |

This is implemented by a single presentation-adapter property,
`IHex1bTerminalPresentationAdapter.AnswersProtocolQueriesDirectly` (default
`false`): a presentation adapter overrides it to `true` only when it connects
Hex1b directly to a real, independent terminal emulator whose raw
stdin/stdout Hex1b merely forwards — currently only
`ConsolePresentationAdapter`. `Hex1bTerminal.HandleDeviceAttributesQuery` and
`HandleWindowOperationQuery` both check
`_presentation.AnswersProtocolQueriesDirectly` first and return immediately
without sending anything when it is true, guaranteeing the real terminal's own
reply is the only one that ever reaches the workload. For every other
presentation, `Hex1bTerminal` is the single, deterministic answerer:

- **DA1** (`CSI c`/`CSI 0 c`, recognized without a private-mode prefix per
  `AnsiTokenizer`) replies `\x1b[?62;4c` (VT220-class identity plus Sixel,
  parameter 4) when `Capabilities.SixelSupport` is `Native`, `Translated`, or
  `Headless` (or `Capabilities.SupportsSixel` is set), or `\x1b[?62c`
  otherwise — including for both `Unknown` and `None`, since neither is an
  affirmative "yes." This reply format is a Hex1b-owned synthetic identity,
  not verified byte-for-byte against a specific real terminal's own DA1
  string.
- **`CSI 18 t`** (report text-area size in characters) replies
  `\x1b[8;{rows};{cols}t` from the terminal's own row/column count.
- **`CSI 14 t`** (report text-area size in pixels) replies
  `\x1b[4;{heightPixels};{widthPixels}t`, computed by multiplying rows/columns
  by the terminal's current `SixelCellMetrics` (falling back to the documented
  10x20 estimate only when metrics are genuinely unknown) and rounding to the
  nearest pixel.
- **`CSI 16 t`** (report cell size in pixels) replies
  `\x1b[6;{height};{width}t` from the same `SixelCellMetrics`, rounded.

Only the report-style window operations recognized by `AnsiTokenizer` as a
`WindowOperationToken` (`CSI 14/16/18 t` specifically) ever reach these
handlers; any other `Ps` value remains an unrecognized sequence and is not
routed here. Capability changes (from discovery completing, a resize
invalidating derived metrics, or a direct declaration) propagate to these
handlers deterministically because `Hex1bTerminal.Capabilities` is a live
passthrough to `_presentation.Capabilities` — it is never cached or
snapshotted inside `Hex1bTerminal` itself, so the very next query answered
after a capability change always reflects the new value.

### Console probe integration

All of the above, for `ConsolePresentationAdapter`, is implemented as an
extension of its pre-existing KGP/background-color probe pass in
`ProbeCapabilitiesAsync` — not a second, competing reader. A single bounded
read loop demultiplexes DA1, `CSI 16/14/18 t`, `OSC 1337;ReportCellSize`, and
the existing KGP/OSC 11 background-color replies by their exact wire
signatures, regardless of fragmentation (a reply split at any byte boundary
across multiple reads is still recognized) or interleaving (replies for
different queries, and arbitrary workload/keyboard input, can arrive mixed
together in any order and are all still recognized independently). The loop
terminates as soon as every expected reply has been accounted for (answered,
malformed, or otherwise resolved) or a single shared deadline
(`_kgpProbeTimeout`) elapses, whichever comes first — it never blocks
indefinitely. Every byte that is not consumed as part of a recognized reply —
including a fully unrelated reply, ordinary keyboard input, or a partially
read fragment of a reply that eventually times out — is preserved
byte-for-byte, in original order, into `_prefetchedInput`, so nothing the
probe does not explicitly recognize and consume is ever lost, reordered, or
duplicated. On Windows, `ConsolePresentationAdapter` skips the entire Sixel
probe outright (Windows console input records are not a raw byte stream
compatible with these replies) and reports every tier as `NotAttempted` with
an explicit diagnostic reason, rather than attempting to read a stream that
does not exist for that platform — Sixel support and metrics stay unknown on
Windows unless declared directly via `WithSixelSupport`.

### Explicitly out of scope for this stage

Per the exclusions in [#455](https://github.com/mitchdenny/hex1b/issues/455):
no changes to raster decoding, no Sixel-to-KGP/iTerm2 translation (the
`Translated` enum value is a placeholder for
[#458](https://github.com/mitchdenny/hex1b/issues/458), which alone implements
it), and no `SixelWidget`/`Surface`-side fallback logic. Broad conformance
hardening beyond the safe, bounded probing described above is likewise
deferred; this stage answers "what can the presentation do and how big is a
cell," not "make every terminal work perfectly."

## Routing, translation, and managed presentation (#458)

[#458](https://github.com/mitchdenny/hex1b/issues/458) answers the question
[#455](https://github.com/mitchdenny/hex1b/issues/455) deliberately left open:
given an effective `SixelPresentationSupport`, what does `Hex1bTerminal` *do*
with a Sixel DCS sequence? This stage adds the routing decision itself, a
protocol-neutral managed raster event stream for non-native presentations, a
Sixel-to-KGP translator, an opt-in unsupported-presentation placeholder
policy, and an opt-in sanitization policy — all built on top of #451/#452/#455's
existing authoritative placement model without introducing a second decoder
or a competing event channel.

### Routing matrix

`Hex1bTerminal` computes one `SixelEffectiveRoute` per batch from
`Capabilities.SixelSupport`, `Capabilities.SupportsKgp`, and whether the
active presentation implements `ISixelRasterPresentationSink`. The route
governs where the batch's raster events (if any) are delivered; it never
governs whether native bytes are forwarded — see "Native passthrough is
unconditional" below.

| `SixelPresentationSupport` | Managed sink attached? | Effective route | What happens |
|---|---|---|---|
| `Native` | any | Native | Byte-exact passthrough only. Raster events are still computed (for diagnostics/managed-sink correctness) but are not required for rendering — a real terminal already renders the bytes itself. |
| `Native` | Yes (`ISixelRasterPresentationSink`) | ManagedRasterSink | The documented dual-delivery case: raw Sixel bytes keep reaching the presentation exactly as `Native` above, and the managed sink additionally observes the same batch as structured events. |
| `Headless`/`Translated`/`None`/`Unknown` | Yes (`ISixelRasterPresentationSink`) | ManagedRasterSink | The managed sink takes priority over the raw capability value for routing structured events — a presentation that wants events gets them regardless of what `SixelSupport` otherwise reports — but raw Sixel wire bytes are withheld: only `Native` reaches raw bytes, so a non-Native managed sink never receives raw, uninterpretable Sixel DCS bytes it never asked to reparse. `SixelRoutingIntegrationTests.ManagedSink_Headless_NeverReceivesRawSixelWireBytes` and `ManagedSink_Translated_NeverReceivesRawSixelWireBytesAtAnySplitBoundary` are the dedicated regression tests. |
| `Headless` | No | Headless (no route action) | `Hex1bTerminal`'s own model is the sole source of truth; no output is written and no translation is attempted. Existing snapshot/export APIs from [#456](https://github.com/mitchdenny/hex1b/issues/456) remain the way to inspect this state. |
| `Translated` | No | KgpTranslated (if `SupportsKgp`) or Unsupported (otherwise) | With `SupportsKgp`, the Sixel raster is translated to KGP image/placement operations (see "KGP translation" below). Without it, no translation target exists yet (iTerm2 is deferred — see "iTerm2 deferral" below), so the route falls back to Unsupported and a `TranslationUnavailable` diagnostic is raised. |
| `None` / `Unknown` | No | Unsupported | The configured `SixelUnsupportedPresentationPolicy` applies (see "Unsupported-presentation policy" below). |

Effective capability reported to a hosted workload (the DA1 reply) is
unaffected by this stage's route computation directly, but #458 does correct
a related pre-existing bug in that reply: `Translated` with `SupportsKgp` now
correctly advertises Sixel (a translation route exists), where it previously
did not, because the reply logic checked raw `SixelSupport` values rather than
the actual selected route. `Da1Query_TranslatedPresentationWithKgp_RepliesDeclaringParameter4`
and `Da1Query_TranslatedPresentationWithoutKgp_RepliesWithoutParameter4` in
`Hex1bTerminalQueryOwnershipTests.cs` are the regression tests for this fix.

A route change (including the terminal's very first batch, and a presentation
reconnect that changes the effective route) resets only this stage's own
dedup/visibility bookkeeping (`SixelRasterRouter`/`KgpSixelTranslator`
internal state) so a newly-attached sink or translator starts from a clean,
consistent baseline. It never rewrites `Hex1bTerminal`'s own authoritative
placement history, sequence numbers, or already-reported historical metrics —
only forward bookkeeping is reset. When the route being left is `KgpTranslated`,
this reset is preceded by an explicit `KgpSixelTranslator.ReleaseAllAsync` call
that emits a delete/release wire command for every placement and image the
translator had transmitted, so a live presentation is never left holding
stale KGP graphics it can no longer be told about once bookkeeping clears —
the presentation connection itself is never replaced or reconnected in this
architecture, only its effective route changes.
`SixelRoutingIntegrationTests.RouteChangeAwayFromKgpTranslated_ReleasesStalePlacementAndImage`
is the dedicated regression test.

### Native passthrough is unconditional

Native byte-exact forwarding — every workload byte, unchanged, including
7-bit/C1 framing, parameter spelling, palette commands, malformed/rejected
sequences, arbitrary chunk boundaries, and limit-degraded input — is
forwarded before DCS completion, parsing, allocation, hashing, snapshotting,
filtering, or translation, exactly as it was before this stage. Internal
parser/raster failure never mutates or delays it. This holds regardless of
the computed route: the only thing that changes native output at all is the
opt-in sanitization policy (below), which is a deliberate, explicit,
documented departure from this default — never a side effect of routing,
translation, or degradation.

The dedicated regression coverage for this invariant is
`SixelRoutingIntegrationTests.NativeRoute_ForwardsBytesExactlyAtEverySplitBoundary`
(every possible single-split chunk boundary of a Sixel payload produces
byte-identical output) and
`SixelRoutingIntegrationTests.NativeRoute_MalformedSixel_ForwardsBytesUnchangedAndUnmutated`
(a structurally malformed but still DCS-framed sequence — DECGRA with five
parameters, which the parser marks `SixelParseOutcome.Malformed` without
aborting the DCS string itself — still forwards unchanged). No dedicated
benchmark project exists for this specific path today; per this stage's
validation instructions, deterministic chunk-forwarding tests stand in as the
measured validation until such benchmark infrastructure exists, rather than
introducing new benchmark tooling for a single stage.

### Model divergence and degradation

Two distinct, never-silent degradation paths exist, both already anticipated
by #451/#452's placement model and #455's capability model:

- **Geometry-only downgrade** (`SixelRasterRouteDiagnosticKind.GeometryOnlyDowngrade`):
  pixel decoding/raster allocation exceeded a bounded resource limit
  (`SixelPlacement.IsGeometryOnly`), but geometry/cursor parsing remained
  trustworthy. Native forwarding is unaffected; a managed sink or the KGP
  translator still receives a placement with geometry and anchor but no
  pixel content, and `Hex1bTerminal.SixelRouteDiagnosticRaised` fires
  regardless of whether any sink is attached at all, so a host can detect
  this even in native/headless configurations with no managed presentation.
- **Desynchronization** (`SixelRasterRouteDiagnosticKind.Desynchronized`):
  the byte stream framer itself exceeded its bounded retention limit before
  geometry could be parsed at all (`SixelParseOutcome.LimitDowngraded` at the
  framer level, distinct from the rasterizer-level geometry-only case above).
  No placement is created; the terminal recovers at the next valid DCS
  boundary, and an explicit diagnostic is raised rather than silently
  dropping the sequence. Following text/cursor state remains synchronized
  whenever the framer's own boundary recovery permits it — this stage adds no
  new recovery mechanism beyond what the pre-existing framer already
  provides, only the explicit diagnostic surface.

`SixelRoutingIntegrationTests.NativeRoute_GeometryOnlyDowngrade_IsObservableViaDiagnosticsWithoutAffectingNativeBytes`
is the dedicated test for the first case, driving the same
DECGRA-999999999x999999999 fixture #452's SVG export tests already use for
`SixelPlacement.IsGeometryOnly`.

### Protocol-neutral managed raster presentation

`Hex1b.Sixel.ISixelRasterPresentationSink` (implemented alongside
`IHex1bTerminalPresentationAdapter`) is the single, strongly typed extension
point a managed presentation — for example a WebSocket/delta client — uses to
receive Sixel graphics without reparsing the wire protocol. Its one method,
`OnSixelRasterEventsAsync`, delivers an ordered `IReadOnlyList<SixelRasterEvent>`
per output batch, interleaved with (never ahead of or independent from) the
same batch's text/cell output — no second, independently-ordered event
channel is introduced. The event hierarchy (`Hex1b.Sixel.SixelRasterEvent`)
reuses #456's already-public `SixelData`/`SixelPlacement` types rather than
inventing a parallel raster representation:

| Event | Raised when |
|---|---|
| `SixelRasterContentDefined` | A raster's content (keyed by `SixelData.ContentHash`) becomes reachable from a live placement for the first time this screen lifetime. Content already known to the sink is never retransmitted — deduplication is structural, not a sink-side optimization. |
| `SixelRasterPlacementUpdated` | A placement is created (`IsNewPlacement: true`) or an existing placement's geometry changes (scrolling, history eviction, reflow, margin clipping). Placement identity across events is `SixelPlacement.Sequence`. |
| `SixelRasterPlacementDamaged` | Text or another operation destructively damages pixels within a still-live placement's painted region without removing the placement. |
| `SixelRasterPlacementReleased` | A placement leaves the active screen's live set (scrolled past retained history, erased, screen reset/switch). |
| `SixelRasterContentReleased` | A raster's content is no longer referenced by any live placement, mirroring the same reachability sweep `SixelData`'s backing store performs. |
| `SixelRasterScreenTransition` | The active screen changes (`EnteredAlternateScreen`/`ExitedAlternateScreen`). Placements on the screen being left are released; placements already live on the screen being entered are re-announced with fresh `SixelRasterPlacementUpdated` events, since a sink cannot be assumed to retain per-screen state across the switch. |
| `SixelRasterReset` | A full RIS reset clears all Sixel graphics state. Raised once, instead of individual release events for everything that was live — "everything this sink knew is gone." |
| `SixelRasterRouteDiagnostic` | Carries one of the diagnostic kinds above, inline in the ordered stream for a managed sink, in addition to the standalone `Hex1bTerminal.SixelRouteDiagnosticRaised` event any host can subscribe to regardless of route. |

`SixelRoutingIntegrationTests.cs` covers ordered content-then-placement
delivery, content deduplication across a second placement of identical
raster bytes, release-on-scroll for both the placement and its now-unreferenced
content, alternate-screen entry/exit release-then-reannounce, a full RIS
reset, and text-overwrite damage.

### KGP translation

When the route is `KgpTranslated`, `Hex1b.Kgp.KgpSixelTranslator` (internal —
translation bookkeeping is never exposed to the workload) converts the same
ordered raster events into Kitty Graphics Protocol wire commands using the
existing `KgpImageStore`/`KgpPlacement` encoder machinery unchanged — this
stage adds no new KGP wire format, only a Sixel-shaped event source feeding
the existing one. Decoded pixels, alpha/transparency, aspect/rendered
dimensions, source crop, damage, anchor, occupied span, placement order,
screen state, and scroll/history/reset/deletion transitions are all preserved
because they are read directly from the same `SixelData`/`SixelPlacement`
values the managed-sink path above uses — there is no separate, independently
lossy translation path.

Internal KGP image/placement IDs allocated for translated content always
carry a reserved high bit (bit 31, `0x8000_0000`) that workload-authored KGP
IDs — ordinary small positive integers per the Kitty protocol's own
conventions — cannot produce, so a collision between a translated ID and a
workload's own native KGP command is structurally impossible without needing
runtime bookkeeping to detect it. `KgpTranslatedRoute_TransmitsImageThenPlacementWithReservedIdBit`
asserts this directly against the wire bytes. Unchanged raster content is
never retransmitted (content dedup carries over unchanged from the managed
sink model above — the translator observes the same `SixelRasterContentDefined`
event semantics); only the affected placements are updated or deleted, and a
placement scrolling out of any retained history emits an explicit KGP delete
command (`a=d,d=i,...`) rather than leaving stale image data referenced.
Existing KGP-native handling and the pre-existing KGP terminal-state test
suite are unchanged and continue to pass unmodified — the translator is
strictly additive on top of, never a modification of, the existing encoder.

`KgpTranslatedRoute_ScrollOffScreen_EmitsDeleteCommands` and
`KgpTranslatedRoute_IdenticalContentTwice_DoesNotRetransmitImage` are the
dedicated regression tests for deletion-on-release and content dedup at the
KGP wire level specifically (as distinct from the managed-sink event-level
dedup test above, which covers the same property one layer up the stack).

### Unsupported-presentation policy

When the effective route is `Unsupported`, `Hex1bTerminalOptions.SixelUnsupportedPresentation`
(`Hex1b.Sixel.SixelUnsupportedPresentationPolicy`) governs what, if anything,
is written to the presentation in place of a graphic a human on that
presentation cannot see. `Hex1bTerminal`'s authoritative Sixel model is never
discarded regardless of this policy — it governs presentation-side
substitution only:

- **`Suppress`** (default): no substitute output beyond whatever byte-exact
  passthrough already forwards (which most real terminals/harnesses silently
  ignore as an unrecognized DCS sequence). Preserves pre-#458 behavior
  exactly.
- **`Placeholder`**: writes a short, human-readable diagnostic placeholder
  (for example `[sixel: 320x200 image not shown — presentation cannot display
  graphics]`) for each graphic that cannot be rendered, and raises a
  `PlaceholderApplied` diagnostic, so a user watching an unsupported
  presentation is not left wondering whether something silently failed.

`UnsupportedRoute_PlaceholderPolicy_WritesDiagnosticPlaceholder` and
`UnsupportedRoute_SuppressPolicy_WritesNoPlaceholderText` are the dedicated
tests. `TranslatedRoute_WithoutKgp_RaisesTranslationUnavailableDiagnostic`
covers the specific case where `SixelSupport.Translated` was requested but no
translation target is available — a distinct diagnostic kind
(`TranslationUnavailable`) from "translation was never requested," raised
alongside whichever `SixelUnsupportedPresentationPolicy` applies.

### Sanitization is honestly incompatible with immediate forwarding

`Hex1bTerminalOptions.SixelSanitization` (`Hex1b.Sixel.SixelSanitizationPolicy`)
is an explicit, opt-in host policy (`Disabled` by default) that can suppress
malformed, rejected, oversized, or limit-downgraded Sixel data before it
reaches a native upstream presentation. Whether a sequence should be
suppressed depends on its final outcome, which is only known once the
sequence terminates — so this stage designs the tradeoff honestly rather than
claiming both immediate-forwarding and filtering simultaneously: enabling
sanitization buffers bytes for the duration of any in-progress DCS string
(bounded by the same retention limits `SixelCompatibilityPolicy` already
enforces) and flushes, suppresses, or replaces them once the outcome is
known. Ordinary text and DCS sequences unrelated to Sixel are never buffered
or affected by this policy. Each suppression raises an explicit `Suppressed`
diagnostic naming the reason (`malformed`, `geometry-only downgrade`, and so
on) — nothing is silently dropped.

`SuppressGeometryOnly` defaults to `false` even when sanitization is
otherwise enabled: a geometry-only downgrade is a legitimate, non-malformed
outcome (the rasterizer chose bounded degradation over failure), so a host
must opt in separately to also suppress it.

`SuppressCancelledOrUnterminated` and `SuppressRetentionLimitExceeded`
(both default `true` when sanitization is enabled) govern the two outcomes
that never produce a `DcsToken` at all — framing cancelled mid-string, left
unterminated, or exceeding the framer's bounded retention limit before
completion. When either is set to `false`, the affected frame's bounded
retained bytes (up to `SixelCompatibilityPolicy`'s retention limit,
introducer/parameters/payload included) are reconstructed into a
self-contained `ESC P ... ESC \` sequence and forwarded verbatim instead of
being unconditionally discarded — a dedicated internal token
(`SixelSanitizedFrameForwardToken`) carries these bytes through the pipeline
without contributing any Sixel model state, and without being reparsed as
if it were a `Complete` DCS token (unsafe, since these outcomes were never
validated as such). This forwarding only ever happens when the effective
Sixel route allows raw wire bytes to reach the presentation at all (see the
routing matrix above) — a route that never delivers raw Sixel bytes (for
example `Headless` or `KgpTranslated`) still never sees these bytes even
with the flag set to `false`, since route precedence for wire delivery is
unconditional.

`Sanitization_SuppressesMalformedGraphic_PreservesOrdinaryText`,
`Sanitization_Disabled_DefaultPreservesByteExactPassthrough`,
`Sanitization_GeometryOnlyDefaultNotSuppressed_UnlessOptedIn`,
`Sanitization_SuppressCancelledOrUnterminatedDefaultTrue_DropsCancelledFrame`,
`Sanitization_SuppressCancelledOrUnterminatedFalse_ForwardsBoundedBytesAndPreservesText`,
`Sanitization_SuppressRetentionLimitExceededDefaultTrue_DropsOversizedFrame`, and
`Sanitization_SuppressRetentionLimitExceededFalse_ForwardsBoundedRetainedBytes`
are the dedicated tests for, respectively, the suppression behavior itself,
the disabled-by-default passthrough guarantee, the separate opt-in required
for geometry-only suppression, and the true/false behavior of each of the
two previously-inert flags — all confirming unrelated text and DCS framing
remain unaffected in every case.

### iTerm2 deferral

[#430](https://github.com/mitchdenny/hex1b/issues/430) and
[#433](https://github.com/mitchdenny/hex1b/issues/433) — the iTerm2 image
protocol issues — are still open as of this stage. This stage deliberately
implements no concrete iTerm2 translator and no iTerm2-specific raster
subsystem. `ISixelRasterPresentationSink` and the `SixelRasterEvent` model are
already protocol-neutral: an iTerm2 translator, once #430/#433 establish the
shared path, would consume the exact same ordered event stream the KGP
translator consumes today, not a second decoder or a parallel raster model.
`SixelPresentationSupport.Translated` without `SupportsKgp` currently always
falls back to `Unsupported` with a `TranslationUnavailable` diagnostic — there
is no capability flag or code path that attempts iTerm2 translation yet, by
design.

### Explicitly out of scope for this stage

Per the exclusions in [#458](https://github.com/mitchdenny/hex1b/issues/458):
no `SixelWidget`/`Surface`-side fallback logic, no `SixelEncoder`-side
optimization work, no translation of arbitrary non-Sixel DCS sequences, and
no concrete iTerm2 translation (see "iTerm2 deferral" above).

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
`tests/Hex1b.Tests/Sixel/SixelSnapshotSharingTests.cs`,
`tests/Hex1b.Tests/Sixel/SixelSvgExportTests.cs`, and
`tests/Hex1b.Tests/Sixel/SixelHtmlExportTests.cs`, together with
`tests/Hex1b.Tests/Hmp1/Hmp1SixelRecordingTests.cs` and
`tests/Hex1b.Tests/Hmp1/Hmp1SixelStateReplayTests.cs`, are #456's dedicated
regression suites for the snapshot/export/recording/replay contract described
above.
`tests/Hex1b.Tests/Sixel/SixelCapabilityDiscoveryTests.cs` is #455's dedicated
regression suite for `ConsolePresentationAdapter`'s probe engine: direct
declaration short-circuiting the probe, single-source acceptance for each of
CSI16/OSC1337/CSI14+18/TIOCGWINSZ, precedence and disagreement across
conflicting sources, plausibility rejection (zero/negative/non-finite/
overflowing/malformed), the DA1 support tri-state (unknown/unsupported/
native), exhaustive single-byte-boundary fragmentation of every supported
response, exhaustive interleaving of DA1/CSI16/CSI14/CSI18/OSC1337/KGP/
background-color replies with ordinary keyboard input, timeout/malformed/
cancellation preservation of already-read bytes, and resize invalidation of
derived-but-not-authoritative metrics.
`tests/Hex1b.Tests/Sixel/Hex1bTerminalQueryOwnershipTests.cs` is #455's
dedicated regression suite for `Hex1bTerminal`'s query-ownership model: DA1 and
`CSI 14/16/18 t` replies (with and without Sixel support declared) for
non-native presentations, confirmed silence for a native
(`AnswersProtocolQueriesDirectly == true`) presentation across all four query
types, `HeadlessPresentationAdapter`'s default (no advertisement) versus
explicitly authoritative (advertises) capability reporting, and
`WebSocketPresentationAdapter`'s always-native capability declaration. All
pre-existing capability, `ConsolePresentationAdapter`, KGP-probe, WebSocket,
terminal-query, and Sixel tests continue to pass unmodified, confirming this
stage adds a parallel discovery/query-ownership path without altering any
prior-stage behavior.
`tests/Hex1b.Tests/Sixel/SixelRoutingIntegrationTests.cs` is #458's dedicated
regression suite, driven end to end through a purpose-built
`SixelRoutingTestTerminal` harness (distinct from `SixelTestTerminal` because
it needs a configurable `SixelPresentationSupport`/KGP capability/managed-sink
participation/sanitization/unsupported-presentation policy, rather than the
hard-coded native-only configuration existing Sixel tests use). It covers:
byte-exact native forwarding at every possible chunk-split boundary and for a
malformed-but-still-framed sequence; the geometry-only downgrade diagnostic
observable without affecting native bytes; ordered managed-sink delivery
(content-defined-then-placement-updated), content deduplication across a
second placement, and release-on-scroll for both placement and content;
alternate-screen entry/exit release-then-reannounce and a full RIS reset via
the managed sink; text-overwrite damage; KGP translation's reserved-ID-bit
wire format, delete-on-scroll, and content dedup at the wire level;
`TranslationUnavailable`, `PlaceholderApplied`, and `Suppressed` diagnostics
for the unsupported/sanitization policies; and the sanitization
enabled/disabled/geometry-only-opt-in-only behavior described above. The two
new DA1 route-fix tests in `Hex1bTerminalQueryOwnershipTests.cs` are described
above. All pre-existing Sixel/KGP/WebSocket/Hmp1/snapshot tests continue to
pass unmodified, confirming this stage's routing/translation/sanitization
logic is additive on top of, not a modification of, prior-stage behavior.

```bash
dotnet test tests/Hex1b.Tests/Hex1b.Tests.csproj \
  --filter "FullyQualifiedName~Hex1b.Tests.Sixel."
dotnet test tests/Hex1b.Tests/Hex1b.Tests.csproj \
  --filter "FullyQualifiedName~Hex1b.Tests.Hmp1.Hmp1Sixel"
dotnet test tests/Hex1b.Tests/Hex1b.Tests.csproj \
  --filter "FullyQualifiedName~SixelCapabilityDiscoveryTests|FullyQualifiedName~Hex1bTerminalQueryOwnershipTests"
dotnet test tests/Hex1b.Tests/Hex1b.Tests.csproj \
  --filter "FullyQualifiedName~SixelRoutingIntegrationTests"
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
`samples/SixelTerminalDemo/RawSnapshotExportReplayScenes.cs` adds #456's
snapshot/export/recording/replay scenes, again using the same raw-DCS
convention with no `SixelWidget`/`SixelEncoder` involvement. Its headless
inspector (`InspectSnapshotExportReplaySceneAsync` in `Program.cs`) goes
beyond replaying the script: it also creates multiple snapshots to prove
raster sharing and safe double-disposal, compares the four projection modes
against each other, exports SVG/HTML to confirm the geometry-only diagnostic
placeholder and byte-identical repeated export, and drives
`Hmp1SixelRecording.Serialize`/`Deserialize`/`BuildReplayEscapeSequence` to
record a snapshot, replay it into a brand-new terminal with no live upstream
connection, and compare the resulting pixels and painted (damage) extent —
including across a main/alternate screen transition — plus feeds
deliberately corrupted recordings (wrong magic marker, truncation, a bumped
version number) through `Deserialize` to show each fails with its own
explicit `Hmp1SixelRecordingFailureReason`, never a silent or success-shaped
fallback.
`samples/SixelTerminalDemo/CapabilityDiscoveryScenarios.cs` adds #455's
capability-discovery scenarios. Unlike the scenes above, discovery is a
wire-protocol probing concern with no visual/raster component, so it has no
corresponding numbered screen and runs only in the headless transcript, under
"Capability discovery and query ownership observations (#455)". It reuses the
same no-real-terminal-required approach as
`SixelCapabilityDiscoveryTests.cs`/`Hex1bTerminalQueryOwnershipTests.cs`: a
demo-local `FakeConsoleDriver` (an `IConsoleDriver` the demo can implement via
`InternalsVisibleTo`) drives `ConsolePresentationAdapter`'s probe with queued,
deterministic reply bytes, and a demo-local `ScriptedWorkloadAdapter` drives
`Hex1bTerminal`'s query-ownership behavior directly. Its eight scenarios are
direct evidence for the contract above: a direct `WithSixelSupport`
declaration writing zero probe bytes; DA1 and `CSI 16 t` replies fed one byte
at a time and interleaved with arbitrary keyboard bytes and the existing
KGP/background probe replies, with the keyboard bytes preserved byte-for-byte
and in order; `CSI 16 t` overriding a conflicting `OSC 1337` value with the
disagreement surfaced in diagnostics; a fractional cell size derived from
`CSI 14 t`/`CSI 18 t` alone; an implausible (negative) `OSC 1337` height
rejected with an explicit diagnostic detail; a resize invalidating only
`Derived`-sourced metrics while leaving `SixelSupport` itself untouched; a
later `SetSixelCellMetrics` change leaving an already-created placement's
recorded metrics unchanged while a subsequent placement (with distinct
payload content, since identical content is deduplicated by
`TrackedObjectStore.GetOrCreateSixel`) picks up the new value; and, run
against `Hex1bTerminal` directly, native-presentation silence versus
default-headless (`SixelSupport.Unknown`, "no parameter 4") versus an
explicitly declared confirmed-unsupported headless (`SixelSupport.None`,
also "no parameter 4," for a different, explicit reason) versus
authoritative-headless ("parameter 4 present") DA1 replies — demonstrating
that `Unknown` and `None` are distinct, separately observable capability
states that nonetheless agree on the workload-facing answer, matching
`Hex1bTerminalQueryOwnershipTests.cs` exactly.

`samples/SixelTerminalDemo/RoutingTranslationScenarios.cs` adds #458's
routing/translation/sanitization scenarios. Like capability discovery, this is
a headless-only concern — every route/policy combination is independent of
which paged screen (if any) is being viewed — and its six scenarios print
under "Routing, translation, and sanitization observations (#458)". Each
scenario feeds independently authored raw Sixel bytes (reusing
`RawSixelFixtures`, never `SixelWidget`/`SixelEncoder`) through a small,
demo-local `Hex1bTerminal` harness configured for one specific
`SixelPresentationSupport`/policy combination. They are direct evidence for
the contract above: a byte fed one at a time into a `Native`-route terminal,
showing the presentation's captured length grows with every single byte
(forwarding begins before the DCS terminator even arrives) and the final
bytes are exactly byte-equal to the original payload; a managed sink
receiving `SixelRasterContentDefined` before `SixelRasterPlacementUpdated`
for the first placement, exactly one content-defined event across two
placements of identical content (deduplication), and a damage event when
text overwrites the first placement's origin cell; a `Translated` route with
KGP support translating the same raster into a KGP transmit (`a=t,f=32`)
followed by a placement (`a=p,i=`) command, with the translated image id
carrying the reserved high bit that structurally prevents collision with a
workload-authored small-integer KGP id; an `Unsupported` route with the
`Placeholder` policy appending a diagnostic placeholder after the
unconditionally-forwarded raw bytes (the placeholder is always additive,
never a substitute for native forwarding) and raising a `PlaceholderApplied`
diagnostic; opt-in sanitization suppressing a malformed, DCS-framed Sixel
sequence embedded between two ordinary text fragments while leaving both
fragments intact and raising a `Suppressed` diagnostic; and a `Translated`
route's DA1 reply advertising Sixel support (parameter 4) only when KGP
support is actually present, demonstrating that the effective capability
reported to a workload matches the actual selected route rather than raw
parser support.

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
