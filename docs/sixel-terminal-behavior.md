# Sixel Terminal Behavior Contract

> **Status**: Evolving contract for [#445](https://github.com/mitchdenny/hex1b/issues/445)
> **First executable stage**: [#448](https://github.com/mitchdenny/hex1b/issues/448)
> **Independent graphics state**: [#451](https://github.com/mitchdenny/hex1b/issues/451)
> **Snapshots, exports, recording, and replay**: [#456](https://github.com/mitchdenny/hex1b/issues/456)
> **Capability discovery and protocol cell metrics**: [#455](https://github.com/mitchdenny/hex1b/issues/455)
> **Differential conformance corpus**: [#457](https://github.com/mitchdenny/hex1b/issues/457)
> **Widget and Surface integration**: [#480](https://github.com/mitchdenny/hex1b/issues/480)
> **Baseline**: DEC VT340

## Purpose

This document defines the terminal-side Sixel behavior that Hex1b tests and
implements. It is a contract for `Hex1bTerminal`, not for `SixelWidget` or the
Sixel encoder.

The DEC VT340 is the baseline. Modern-terminal differences are recorded here
and must be represented by explicit compatibility policy when unavoidable.
Hex1b must not branch on terminal names.

The tests under `tests/Hex1b.Tests/Sixel/` establish the executable form of this
contract. The finite differential corpus in
`tests/Hex1b.Tests/TestData/Sixel/Conformance/terminal-reference-matrix.json`
records the primary reference matrix, normalized expected outcomes, provenance,
and the dedicated regression suites covering the rest of the terminal contract.

The application-facing `SixelWidget` contract is documented separately in
[`src/content/guide/widgets/sixel.md`](../src/content/guide/widgets/sixel.md).
Direct render contexts emit normalized native Sixel, while Surface-backed
contexts retain structured Sixel content for deterministic diffing, clipping,
overlap, caching, movement, replacement, removal, and resize behavior.

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

| Behavior | DEC and reference terminals | Hex1b default | Status and evidence |
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
The terminal retains that explicit geometry-only state using only the bounded
content prefix; it never treats the prefix as a complete replayable payload.

### Hardening limits and observability (#454)

The default internal `SixelCompatibilityPolicy` applies these deterministic
limits. Boundary and boundary-plus-one behavior is covered by parser,
rasterizer, placement-lifetime, and fuzz tests.

| Resource | Default limit | Limit behavior |
|---|---:|---|
| Retained DCS content | 1 MiB | Continue framing and geometry observation; retain a geometry-only image with `RetainedContentLimitExceeded` |
| DCS header parameters | 16 | Reject the introducer with a typed diagnostic |
| Numeric parameter/repeat value | 999,999,999 | Saturate geometry safely and report `NumericLimitExceeded`/`GeometrySaturated` |
| Retained raster commands | 65,536 | Stop command retention, continue geometry, and report `CommandRetentionLimitExceeded` |
| Palette mutations | 4,096 | Stop retaining ordered mutation history and report `MetadataLimitExceeded`; separately retain the final definition for each valid register so terminal-scoped palette state remains authoritative |
| Parser diagnostics | 64 | Bound diagnostic cardinality; diagnostics never retain raw hostile payloads |
| Raster pixels | 16 Mi pixels | Return geometry-only with `RasterPixelLimitExceeded` |
| Raster operations | 64 Mi pixel writes | Return geometry-only with `RasterOperationLimitExceeded` |
| Sparse raster tiles | 4,096 tiles of 64x64 | Return geometry-only with `RasterTileLimitExceeded` |
| Live placements per screen | 4,096 | Evict oldest placements first |
| History placement fragments | 4,096 | Evict oldest history fragments first |
| Distinct images per screen | 1,024 | Evict oldest reachable placements until the image set is bounded |
| Aggregate retained raster-capable area | 64 Mi logical pixels | Evict oldest placements; each image contributes at most the per-image raster limit |
| Aggregate retained image data per screen | 320 MiB | Count each distinct image once; evict oldest reachable placements until payload, parsed metadata, sparse tiles, and cached dense pixels fit |
| Embedded Sixel data per SVG/HTML export | 64 MiB by default | `TerminalSvgOptions.MaximumEmbeddedSixelBytes` replaces later raster placements with deterministic diagnostic placeholders |

`Hex1bTerminalGraphicsOptions.MaximumRetainedBytesPerScreen` configures the
aggregate retained-image budget independently for the main and alternate
screens. Accounting is deterministic protocol content accounting, not a CLR
heap-size estimate: retained UTF-16 strings, content identities, parsed commands,
palette and diagnostic metadata, captured palette state, sparse tile pixels
and keys, raster diagnostics, and a cached dense RGBA buffer are included.
Placements and history fragments remain governed by their count limits and do
not cause a shared image to be counted more than once.

The byte ceiling is shared with KGP rather than duplicated per protocol. Each
protocol applies its established oldest-first eviction order to its own
resources. A new Sixel or KGP image that still cannot fit in the capacity left
by the other protocol is rejected without destroying the other protocol's
placements. Lazy Sixel sparse-raster or dense-cache growth is observational:
if it cannot fit, the requested raster or pixels are returned uncached and no
live placement is evicted. Clearing, history pruning, alternate-screen exit,
RIS, reflow, and terminal disposal sweep unreachable images and their
accounting together.

Snapshots copy placements but intentionally share immutable `SixelData`
resources. A caller-retained snapshot can therefore extend an evicted image's
payload, raster, or pixel-cache lifetime outside the live terminal budget.

The parser exposes typed outcomes (`Complete`, `LimitDowngraded`, `Cancelled`,
`Malformed`, and `Rejected`) and bounded diagnostic codes for malformed,
cancelled, unterminated, numeric/geometry saturation, command/palette
retention, and DCS-content retention. Rasterization separately reports
geometry-only reasons for incomplete commands, extent overflow, pixel,
operation, tile, and color-register limits.

The `Hex1b` meter exposes constant-cardinality operational measurements:

- `hex1b.terminal.raw_passthrough.duration`
- `hex1b.terminal.sixel.processing.duration`
- `hex1b.terminal.sixel.outcomes` (`outcome`)
- `hex1b.terminal.sixel.resources` (`action`, optional `reason`)
- `hex1b.terminal.sixel.limit` (`limit`)

Resource actions are allocation, deduplication, rejection, release, placement
creation, damage, and eviction. Eviction reasons distinguish history pruning,
scrolling, line edits, viewport clipping, reflow, and the `history_limit`,
`placement_limit`, `image_limit`, `logical_pixel_limit`, and
`retained_byte_limit` policy limits.
Existing DCS byte, dispatch, cancellation, malformed-recovery, and
retention-limit instruments remain the framing-level source of truth.

## Grammar and raster model

### DCS parameters

The introducer is `DCS P1 ; P2 ; P3 q`.

| Parameter | DEC VT340 | Selected Hex1b behavior | Modern differences |
|---|---|---|---|
| `P1` aspect macro | Omitted, 0, 1, 5, or 6 select 2:1; 2 selects 5:1; 3 or 4 select 3:1; 7, 8, or 9 select 1:1 | Use the DEC table; unsupported values use the 2:1 default | The pinned xterm 411 and WezTerm 20240203 render paths use square logical pixels and do not apply this aspect metadata |
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

| Behavior | DEC and reference terminals | Hex1b default | Status and evidence |
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
performs a graphics carriage return. DEC does not document this, and the report
was not independently reproducible in the #457 environment. The finite corpus
therefore classifies it as unsupported reference behavior and does not add a
profile value.

## Color and palette

| Behavior | DEC and reference terminals | Hex1b default | Status and evidence |
|---|---|---|---|
| RGB | Three 0-100% components | Clamp valid components to the DEC domain and convert deterministically to 8-bit RGB with nearest rounding (`(percent * 255 + 50) / 100`) | Active rasterizer tests |
| HLS | Hue 0 is blue, 120 is red, and 240 is green; lightness and saturation are percentages | Use the DEC hue wheel, not the CSS hue wheel; hue wraps modulo 360 and lightness/saturation clamp to 0-100 | Active rasterizer tests |
| Default palette | DEC VT340 ships 16 hardware colors; the pinned WezTerm source initializes a distinct 16-color map | The DEC/xterm profiles use the VT340 defaults plus the documented 256-color extension; the WezTerm profile uses its pinned initial map and white for otherwise undefined entries | Centralized in `SixelDefaultPalette` and selected by `SixelCompatibilityPolicy`; the corpus pins register 6 where the reference maps differ |
| Register count | DEC VT340 exposes 16; modern terminals commonly expose 256 | 256 terminal-scoped registers; selection or definition outside the policy is rejected explicitly with a diagnostic and never silently wrapped | Centralized in `SixelCompatibilityPolicy` |
| Register persistence | DEC has a shared palette; xterm, WezTerm, foot, Windows Terminal, and xterm.js persist by default | Share palette state between sequences on the same terminal; definitions apply in command order even when rasterization degrades to geometry only | Active rasterizer and terminal tests |
| Private registers | xterm mode 1070 and some terminal options provide per-image palettes | Shared by default; any private mode must be an explicit compatibility option expressed through `SixelCompatibilityPolicy.PaletteScope` | Outside the finite primary matrix; no unsupported reset claim is made |
| RIS | WezTerm resets its shared color map; other reviewed behavior is incomplete | Reset the Sixel palette and remove active placements while preserving prior snapshots | Active palette and lifecycle tests |
| Alternate screen | Reviewed terminals keep one shared color map across screen buffers | Preserve palette registers across alternate-screen transitions | Active terminal tests |
| DECSTR | Reference behavior is not sufficiently established | Reset Sixel modes; preserve palette, placements, cursor, and snapshots | Deterministic Hex1b contract; external behavior remains implementation-defined |

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
branch. The #457 corpus exercises both the DEC/Hex1b captured-background
profile and the source-derived xterm/WezTerm register-zero profile.

Because captured background and persistent palette state change how an identical
payload rasterizes, while protocol cell metrics and the declared cell span
change how that raster is clipped and damaged, tracked Sixel deduplication keys
on all of those immutable resource inputs rather than payload content alone.

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
rather than in terminal detection. The #457 corpus pins xterm 411 and WezTerm
20240203 to the inverted profile.

### Cursor, margins, and origin

In scrolling mode, placement starts at the active text position. Origin mode
and active margins determine that position and the scrolling region. Placement
may be clipped by a margin or viewport without changing its source raster.
After the sequence, the default cursor is at its original column below the
occupied cell rows; mode 8452 may select the right-side outcome.

In non-scrolling mode, placement starts at the graphics-page origin and restores
the text cursor exactly. Windows Terminal explicitly uses the full page instead
of text margins in this mode. Exact cross-reference margin state is not exposed
authoritatively by the primary matrix; Hex1b's margin and clipping behavior is
covered by the dedicated cursor-semantics suite.

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

| Operation | Selected Hex1b behavior | Notes |
|---|---|---|
| Sixel over Sixel | Both placements are retained independently; presentation composites in placement sequence order. Painted pixels from a later placement cover earlier pixels; unpainted/transparent pixels leave earlier placements visible. | Authoritative machine-readable state is unavailable from the primary references; Hex1b's deterministic model is covered by placement and terminal-semantics tests. #458 explicitly rejects automatic translation into another graphics protocol. |
| Text over Sixel | Any text-cell write destructively damages the Sixel pixels projected into the overwritten cell. A space, styled background write, combining-cluster update, wide-character leading cell, or wide-character continuation cleanup is still a text write for graphics damage. Destroyed Sixel pixels do not reappear if the text is later erased. | Damage is modeled at bounded cell granularity rather than sub-cell glyph-shape granularity. |
| ED/EL/ECH/DECERA/DECSERA | Erase graphics in the same clipped cell region that text erasure affects. Selective erase preserves graphics only where the underlying terminal cell is protected. Full ED/RIS remove active placements; partial erases damage only intersecting placement cells. | Implemented; scrolling/reflow projection across the scrollback boundary is implemented by [#452](https://github.com/mitchdenny/hex1b/issues/452) (see below). |
| Insert/delete characters, columns, and lines | Character/column/line edits damage every overwritten destination or blank-fill cell in their clipped edit region, while Sixel placements themselves do not shift with ordinary text edits unless the existing scroll integration explicitly moves/drops them. | History/reflow projection is implemented by [#452](https://github.com/mitchdenny/hex1b/issues/452). |
| Scroll-region operations | Move, clip, split into history, or erase placements using the same region semantics as text rows, including partial vertical/horizontal margins under DECSTBM/DECLRMM | Full-fidelity scrolling/reflow projection across the scrollback boundary is implemented by [#452](https://github.com/mitchdenny/hex1b/issues/452); see "Independent Sixel scrolling, history, and reflow (#452)" below |
| RIS | Clear main and alternate placements, reset Sixel modes, reset the palette, clear saved screen state, and leave previously captured snapshots valid. | Implemented for lifecycle; native presentations own their terminal's response to the original bytes, while managed consumers observe impacts and snapshots. |
| DECSTR | Reset modes, including DECSDM and mode 8452; preserve palette, placements, cursor position, and snapshots. | External behavior is implementation-defined in the finite matrix; Hex1b's compatibility choice is centralized and deterministic. |

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
- Each `SixelScreenGraphicsState` owns a `SixelImageStore` (the image
  resources, deduplicated by resource identity — payload, captured
  background/palette state, protocol cell metrics, and declared cell span), a
  live `Placements` list, and — main screen only — a `HistoryPlacements`
  partition keyed by stable scrollback row identity.
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
  resource identity hash exposed as `ContentHash`, and parser diagnostics.
- **Lifetime is reachability-based, not manually reference-counted.** Mirroring
  `KgpImageStore`, every placement-removing mutation recomputes the set of
  resource identity hashes reachable from `Placements ∪ HistoryPlacements` and
  sweeps any image no longer in that set. A `SixelPlacement`'s image is never
  released just because one text cell it covered was overwritten — only when no
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
resource ownership by immutable identity, placement/source-crop geometry
(anchor + occupied span + painted crop), reachability-based lifetime
accounting, screen/history partitioning, and simple scroll/clip geometry helpers. **Kept
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
- Automatic translation into another graphics protocol is not part of
  `Hex1bTerminal`; [#458](https://github.com/mitchdenny/hex1b/issues/458)
  finalizes native forwarding, managed impacts/snapshots, and internal HMP1
  replay as the supported boundaries instead.
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
  `IReadOnlyDictionary<byte[], SixelData>` keyed by immutable resource identity
  hash) are now public, mirroring the shape of the existing
  `KgpPlacements`/KGP image surfaces. `SixelPlacement` and the `SixelData`
  properties it exposes
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
  directly; neither reparses, redecodes, or rehashes the payload. Embedded
  Sixel BMP data URIs are capped at 64 MiB per export by default through
  `TerminalSvgOptions.MaximumEmbeddedSixelBytes`; a placement that would
  exceed the remaining budget is represented by an explicit
  `ExportLimitExceeded` placeholder before allocating its RGBA/BMP buffers.
  HTML forwards the same option to its embedded SVG. Repeated
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
    a geometry-only placement has no decoded pixels, so replay restores DCS
    framing around its retained content. A retention-limited placement is
    skipped because the bounded prefix is not a complete replay payload.
    Replay is bounded to 4,096 placements, 2^20 damaged cells, 64 MiB per
    generated sequence, 64 MiB total output, and 1 MiB HMP1 frames. Exact
    raster re-encoding is byte-budgeted before output construction and checks
    cancellation while discovering palettes and constructing color runs.
    Only the viewport is
    replayed here, matching
    `Hex1bTerminal.CreateSnapshot()`'s existing zero-scrollback state-sync
    scope.
  - `Hmp1SixelRecording` (internal) is a new, versioned binary format for
    the explicit record/serialize/replay/compare scenarios the plain-bytes
    wire replay above cannot express (truncation, unsupported version,
    missing references, invalid geometry, resource limits). It serializes
    an `IReadOnlyList<SixelPlacement>` — the same type the live snapshot and
    live wire replay use — into a `SXRC`-tagged, versioned
    (`Hmp1SixelRecording.CurrentVersion = 2`) stream: an image table
    deduplicated by `SixelData.ContentHash`. Placements share an entry only
    when payload, raster state, protocol metrics, and cell span all match, so
    incompatible clipping/damage contexts are not conflated. Version 2 stores
    each image's exact raw metric width/height bits plus
    `SixelCellMetricsSource` and `SixelCellMetricsReliability`; version 1
    remains readable and retains its historical target-metric replay behavior
    because it did not carry per-image metric context. The image table is
    followed by placements referencing it by index, each carrying its
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
    `MaxPayloadLength`/aggregate payload = 64 MiB,
    `MaxRecordingLength` = 72 MiB, and aggregate
    `MaxDamagedCellCount` = 2^20). Exact raster re-encoding is constrained by
    the remaining per-image and aggregate payload budget before allocating
    its output. `Hmp1SixelRecordingSnapshot.ReplayInto` performs a checked,
    cancellable preflight and rejects expanded output above the same 64 MiB
    aggregate limit before changing the target. It then applies each version
    2 image's recorded metrics, feeds cursor-position plus Sixel DCS through
    the ordinary terminal parser, restores the recorded painted crop and
    damage mask, and finally restores the target's prior metric override.
    Retention-limited images are rejected before
    serialization because their complete payload is unavailable; deserialization
    rejects an oversized input before copying it and rejects trailing bytes.
    Recorded
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
  a recording into a fresh terminal and compares resulting pixels, captured
  metrics, spans, resource identity, painted crops, and damage boundaries,
  and exercises every `Hmp1SixelRecordingException`
  failure mode named above. `tests/Hex1b.Tests/Hmp1/Hmp1SixelStateReplayTests.cs`
  covers the existing live wire replay path, including the damage-patch
  restoration. All pre-existing KGP snapshot/export/replay and terminal
  automation tests continue to pass unmodified, confirming this stage adds
  a parallel Sixel path without touching KGP's.
- **Explicitly out of scope for this stage** (see the exclusions in
  [#456](https://github.com/mitchdenny/hex1b/issues/456)): capability probing
  ([#455](https://github.com/mitchdenny/hex1b/issues/455)), presentation
  boundary cleanup ([#458](https://github.com/mitchdenny/hex1b/issues/458)),
  and any
  `SixelWidget`/`Surface`-side preview generation.

## Capability discovery and protocol cell metrics (#455)

[#455](https://github.com/mitchdenny/hex1b/issues/455) answers a question no
earlier stage needed to ask: given an effective upstream presentation, can it
actually turn Sixel bytes into pixels a human can see, and if so, what protocol
cell size should occupancy math use? This stage adds no raster decoding or
`SixelWidget`/`Surface`-side fallback logic and does not translate Sixel into
KGP, iTerm2, or any other graphics protocol. It only discovers and reports,
safely and without ever consuming, reordering, or duplicating a byte of user
input or a terminal's own query response.

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
| `Headless` | There is no real display; `Hex1bTerminal`'s own graphics-state model (from [#451](https://github.com/mitchdenny/hex1b/issues/451)/[#452](https://github.com/mitchdenny/hex1b/issues/452)/[#456](https://github.com/mitchdenny/hex1b/issues/456)) is the sole, authoritative source of truth. |

`TerminalCapabilities.SixelSupport` carries this value; the older
`TerminalCapabilities.SupportsSixel` boolean remains for back-compatibility and
must be kept consistent with it (`true` only when `SixelSupport` is `Native`,
or `Headless` — never for `Unknown` or `None`) by any adapter
that participates in discovery. Workload-facing feature reporting (the DA1
reply below) advertises Sixel to a hosted workload only when the effective
path is `Native` or an authoritative `Headless` model — parser
capability alone is never sufficient, and both `Unknown` and `None` always mean
"do not advertise." Advertisement logic is written as an allowlist
(`is Native or Headless`) rather than a `!= None` denylist,
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
  parameter 4) when `Capabilities.SixelSupport` is `Native` or `Headless`
  (or the back-compatible `Capabilities.SupportsSixel` flag is set), or `\x1b[?62c`
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

## Presentation boundaries (#458)

`Hex1bTerminal` is a terminal emulator, not a cross-protocol graphics gateway.
Sixel presentation uses the existing terminal contracts:

- **Native presentations receive workload bytes immediately and byte-for-byte.**
  When no presentation filter or impact-aware adapter is attached, every
  workload read is written to the presentation before DCS framing, parsing,
  rasterization, or snapshot work. Arbitrary chunking is preserved, and a
  rejected, malformed, oversized, or geometry-only Sixel model outcome cannot
  delay, rewrite, suppress, or replace the native bytes.
- **Impact-aware managed presentations receive ordered token effects.**
  `ICellImpactAwarePresentationAdapter` receives each batch as ordered
  `AppliedToken` values. `AppliedToken.GraphicsImpacts` reports Sixel additions,
  damage, removals, and other graphics invalidation independently from cell
  impacts, so a managed consumer can update incrementally without a second
  Sixel-specific event hierarchy.
- **Snapshots provide full authoritative graphics state.**
  `Hex1bTerminal.CreateSnapshot()` exposes `SixelPlacements` and the
  identity-addressed `SixelImages` table, including decoded pixels or an explicit
  geometry-only outcome, captured protocol metrics, placement geometry, crop,
  ordering, and damage state.
  Consumers that need complete state use snapshots rather than reconstructing it
  from incremental impacts alone.
- **HMP1 replay is private same-protocol restoration.**
  A newly connected HMP1 peer receives StateSync followed by internally generated
  cursor-position and Sixel DCS output that recreates the current viewport and
  repairs damaged cells. This is Sixel-to-Sixel state restoration through the
  ordinary terminal parser, not a browser-native delta protocol. Replay
  serialization, frame ordering, limits, wire/version types, and mechanics remain
  internal to `src/Hex1b/Hmp1/`.

Automatic Sixel-to-KGP or Sixel-to-iTerm2 conversion is explicitly not part of
Hex1bTerminal. The public API therefore has no translated support state,
Sixel-specific presentation sink/event hierarchy, routing state machine,
unsupported-presentation placeholder policy, or Sixel sanitization/interception
policy.

### Explicitly out of scope for capability discovery

Per the exclusions in [#455](https://github.com/mitchdenny/hex1b/issues/455),
capability discovery makes no changes to raster decoding or
`SixelWidget`/`Surface`-side fallback logic. Broad conformance hardening beyond
the safe, bounded probing described above is likewise deferred; that stage
answers "what can the presentation do and how big is a cell," not "make every
terminal work perfectly."

## Screens, scrollback, resize, and reflow

| Area | Selected Hex1b contract | Status and evidence |
|---|---|---|
| Main/alternate screen | Each screen owns independent placements; leaving the alternate screen restores the unchanged main-screen graphics | Implemented by [#451](https://github.com/mitchdenny/hex1b/issues/451) |
| Scrollback | Scrolling placements remain anchored to logical row lineage and can span visible and history rows; a placement is split across the visible/history boundary via a non-destructive crop of its *current* painted window | Implemented by [#452](https://github.com/mitchdenny/hex1b/issues/452); foot provides verified prior art |
| History eviction | Remove only the placement portions no longer owned by retained row lineage | Implemented by [#452](https://github.com/mitchdenny/hex1b/issues/452), with KGP's partial-crop/transfer-to-successor-row fidelity |
| Resize | Clip to the viewport without destroying source pixels; reveal them again when space returns | Implemented by [#451](https://github.com/mitchdenny/hex1b/issues/451)/[#452](https://github.com/mitchdenny/hex1b/issues/452): a placement is dropped only once it is wholly outside the new bounds; a partially-visible placement keeps its full underlying raster and geometry, so it reappears in full when space returns |
| Reflow | Re-anchor through the same row-lineage plan as text and KGP placements; atomic per-anchor movement, with projection-only splitting when an anchor lands in a history/discarded window | Implemented by [#452](https://github.com/mitchdenny/hex1b/issues/452) |
| Cell-metric change | Recompute occupied cells from stable pixel geometry using deterministic outward rounding; a protocol metric-query response never resizes the terminal or its placements on its own | Implemented by [#452](https://github.com/mitchdenny/hex1b/issues/452); no primary reference exposes equivalent authoritative placement state |

No reviewed reference provided a complete answer for resize/reflow or
main/alternate-screen ownership. These decisions intentionally align Sixel with
Hex1b's protocol-neutral terminal model and existing KGP reflow machinery while
remaining covered by deterministic terminal-model tests rather than screenshot
comparison.

## Differential conformance corpus (#457)

The differential corpus is intentionally finite. Its primary matrix is DEC
VT340, xterm patch 411, and WezTerm
`20240203-110809-5046fc22`. The checked-in manifest is
`tests/Hex1b.Tests/TestData/Sixel/Conformance/terminal-reference-matrix.json`;
the executable normalizer is
`tests/Hex1b.Tests/Sixel/SixelReferenceConformanceTests.cs`.

| Behavior | Hex1b / DEC VT340 profile | xterm 411 profile | WezTerm 20240203 profile | Classification |
|---|---|---|---|---|
| Initial paint color | Register 0 | Register 3 (`#33CC33` in the VT340 palette) | Independent pure green (`#00FF00`) until the first explicit selection | DEC behavior is implementation-defined; xterm/WezTerm behavior is source-derived |
| Initial register palette | VT340 values; register 6 is `#CCCC33` | VT340 values; register 6 is `#CCCC33` | Pinned WezTerm map; register 6 is `#CCCCCC` | Documented reference difference |
| Pixel aspect | Apply the DEC `P1`/DECGRA ratio | Render square logical pixels | Render square logical pixels | Documented reference difference |
| Opaque `P2=0/2` fill | Captured terminal background | Sixel register 0 | Sixel register 0 | Documented reference difference; DEC's “current background” wording is ambiguous |
| Color definition | Define and select the register | Define and select the register | Define without changing the selected register | Documented reference difference |
| `!0` repeat | Treat zero as the default single repeat | Treat zero as a single repeat | Perform zero writes and reject the resulting zero-area image | DEC zero-count wording is implementation-defined; xterm/WezTerm behavior is source-derived |
| RGB 50% conversion | Nearest, producing 128 | Not asserted by this corpus | Truncate, producing 127 | Implementation-defined quantization and documented WezTerm difference |
| `CSI ? 80 h` | Enable Sixel scrolling | Disable Sixel scrolling | Disable Sixel scrolling | Documented reference difference |
| Palette lifetime | Shared terminal registers | Shared by default | Shared by default | Reference match |

These differences are centralized in the internal
`SixelCompatibilityPolicy`; parser, raster, placement, and terminal code do not
branch on terminal names. The default remains the DEC VT340 profile.

The corpus classifies every asserted difference as one of:

- **Hex1b defect** — evidence contradicts the selected Hex1b contract and the
  implementation must be fixed.
- **Documented reference difference** — a pinned reference intentionally
  differs and is represented by a profile.
- **Implementation-defined/ambiguous** — the DEC material does not define the
  exact modern representation, such as 8-bit RGB midpoint quantization.
- **Unsupported reference behavior** — the available evidence is insufficient
  for an executable assertion; the corpus records no invented result.

The corpus review exposed and fixed default-path raster-preparation identity
defects: palette-dependent colors were treated as an unordered set, and
register 0 was omitted when it supplied the opaque background. The identity now
records the resolved unpainted color and the ordered effective paint colors.
Profile-specific fixes also model xterm/WezTerm initial paint color, select the
pinned WezTerm initial register map for both terminal construction and RIS,
and prevent WezTerm's zero-area repeat result from becoming a placement.

### Scope decisions

The previous open questions now have finite dispositions:

1. DECSDM polarity, opaque background source, aspect handling, color-definition
   selection, zero repeat, and RGB quantization are explicit profile values.
2. DECGRA carriage return is unsupported reference behavior in this matrix:
   the Windows Terminal report was not reproducible in this environment and no
   result is fabricated.
3. Sixel-over-Sixel ordering, partial-cell damage, DECSTR placement behavior,
   private-mode save/restore, alternate-screen ownership, history, resize, and
   reflow remain Hex1b terminal-model contracts backed by dedicated executable
   tests. The selected primary references do not expose authoritative,
   machine-readable state for those operations.
4. Registers 16-255 and private-register modes remain outside the executable
   equality matrix. The WezTerm profile uses the pinned source's white fallback
   for undefined entries; the DEC/xterm profiles retain the documented Hex1b
   extension. No cross-terminal equality claim is made for those registers.

This closes the terminal-side matrix rather than creating a permanently
expanding list of emulator targets. A new reference belongs here only when its
version, provenance, capture process, and normalized authoritative outcome can
all be checked in.

### Fixture provenance and reproduction

The corpus contains minimal independently authored Sixel payloads. It does not
copy terminal source code or use `SixelEncoder`. Reference facts are derived
from:

- DEC VT300 Series Programmer Reference Manual Volume 2,
  `EK-VT3XX-GP-001`, Chapter 14.
- xterm patch 411 source revision
  `9489b2056ee51fa9dd6a7087483b9b8f85d6a0c4`;
  `graphics_sixel.c` SHA-256
  `3f6234e71ded3d816d6b2e7792b3a4860ecaf8c3c104d2284f0a44f31c726c96`
  and `graphics.c` SHA-256
  `447846c4a6120d24962f97b653538794df888a88b25b4c74a1c5ac5a5d0019c0`.
- WezTerm build `20240203-110809-5046fc22`, source revision
  `5046fc225992db6ba2ef8812743fadfdfe4b184a`;
  terminal Sixel renderer SHA-256
  `4a8007dd75244005874791b26cd7d0e4c62872bef4df4e978b9f921bf53ee9f9`
  termwiz Sixel parser SHA-256
  `ee4d38f6bafd98d3810074ba3d2f2675d62deac923da6a4b2559a4035add3de0`,
  and terminal-state palette source SHA-256
  `5bbdf36cae5d86c29fa4decda94fde6d370e2295c6451245c6218fec665082d9`.

To reproduce the source provenance, check out those exact revisions and hash
the files rather than inspecting a moving default branch:

```bash
git clone https://github.com/ThomasDickey/xterm-snapshots.git /tmp/xterm-sixel
git -C /tmp/xterm-sixel checkout 9489b2056ee51fa9dd6a7087483b9b8f85d6a0c4
shasum -a 256 /tmp/xterm-sixel/graphics_sixel.c
shasum -a 256 /tmp/xterm-sixel/graphics.c

git clone https://github.com/wezterm/wezterm.git /tmp/wezterm-sixel
git -C /tmp/wezterm-sixel checkout 5046fc225992db6ba2ef8812743fadfdfe4b184a
shasum -a 256 /tmp/wezterm-sixel/term/src/terminalstate/sixel.rs
shasum -a 256 /tmp/wezterm-sixel/termwiz/src/escape/parser/sixel.rs
shasum -a 256 /tmp/wezterm-sixel/term/src/terminalstate/mod.rs
wezterm --version
```

The executable normalizer wraps each ASCII fixture in either 7-bit
`ESC P ... ESC \` or 8-bit C1 `DCS`/`ST`, feeds the exact bytes through
`Hex1bTerminal`, and compares decoded RGBA pixels, logical/rendered extents,
cell placement, cursor state, parser/raster outcomes and diagnostics, and
native presentation bytes. The framing case is repeated at every byte boundary
and with one-byte chunks. The manifest also maps the complete terminal-side
contract to the existing damage, ordering, lifecycle, history, resize, reflow,
snapshot, recording, internal HMP1 replay, hardening, and fuzz suites.

```bash
dotnet test tests/Hex1b.Tests/Hex1b.Tests.csproj -v q \
  --filter "FullyQualifiedName~SixelReferenceConformanceTests"
```

For a supplemental visual check in a Sixel-capable terminal, print one payload
without changing it:

```bash
printf '\033P'; cat tests/Hex1b.Tests/TestData/Sixel/reference-default-aspect.sixel; printf '\033\\'
```

Screenshots are not authoritative corpus data. xterm was not installed in the
capture environment. The installed WezTerm build can display fixtures but does
not expose a CLI for extracting its decoded raster, placement, cursor, damage,
or diagnostics as machine-readable state. Consequently xterm and WezTerm
assertions are explicitly source-derived from the pinned revisions above.
Windows Terminal, foot, mintty, and other implementations are excluded from
the primary matrix because no equally reproducible local capture or
independently sourced checked-in fixture was available.

## Evidence and running the contract

The test fixtures are small ASCII payloads embedded from
`tests/Hex1b.Tests/TestData/Sixel/`. Expected data is independently authored and
does not use `SixelEncoder`.
`tests/Hex1b.Tests/Sixel/SixelReferenceConformanceTests.cs` loads the checked-in
reference matrix and verifies the profile-specific pixels, logical/rendered
geometry, placement, cursor state, and exact native bytes. It also verifies
that the manifest covers the complete terminal-side contract and that every
ignored Sixel test is explicitly accounted for. The only ignored Sixel tests
exercise `SixelWidget`/Surface emission and remain outside terminal-side #457;
there are no unexplained terminal-side ignores.
`tests/Hex1b.Tests/Sixel/SixelPlacementLifetimeTests.cs`
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
`tests/Hex1b.Tests/Sixel/SixelHardeningFuzzTests.cs` runs the checked-in
minimized regression fixtures (`regression-numeric-overflow`,
`regression-command-replacement`, `regression-extent-overflow`, and
`regression-palette-raster-repeat`) plus generated malformed-control,
numeric-overflow, huge-repeat/extent, palette/raster, cancellation, truncation,
chunking, and lifecycle operations with stable seeds `445`, `454`, `473`, and
`0x51E1`. Every generated stream is compared across single-chunk and arbitrary
chunk boundaries; lifecycle runs mix placement, damage, erasure, scrolling,
alternate-screen switching, resize, RIS, snapshot, recording, and replay while
asserting the configured state bounds.
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

```bash
dotnet test tests/Hex1b.Tests/Hex1b.Tests.csproj \
  --filter "FullyQualifiedName~Hex1b.Tests.Sixel."
dotnet test tests/Hex1b.Tests/Hex1b.Tests.csproj \
  --filter "FullyQualifiedName~Hex1b.Tests.Hmp1.Hmp1Sixel"
dotnet test tests/Hex1b.Tests/Hex1b.Tests.csproj \
  --filter "FullyQualifiedName~SixelCapabilityDiscoveryTests|FullyQualifiedName~Hex1bTerminalQueryOwnershipTests"
dotnet test tests/Hex1b.Tests/Hex1b.Tests.csproj \
  --filter "FullyQualifiedName~SixelHardeningFuzzTests"
dotnet run -c Release --project benchmarks/Hex1b.Benchmarks -- \
  sixel --filter "*SixelHardeningBenchmarks*"
```

`SixelHardeningBenchmarks` measures raw forwarding alone, raw forwarding with
incremental Sixel observation, parser geometry, bounded worst-case raster
rejection, placement/damage/scroll/resize lifecycle work, snapshot plus
recording round-trip, and internal HMP1 replay. BenchmarkDotNet JSON output is
the reproducible baseline artifact. These timings are intentionally not
CI-gated: short terminal/parser operations are sensitive to runner CPU,
virtualization, runtime tiering, and architecture, so a fixed cross-runner
threshold would be noisy. Correctness and allocation bounds are CI-tested;
performance budgets are reviewed against same-machine Release baselines.

The terminal-first demo sends independently authored raw Sixel bytes through
`Hex1bTerminal`. It does not use `SixelWidget` or `SixelEncoder`.
Invisible malformed, limit-degraded, and geometry-only cases live in the
headless executable suites and corpus rather than consuming a visual demo
screen.
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
`Hmp1SixelRecording.Serialize`/`Deserialize`/`ReplayInto` to
record a snapshot, replay it into a brand-new terminal with no live upstream
connection, and compare the resulting pixels, painted crop, and damage mask —
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
recorded metrics unchanged while a subsequent placement of the same payload
picks up the new value and retains a distinct metric-specific image resource;
and, run
against `Hex1bTerminal` directly, native-presentation silence versus
default-headless (`SixelSupport.Unknown`, "no parameter 4") versus an
explicitly declared confirmed-unsupported headless (`SixelSupport.None`,
also "no parameter 4," for a different, explicit reason) versus
authoritative-headless ("parameter 4 present") DA1 replies — demonstrating
that `Unknown` and `None` are distinct, separately observable capability
states that nonetheless agree on the workload-facing answer, matching
`Hex1bTerminalQueryOwnershipTests.cs` exactly.

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
- [xterm 411 `graphics_sixel.c`](https://github.com/ThomasDickey/xterm-snapshots/blob/9489b2056ee51fa9dd6a7087483b9b8f85d6a0c4/graphics_sixel.c)
- [Windows Terminal `SixelParser`](https://github.com/microsoft/terminal/tree/main/src/terminal/adapter)
- [WezTerm pinned Sixel parser](https://github.com/wezterm/wezterm/blob/5046fc225992db6ba2ef8812743fadfdfe4b184a/termwiz/src/escape/parser/sixel.rs)
- [WezTerm pinned Sixel terminal state](https://github.com/wezterm/wezterm/blob/5046fc225992db6ba2ef8812743fadfdfe4b184a/term/src/terminalstate/sixel.rs)
- [WezTerm pinned default Sixel palette](https://github.com/wezterm/wezterm/blob/5046fc225992db6ba2ef8812743fadfdfe4b184a/term/src/terminalstate/mod.rs#L405-L430)
- [foot `sixel.c`](https://codeberg.org/dnkl/foot/src/branch/master/sixel.c)
- [mintty `sixel.c`](https://github.com/mintty/mintty/blob/master/src/sixel.c)
- [xterm.js image add-on](https://github.com/xtermjs/xterm.js/tree/master/addons/addon-image/src)
