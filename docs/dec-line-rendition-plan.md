# DEC line rendition: production hardening plan

Status: the six hardening stages are implemented and validated for the policy and
bounded corpus below. Ready for code review, not a claim of complete terminal
conformance or cross-platform certification. Double-height support remains a
terminal text feature, not a widget/layout feature.

## State model and compatibility rules

- Keep rectangular cell storage. A physical row owns its rendition; logical
  addressable width is derived from that rendition and terminal width.
- Top and bottom halves remain independent rows. Do not pair them, duplicate
  text automatically, or deduplicate them during copying.
- Distinguish physical cell-storage columns, logical row columns, and display
  coordinates. Unicode width remains expressed in logical cells.
- Row moves, copies, clears and buffer switches must preserve/reset row metadata
  together with cells, rather than requiring unrelated call-site updates.
- DEC commands define the normative behavior. Windows Terminal is a compatibility
  reference for modern extensions, not a replacement specification.
- Enlarged rows truncate rather than paragraph-reflow on resize. Adjacent normal
  rows must not absorb them. This is a compatibility policy, not a DEC mandate.
- The spike uses a one-logical-column minimum for a one-column viewport. Retain
  that safety policy unless an explicit alternative is chosen and tested.
- Graphics interactions, external reflow providers and HMP1 history must have
  explicit policies/contracts before production readiness. Missing metadata must
  not silently become normal text on a path advertised as preserving state.

## Execution stages

| Stage | Work | Acceptance gate | Status |
|---|---|---|---|
| 1. Behavioral corpus | Original data-driven tests for mode transitions, printing, editing, cursor movement, Unicode boundaries, row operations, reset/protection and resize. Record provenance and distinguish requirements from policy. | Tests exercise mixed row modes and hidden storage, and reproduce defects before fixing them. | Complete for the documented corpus |
| 2. Row ownership | Introduce shared internal row operations owning cells and metadata; cover scroll regions, IL/DL, clears, resize and alternate screens. Preserve resource ownership and text/history identity. | No row-moving path needs a separate rendition-array adjustment; normal rows retain existing behavior. | Complete |
| 3. Geometry | Audit physical versus logical bounds throughout printing, REP, split graphemes, cursor movement, tabs, editing and margins. Replace the blanket post-token clamp with correct operation-local bounds and invariant assertions. | All supported operations leave valid cursor/cell state; no inaccessible character or orphaned continuation is created. | Complete for the supported operation corpus |
| 4. State transfer | Make snapshot regions, built-in/external reflow, scrollback, capture, ANSI replay, HWT1 and HMP1 preserve row semantics. Design version/capability handling where needed. | Round trips, reconnects, history eviction and resize during alternate-screen use preserve declared semantics. | Complete |
| 5. Rendering and input | Extend the tape and automated rendering tests for glyph halves, combining marks, color emoji, decorations, cursor, selection and links. Define graphics policy. | Both backends, multiple fonts, fractional scaling and history views agree on display/hit-test geometry. | Complete for the tested browser matrix |
| 6. Invariants and performance | Deterministic operation sequences and chunk-boundary tests; measure ordinary text throughput, allocation, reflow and frame generation before/after changes. | State invariants hold after each operation; no unexplained common-case regression or renderer resource growth. | Complete for the tested capability profile and benchmarks |

Stages may overlap when a regression reveals a small prerequisite. Do not mark a
stage complete merely because its first tests pass.

## First execution increment

- [x] Expand the behavioral corpus beyond the spike's example-based tests.
- [x] Run the new tests against the spike and record failing scenarios.
- [x] Fix the exposed core failures with shared operation-level logic where
      appropriate, preserving existing styles, hyperlinks and tracking behavior.
- [x] Run focused tests and related conformance/reflow/capture/HWT tests.
- [x] Record the completed increment and outstanding work below.

## Test provenance

Write original Hex1b scenarios and assertions; do not mechanically port the C++
test harness or assume all Windows Terminal behavior is normative.

DEC reference material:

- [DECDHL](https://vt100.net/docs/vt510-rm/DECDHL.html),
  [DECDWL](https://vt100.net/docs/vt510-rm/DECDWL.html),
  [DECSWL](https://vt100.net/docs/vt510-rm/DECSWL.html).
- [DECAWM](https://vt100.net/docs/vt510-rm/DECAWM.html),
  [ED](https://vt100.net/docs/vt510-rm/ED.html),
  [DECLRMM](https://vt100.net/docs/vt510-rm/DECLRMM.html).

Windows Terminal reference pinned to
`bb0541e7a972a1f9e39318c1f6b4e70347a86696`:

- [Rendering exercises](https://github.com/microsoft/terminal/blob/bb0541e7a972a1f9e39318c1f6b4e70347a86696/src/tools/RenderingTests/main.cpp#L237-L251):
  enlarged text with emoji, combining marks, deliberately different halves,
  hyperlinks and decorations. These are manual visual exercises.
- [Selection tests](https://github.com/microsoft/terminal/blob/bb0541e7a972a1f9e39318c1f6b4e70347a86696/src/cascadia/UnitTests_TerminalCore/SelectionTest.cpp#L295-L350):
  wide-glyph endpoints and rectangular selection; extend these scenario ideas
  across row renditions while preserving Hex1b's own selection contract.
- [Reflow tests](https://github.com/microsoft/terminal/blob/bb0541e7a972a1f9e39318c1f6b4e70347a86696/src/buffer/out/ut_textbuffer/ReflowTests.cpp#L30-L140):
  sequences of expected buffers, cursor positions and wrap flags. Add rendition
  and history assertions to this data-driven testing pattern.

The inspected upstream files do not supply a comprehensive automated
double-height suite. Hex1b-specific transport and lifecycle tests remain
necessary even where a Windows Terminal scenario is available.

## Capture baseline issue

The spike investigation reproduced capture tab-stop restoration failure with
ordinary single-width text as well as enlarged text. Treat that as a separate
baseline issue, not evidence of a rendition-only defect or a reason to weaken
rendition assertions. The fourth increment implements HTS and verifies the
previously missing tab-stop behavior and capture replay.

## Execution record

### First increment

Added `tests/Hex1b.Tests/LineRenditionConformanceTests.cs`. The first 31
parameterized cases produced 19 passes and 12 failures on the spike: REP ignored
disabled autowrap, repeated wide glyphs crossed the logical right edge, and
printing past the bottom of a partial scroll region overwrote the row below it.
Each failure reproduced for normal, double-width, top-half and bottom-half rows.

Normal printing and REP now share `Hex1bTerminal.Printing.cs`, including deferred
wrap, destination-row capacity, wide-glyph preflight, insert mode and overlapping
wide-cell cleanup. Wrapping scrolls at the active region's bottom and does not
scroll that region when printing below it. REP retains the source glyph's styling
and its existing no-hyperlink policy, rather than adopting the current pen.
An unrepresentable wide glyph is dropped using the existing narrow-terminal
policy; hidden storage never receives its continuation.

The corpus now contains 62 parameterized cases, including mixed-rendition
destinations, disabled-wrap wide glyphs, one-column destinations, scroll-region
boundaries, pen changes, insertion and overlapping wide glyphs. Cases with
multiple destinations or print/REP variants exercise additional combinations
inside each case.

The extended run caught a refactoring regression in cursor restoration when a
scrollback callback disposes the terminal. Cursor advancement is now committed
only after scrolling succeeds; the existing disposal cases pass unchanged.

Validation: 3,874 affected tests passed, including the rendition corpus, existing
terminal conformance, capture, reflow, HWT1, ANSI serialization, hyperlinks,
text anchors, KGP disposal, Sixel, graphemes and terminal/impact tests. Reproduce:

```sh
dotnet test tests/Hex1b.Tests/Hex1b.Tests.csproj -v q --filter \
  'FullyQualifiedName~LineRendition|FullyQualifiedName~TerminalCaptureTests|FullyQualifiedName~Conformance|FullyQualifiedName~Reflow|FullyQualifiedName~Hwt1|FullyQualifiedName~AnsiToken|FullyQualifiedName~TerminalRegionAnsi|FullyQualifiedName~TextAnchor|FullyQualifiedName~Hyperlink|FullyQualifiedName~KgpTerminalTests|FullyQualifiedName~Hex1bTerminalTests|FullyQualifiedName~CellImpact|FullyQualifiedName~Sixel|FullyQualifiedName~Grapheme'
```

### Second increment: row ownership

`TerminalScreenBuffer` now owns rectangular cell storage and rendition metadata
for both the active screen and the saved main screen. Buffer cloning, crop
resize, reflow import and alternate-screen restoration keep those together.
The terminal no longer maintains `_lineRenditions`,
`_savedMainLineRenditions`, or a separate `ShiftLineRenditions` operation.

`Hex1bTerminal.Rows.cs` supplies shared row copy, shift and clear operations.
SU/SD and IL/DL use the same movement path. Complete display erasures, full
reset, DECALN and alternate-screen clearing reset row rendition within the
operation that replaces the cells; partial and selective erasures retain their
existing policies. EL continues to preserve rendition.

The first 17 cases in `LineRenditionRowTests` reproduced 13 failures: all 12
row-movement cases prematurely released surviving hyperlink references, and RIS
left enlarged-row metadata behind. Copies now retain each destination reference
before releasing either overwritten owner; RIS uses the shared row clearing
path. The final 26 cases also cover partial/protected display erasure, history
eviction, independent snapshots and alternate-screen resize with and without
reflow.

Text/history identities and graphics placements retain their existing owners.
Their coordinated update ordering remains at the operation boundary, including
Sixel damage suppression during scrolling and disposal-abort handling. This is
not a new combined text/graphics storage abstraction.

Validation: 5,202 affected tests passed, including terminal conformance, the
rendition suites, reflow, capture, HWT1/HMP1, scrollback, text anchors, hyperlinks,
KGP and Sixel. Reproduce:

```sh
dotnet test tests/Hex1b.Tests/Hex1b.Tests.csproj -v q --filter \
  'FullyQualifiedName~LineRendition|FullyQualifiedName~TerminalCaptureTests|FullyQualifiedName~Conformance|FullyQualifiedName~Reflow|FullyQualifiedName~Hwt1|FullyQualifiedName~Hmp1|FullyQualifiedName~AnsiToken|FullyQualifiedName~TerminalRegionAnsi|FullyQualifiedName~TextAnchor|FullyQualifiedName~Hyperlink|FullyQualifiedName~Kgp|FullyQualifiedName~Hex1bTerminalTests|FullyQualifiedName~CellImpact|FullyQualifiedName~Sixel|FullyQualifiedName~Grapheme|FullyQualifiedName~Scrollback'
```

### Third increment: operation-local geometry

Removed the blanket post-token cursor clamp. Absolute and relative positioning,
vertical movement, saved-cursor restoration, scrolling beneath a stationary
cursor, and final graphics cursor placement now enforce the destination row's
logical width within the responsible operation. Saved rows are also bounded
after viewport shrink. LF below a partial scroll region no longer scrolls that
unrelated region.

`Hex1bTerminal.Graphemes.cs` unifies retroactive variation-selector and split
cluster updates. Widening uses the shared printable-glyph path when it must
wrap, retaining the source style and hyperlink rather than adopting the current
pen. Shrinking releases continuation-cell ownership. Both paths report cell
impacts and respect logical capacity, including a one-column destination.
Aborted scrolling releases the temporary glyph owner and stops the token.
Wide-wrap padding now also marks the physical row boundary used by reverse wrap.

The first 26 cases in `LineRenditionGeometryTests` reproduced 14 failures:
saved cursors outside a resized viewport, split emoji leaving an extra deferred
wrap, lost style during glyph widening, an incomplete glyph on a one-column
row, and LF scrolling below its region. The final corpus contains 46 cases.
It covers rune-chunked emoji, combining marks and Indic clusters; mixed-mode
cursor movement; default tabs; scrolling; shrink/widen ownership; reverse wrap;
and disposal during widening. Six deterministic sequences check cursor bounds,
wide-cell pairing and hidden storage after 1,500 mixed operations, including
editing, alternate-screen switches, mode changes and resize.

Validation: 5,248 affected tests passed using the second increment's regression
command. This does not establish arbitrary byte-chunk equivalence or performance
parity; those remain separate stage 6 gates.

### Fourth increment: text boundaries and state transfer

Logical text consumers now read the authoritative physical-edge soft-wrap flag
without treating inaccessible storage as text. Character, word and line
selection join enlarged soft wraps in both live rows and history; independent
top/bottom halves retain their hard break. Rectangle highlights and copy stay
within each row's logical capacity. Cropping expires inaccessible selections
and markers. Pending-wrap markers follow the destination immediately, and crop
resize and built-in reflow retain valid end boundaries while dropping markers
on clipped wide glyphs.

Snapshots expose rendition and logical width through `IHex1bTerminalRegion`,
with defaults preserving existing region implementations. Region columns remain
stored text-cell coordinates, not scaled display positions. Horizontal and
nested slices retain rendition. Historical-width snapshots distinguish a live
row's real capacity from padded storage; cropped snapshots remove incomplete
wide glyphs and release only their own hyperlink references.

ANSI replay configures destination row modes before printing, allowing autowrap
to preserve soft breaks across different renditions. Public region export
replaces clipped wide-glyph fragments with spaces instead of writing beyond the
region. HTS (`ESC H`) is now parsed, applied and serialized, alongside tab-clear
serialization. Capture tests cover default and custom tab stops after replay;
direct assertions establish the actual custom-stop column.

Public reflow context, result and history rows now carry rendition metadata.
Legacy providers may omit screen metadata for normal-only input. Enlarged
screen or history input requires one valid rendition per output screen row;
explicit single-width values declare intentional normalization. Invalid
metadata is rejected before coordinate invalidation or reflow mutation begins.
External providers retain their existing lack of text/graphics lineage: this
change does not promise anchor preservation through arbitrary third-party
reflow. Built-in crop strategies now include newly added normal rows in their
metadata arrays.

HMP1's optional history extension is now version 2, with a validated rendition
byte per history row. Base HMP1 and command-mark extension versions are
unchanged. Version-1 history peers fall back to screen-only transfer rather than
misinterpreting the new row encoding. Late attach, resize, reconnect and two-hop
tests compare row modes as well as cells; unknown rendition bytes are rejected.
See [the protocol specification](muxer-protocol.md).

`LineRenditionStateTests` adds 30 cases. History codec/legacy negotiation and the
capture matrix have additional cases. Validation: 5,354 affected tests passed,
including the earlier suites and the wider terminal-region suite:

```sh
dotnet test tests/Hex1b.Tests/Hex1b.Tests.csproj -v q --filter \
  'FullyQualifiedName~LineRendition|FullyQualifiedName~TerminalCaptureTests|FullyQualifiedName~Conformance|FullyQualifiedName~Reflow|FullyQualifiedName~Hwt1|FullyQualifiedName~Hmp1|FullyQualifiedName~AnsiToken|FullyQualifiedName~TerminalRegion|FullyQualifiedName~TextAnchor|FullyQualifiedName~Hyperlink|FullyQualifiedName~Kgp|FullyQualifiedName~Hex1bTerminalTests|FullyQualifiedName~CellImpact|FullyQualifiedName~Sixel|FullyQualifiedName~Grapheme|FullyQualifiedName~Scrollback'
```

### Fifth increment: visuals, graphics, chunks and performance

SVG exports now scale glyphs and decorations inside physical-row clips, scale
backgrounds and cursor columns horizontally, and exclude hidden storage. Full
snapshots retain the physical viewport width, including the one-column case;
horizontal logical subregions expand enough to show their selected text cells.
HTML inspection shares that SVG and maps hover/click/highlight coordinates back
to logical cells. Live browser inspection verified a bottom-half cell, tooltip
pin/unpin and rejection of the unused physical column in an odd-width viewport.

Renderer quad tests cover all underline styles, overline/strike, colored-glyph
quads, links, selection, three cursor shapes, odd widths and one-column clipping.
The live `src/web-terminal/tests/line-rendition.browser.js` fixture additionally
checks actual glyph/decorations pixels on WebGL2 and WebGPU with Cascadia Mono NF
and generic monospace at backing scales 1, 1.25, 1.5 and 2. It uses combining
marks, a wide CJK glyph and a ZWJ emoji; its pixel assertions establish clipping
and visible halves, not exact font-dependent emoji raster fidelity. Repeating
100 rendition changes leaves glyph count, atlas size and uploaded glyph bytes
unchanged; disposal releases the caches. Real pointer events at four CSS scales
exercise historical local selection, hyperlinks, live application input and
hover refresh after rendition changes.

Reproduce the browser matrix after building the package:

```sh
npm run build --prefix src/web-terminal
npm test --prefix src/web-terminal
python3 -m http.server 5398 --bind 127.0.0.1 --directory src/web-terminal
# In another terminal, with a Playwright browser installed:
playwright-cli -s=rendition open http://127.0.0.1:5398/tests/
playwright-cli -s=rendition run-code --filename=src/web-terminal/tests/line-rendition.browser.js
playwright-cli -s=rendition close
```

The fixture deliberately requires both backends; lack of WebGPU is not counted
as a pass. The original WebTerminalDemo fixture continues to cover the tape,
late attachment and resize; core HWT1 tests cover retained-history semantics.

#### Graphics policy

Images remain in physical cell/pixel coordinates. DEC rendition scales text,
not image rasters or declared placement sizes. Cursor-anchored Sixel/KGP
placement projects logical X to physical X; post-image cursor movement maps back
using the destination row's rendition. Text writes and erasures damage all
physical Sixel columns covered by a logical cell, but do not damage KGP images.
Cropping inaccessible storage on a mode change does not itself erase an image.
Image anchors on enlarged rows survive reflow while inside the new physical
viewport, even when beyond that row's logical text capacity.

KGP Unicode placeholders are text anchors for physical-sized image fragments.
Their positions follow enlarged text columns, but their tiles do not stretch:
adjacent placeholders on an enlarged row produce separate one-physical-cell
fragments, with space between them. Inherited source coordinates still advance
one tile per placeholder. Normalizing the row rejoins contiguous fragments.
Live and historical projection use the same policy. This is an explicit Hex1b
compatibility choice, not a DEC graphics requirement.

New graphics cases cover mode changes, placement origins, post-Sixel cursor,
wide-glyph overlap cleanup, EL damage, cursor-based KGP deletion, reflow and
live/history virtual placements. Existing Sixel/KGP regressions run alongside
these cases.

#### Byte-stream profile

`LineRenditionChunkTests` feeds the actual output pump chunks of 1, 2, 3, 7 and 31
bytes and compares against a whole-stream reference. It covers UTF-8, split
escape sequences, VS16, combining marks, ZWJ emoji, links, mode switches,
history, alternate screen and HTS. The profile explicitly enables
`SupportsRetroactiveVariationSelectors`; legacy profiles that disable
retroactive widening are not claimed to be chunk-equivalent for VS16.

#### Ordinary-text performance

`TerminalTextBenchmarks` measures pre-tokenized scrolling output (100 lines),
snapshot capture, a 80-to-60-to-80 reflow cycle and full HWT1 frame generation.
Identical benchmark source ran against pre-feature revision
`b54fb0f33e3c9019b9403f6fa6dd46b17173bab6` and the working implementation.
BenchmarkDotNet used one launch, five warmups and ten measured iterations on an
Apple M5 Max, macOS, .NET 10.0.12, Release builds:

| Operation | Baseline mean | Final mean | Baseline allocation | Final allocation |
|---|---:|---:|---:|---:|
| Scrolling output | 1,383.76 us | 1,013.74 us | 1,250.06 KiB | 906.31 KiB |
| Snapshot | 35.64 us | 35.35 us | 284.09 KiB | 284.52 KiB |
| Reflow cycle | 441.62 us | 440.96 us | 3,396.07 KiB | 3,408.16 KiB |
| Full HWT1 frame | 163.89 us | 156.95 us | 625.84 KiB | 547.70 KiB |

An initial run exposed a scrolling regression. Shared row copying now uses a
bulk cell copy when neither cell impacts nor Sixel damage must be reported,
while retaining source hyperlink owners before releasing destination owners.
The impact-collecting path is unchanged. Ordinary text also avoids Sixel
coordinate work when no active placements exist. The small snapshot/reflow
allocation increases retain row metadata; the measured output/frame paths
allocate less than the baseline. These results describe this workload and
machine, not a universal performance guarantee.

```sh
dotnet run -c Release --project benchmarks/Hex1b.Benchmarks -- text \
  --filter '*TerminalTextBenchmarks*' --warmupCount 5 --iterationCount 10 --launchCount 1
```

Validation for this increment: 5,377 tests passed using the preceding broad
regression selector, plus the 459-test browser package suite, all 16 live
GPU/font/scale combinations and four live pointer-scale cases. WebTerminalDemo
builds with no warnings or errors. Platform-specific font rasterization and other
browser/driver combinations still require their normal release qualification.

### Fix-first review

Regression cases reproduced and resolved four integration gaps: capture seeding
lost mixed-rendition soft wraps; returning from the alternate screen cropped
unchanged enlarged rows and lost their wrap flags; DECIC/DECDC edited hidden
storage instead of each row's logical columns; and positional KGP deletion
projected children of virtual placements without the parent row's rendition.

Capture now establishes tabs and row modes before painting. Unchanged-width
screen restoration avoids unnecessary cropping. Column edits share the
single-row character-editing paths, including wide-glyph cleanup, text-anchor
updates and retained hyperlink ownership. KGP deletion uses the same rendition
metadata as rendering. The affected regression selector above passes 5,393
tests, including the new reproductions and strengthened wrap-state assertions.
