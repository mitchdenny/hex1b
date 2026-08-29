# KGP validation

`KgpValidation` is a human-verifiable Kitty Graphics Protocol (KGP) compliance
harness for `Hex1bTerminal`.

It intentionally does **not** use `Hex1bApp`, widgets, nodes, or layout. A custom
`IHex1bTerminalWorkloadAdapter` writes raw ANSI and KGP byte streams into
`Hex1bTerminal`, then accepts a small set of navigation keys from the terminal's
normal input path.

## Run

Use a terminal with KGP support, such as Kitty, Ghostty, or a compatible xterm.js
addon:

```bash
dotnet run --project samples/KgpValidation
```

The text shell remains useful in a terminal without KGP support, but the graphic
expectations cannot be verified there. Resize the terminal to at least `80x24`.

## Navigation

| Key | Action |
| --- | --- |
| `N`, `Space`, `Right`, `PageDown`, `Enter` | Next scenario |
| `P`, `Left`, `PageUp`, `Backspace` | Previous scenario |
| `1`-`9` | Jump to one of the first nine scenarios |
| `Home` / `End` | First / last scenario |
| `R` | Toggle the current scenario's interactive state, when offered |
| `Q`, `Esc`, `Ctrl+C` | Exit |

Each page states:

- the compliance area under test;
- the exact final result that should be visible;
- the relevant KGP actions and control keys; and
- a deliberately small graphic recipe.

## Scenarios

| Page | Compliance area | Visual check |
| --- | --- | --- |
| Compliance overview | Current scope and known limits | Text-only implementation summary |
| Direct and chunked transfer | Direct RGB/RGBA and `m=1/0` continuations | Gradient plus complete RGB bars |
| Shared data and named replacement | Reused image bytes and `(i,p)` replacement | Three targets, one moved cyan block, no red ghost |
| Source rectangles and display geometry | `x/y/w/h` crop and `c/r` sizing | Full quadrant reference plus four solid crops |
| Z-order and text occlusion | Negative/positive `z` | Text above blue; orange above dotted cells |
| Scrolling and placement anchors | Scroll margins and `CSI S` | Press `R` to watch image and marker move together |
| Unicode placeholder placement | `U=1` and U+10EEEE cells | One continuous image over a 6x3 placeholder grid |
| Relative placement graph | `P/Q/H/V` and root replacement | Parent and descendants move as one graph |
| Animation frame storage | Frame append/edit/delete | Cyan edited root with one frame remaining |
| Deletion and image lifetime | `d=i/I` and reference-aware reclamation | Only the reused right placement remains |

The animation page is intentionally explicit about the current boundary:
animation frame storage/edit/delete is implemented, while playback control and
frame composition are still typed no-ops.

## Structure

| File | Responsibility |
| --- | --- |
| `KgpValidationWorkload.cs` | Raw workload lifecycle, resize handling, and navigation |
| `KgpValidationFrameRenderer.cs` | Shared explanatory header/footer and page isolation |
| `KgpProtocolWriter.cs` | ANSI cursor movement and APC/KGP framing |
| `KgpImageFactory.cs` | Dependency-free deterministic RGB/RGBA fixtures |
| `KgpValidationScenario.cs` | Scenario contract and expected machine state |
| `KgpScenarioCatalog.cs` | Stable scenario ordering |
| `Scenarios/*.cs` | One documented protocol recipe per compliance area |

Every rendered page starts by deleting active KGP data and clearing the screen.
This makes each scenario independent: stale state from one page cannot make the
next page appear to pass.

The same recipes are also parsed by headless tests. Image identity, placement
geometry, z-order, source rectangles, virtual-placement counts, and frame counts
are machine-checked; final compositing remains a human visual check.

## Debugging a failure

1. Read the page's **Expected** and **Protocol** lines.
2. Open the matching class in `Scenarios/`; the exact KGP control strings are at
   the call site.
3. Compare the final image IDs and placement counts with that scenario's
   `ExpectedState`.
4. Run the focused headless tests:

   ```bash
   dotnet test tests/Hex1b.Tests/Hex1b.Tests.csproj \
     --filter FullyQualifiedName~KgpValidationSampleTests
   ```

5. If the headless state passes but the terminal is visually wrong, capture the
   workload stream with `WithWorkloadLogging` or use the enabled
   `KgpValidation` diagnostics socket to separate Hex1b state from presentation
   renderer behavior.

All KGP commands use `q=2`, so terminals intentionally suppress both success and
error replies. A missing image will not print an inline protocol error; use the
headless tests, workload logging, or diagnostics socket to inspect that failure.

Keep new scenarios small. Add one class, document one expected final screen, and
pair it with a `KgpScenarioExpectation` so human and automated checks describe
the same contract.
