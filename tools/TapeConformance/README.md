# Tape parsing conformance

Normal tests are **offline and Go-free**. They read embedded resources from
`tests/Hex1b.Tests/TestData/Tape`. No test executes a fetched tape, starts VHS,
checks whether an example application is installed, or generates media.

## Reference and provenance

The reference is [charmbracelet/vhs at
c073383b5de0b1f57bf514113029c306bc986539](https://github.com/charmbracelet/vhs/tree/c073383b5de0b1f57bf514113029c306bc986539),
using **Go 1.25.12**. This is VHS v0.11.0 language syntax at a pinned subsequent
commit. `go.mod` and `go.sum` pin the importable module. The oracle imports only
`lexer`, `parser`, and `token`; its dependency graph contains no browser, ttyd,
FFmpeg, or VHS executable.

`upstream/` contains **all 106 upstream `.tape` files, exactly 27,891 bytes**, and
the original MIT license. It does not contain referenced media or example
applications. `provenance.json` records upstream paths, SHA-256 hashes, byte
lengths, and commit IDs. Its additional Go-source entries identify the sources
used to extract inline fixtures; those development inputs are fetched into the
ignored `.work/` directory, not embedded or vendored.
The root `THIRD-PARTY-NOTICES.txt`, included in the Hex1b NuGet package, also
attributes the adapted shipped lexer/parser and reproduces the complete VHS
MIT license.

The generator uses Go's source AST to extract 23 inline lexer/parser inputs
(including table cases and Source fixture bundles). Each extracted case records
its original function/subtest, upstream path, and line. The upstream file-based
`TestLexTapeFile` and `TestParseTapeFile` both use `examples/fixtures/all.tape`,
already included in the complete corpus. Upstream `TestToCamel` tests a display
helper, not tape parsing, and is not imported.

## Regeneration

Run from the repository root:

```sh
python3 tools/TapeConformance/fetch.py
cd tools/TapeConformance
mkdir -p .work/build
GOTMPDIR="$PWD/.work/build" GOTOOLCHAIN=go1.25.12 go run .
```

Regeneration explicitly requires network access to download pinned source and,
on first use, the pinned Go toolchain/module. Normal tests do not.
`fetch.py` rejects a truncated tree or changed tape count. The Go oracle refuses
other toolchain versions and checks Source graphs for cycles and paths outside
the fixture root **before** invoking the unmodified parser. Source fixtures are
materialized beneath `.work/cases/`; only text is parsed. Nothing in a tape is
passed to a shell. The entire `.work/` directory may be removed after generation.

`supplemental.json` contains independently authored cases. The generator also
adds relevant invalid-operand/trailing-token cases for every dispatched command
and every setting. For generic settings that accept arbitrary operands,
unsupported trailing units are syntax errors; numeric conversion is not
invented as a parse-time requirement.

The checked-in snapshot contains **283 cases**: 106 upstream tapes, 23 extracted
inline inputs, 102 authored supplemental inputs, and 52 command/setting negative
probes. The unmodified upstream parser reports 162 successes and 119 failures;
two cyclic Source probes are guarded rather than run. The pure syntax projection
has 169 successes and 114 failures.

To validate the checked-in corpus:

```sh
dotnet test --project tests/Hex1b.Tests/Hex1b.Tests.csproj \
  --filter 'FullyQualifiedName~TapeConformanceTests' --no-progress
```

## Oracle schema

`expectations.json` stores schema version, reference, toolchain, and inventories
derived from the **actual `parseCommand` switch** and `token.IsSetting`.
Upstream's `CommandTypes` list is not complete, so it is not used.

Each case has:

- `id`, upstream `origin`, input `path` or inline `text`, and `inputSha256`;
- original test/line metadata for extracted cases;
- optional `files`, mapping working-directory-relative Source paths to text;
- `tokens`: exact upstream `{type, literal, line, column}`, including EOF;
- `commands`: exact upstream `{type, options, args, source}`;
- `errors`: exact upstream error token and message;
- `status`: `success`, `error`, or an explicitly guarded Source cycle;
- optional `syntax`: the Source-retaining projection described below.

Fields come directly from Go structs, **not `Command.String()`**, which drops
information. Upstream currently leaves the command `source` field empty;
Hex1b source provenance is asserted separately.

Invalid examples remain invalid expectations. Specifically,
`examples/errors/parser.tape` fails parsing; the other 105 upstream tapes parse,
including examples whose dimensions or requirements would fail execution.

## Intentional differences and named coverage

| Difference | Comparison and named cases/tests |
| --- | --- |
| Pure parsing retains `Source`, while upstream opens and expands it | Cases with Source retain the original expansion/errors and additionally have a `syntax` projection. Syntactically valid `.tape` operands are routed through the actual upstream one-string `Env` parser with a reserved marker, then normalized back to `SOURCE`. Invalid Source syntax is still passed unmodified to upstream. `Parse_OracleCorpus_MatchesNormalizedCommandStreamAndTryParse` compares this syntax stream; `ResolveAsync_SourceBundles_MatchesExpandedOracleCommands` compares actual Hex1b resolution against the original expanded stream. |
| Cyclic Source is unsafe upstream | `source-cycle` and `source-self-cycle` are guarded before upstream recursion. `Parse_CyclicSourceFixtures_RetainsSourceWithoutOpeningFiles` verifies pure parsing; the resolution test requires a managed error. The fixture does not claim an observed upstream diagnostic for a probe that was deliberately not run. |
| Managed spans use UTF-16 offsets/lengths and UTF-8 byte columns | Commands assert valid source ranges; tokens/diagnostic positions compare upstream byte columns. `unicode-columns` covers supplementary, combining, and CJK characters. Upstream's token positions, including its unterminated-literal newline/EOF quirks, are preserved in the raw oracle data. |
| Illegal Unicode is diagnosed by character rather than by individual UTF-8 byte | `unicode-bare` and `bom` require the same rejection. Lexer comparisons expand the managed illegal character to upstream byte-literals. Error wording/count is not equated for those two representation cases. |
| Native diagnostic codes/messages | Raw upstream messages remain in the oracle. Tests compare error positions/count, stage/severity/source, and complete Parse/TryParse/async diagnostic equality, not Go's formatted human-readable message. |
| File/stream input does not remove a BOM | `bom` checks the same rejection across string, stream, reader, and file entry points. |
| NUL terminates upstream lexing | `nul` records and tests the same EOF behavior rather than silently rejecting or reading past it. |
| Typed AST versus flat Go commands | Tests normalize every concrete command family, including defaults, delay/repeat spellings, setting normalization, modifier ordering, output extension inference, and Wait scope/pattern. This is not merely a parse-success check. |
| Output capability is not syntax | `output-formats`, every upstream media tape, and `Parse_UnsupportedVideoAndPresentationSyntax_ReturnsTypedAst` ensure unsupported media/presentation syntax remains accepted. Runtime preflight tests own capability rejection. |

Source bundles cover missing, empty, comment-only, malformed, nested, and cyclic
files, working-directory-relative resolution (not include-directory-relative),
filtering included Output commands, and preserved managed source provenance.
All file-writing tests use isolated directories under the working directory's
`TestResults/TapeConformance/` and delete their files in `finally`.

## Coverage invariants

The suite verifies original file hashes, all 106 inventory entries, every
recognized command and setting in successful typed ASTs, and a relevant invalid
case for each. Every case compares `Parse`, both `TryParse` overloads, reader,
stream, and file parsing. Lexer comparisons use the same entire corpus, including
invalid cases. Basic parser tests separately cover cancellation, non-seekable
streams, current-position reads, split UTF-8 input, and syntax extensions.
