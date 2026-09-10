# Tape automation

Hex1b parses Charmbracelet VHS `.tape` scripts into a typed, inspectable document.
Playback is a separate operation with an intentionally narrower set of supported
effects.

The reference grammar is VHS v0.11.0, pinned to
`c073383b5de0b1f57bf514113029c306bc986539`.

## Parse a tape

```csharp
using Hex1b.Automation;

var parser = new TapeParser();
var tape = await parser.ParseAsync(new FileInfo("demo.tape"));

foreach (var command in tape.Commands)
{
    Console.WriteLine($"{command.Span.Line}: {command}");
}
```

`Parse(string)` always takes source text, not a file name. `ParseAsync` accepts a
`FileInfo`, `Stream`, or `TextReader`. The file overload owns its input; the
stream and reader overloads leave caller-owned input open, including on errors
and cancellation. Streams are read as UTF-8 from their current position and need
not support seeking.

`Parse` throws `TapeParseException` containing all parsing diagnostics.
`TryParse(text, out document, out diagnostics)` instead returns `false` and a
null document. Valid commands remain in the AST even if this playback engine
cannot implement them. For example, a video output directive is a valid
`TapeOutputCommand`, not a parsing error.

## Edit the command sequence

`TapeDocument.Commands` is a mutable list. Add, insert, remove, reorder, or
replace commands directly; use `new TapeDocument()` for an empty document.
Individual command records remain immutable.

```csharp
var tape = new TapeParser().Parse("Output 'demo.txt' Type 'hello' Enter");
foreach (var command in tape.Commands.OfType<TapeOutputCommand>().ToArray())
    tape.Commands.Remove(command);
```

Editing the list preserves the document's source label and syntax-extension
registrations. Validation and playback snapshot the command sequence during
preparation; later edits affect subsequent invocations, not an in-flight run.
The list is not thread-safe, so do not edit it concurrently with that initial
snapshot.

## Play in an owned terminal

```csharp
using Hex1b.Automation;
using Hex1b.Layout;

var tape = new TapeParser().Parse("""
    Set TypingSpeed 0
    Wait />$/
    Type "printf 'hello from tape\n'"
    Enter
    Wait+Screen /\nhello from tape\n/
    """);

using var result = await new TapePlayer().PlayAsync(tape, options: new TapePlaybackOptions
{
    TerminalSize = new Size(80, 24),
    Capture = new TapeCaptureOptions
    {
        AsciinemaPath = "demo.cast",
        GoldenTextPath = "demo.txt"
    }
});

Console.WriteLine(result.FinalSnapshot.GetScreenText());
```

The player owns and cleans up its PTY, shell, and terminal. It captures the final
state before cleanup and does not wait indefinitely for an interactive shell
to exit. `ProcessExitCode` is populated only when natural process exit was
observed, not when cleanup terminates the process.

The default owned-terminal size is 80 columns by 24 rows. `DefaultShell` is a
fallback; a tape's `Set Shell` takes precedence. `Environment` configures the
child's base environment and tape `Env` declarations override it. No host
environment variables or process-global working directory are changed.
`DefaultShell` is a profile name (`bash`, `zsh`, `fish`, `cmd`, `powershell`,
`pwsh`, `nu`, `osh`, or `xonsh`), not a shell command line. The selected
executable must be available in the effective child environment.

### Customize the owned terminal

Set `TapePlaybackOptions.TerminalFactory` to select dimensions, presentation,
filters, a time provider, or a different workload:

```csharp
using Hex1b;
using Hex1b.Automation;

var count = 0;
var tape = new TapeParser().Parse("""
    Wait+Screen /Count: 0/
    Enter
    Wait+Screen /Count: 1/
    """);

using var result = await new TapePlayer().PlayAsync(tape, new TapePlaybackOptions
{
    TerminalFactory = builder => builder
        .WithDimensions(40, 10)
        .WithHex1bApp(context => context.Button($"Count: {count}")
            .OnClick(_ => { count++; }))
        .Build()
});

Console.WriteLine(result.FinalSnapshot.GetScreenText());
```

The builder starts with a headless presentation. If you do not select a
workload, the player supplies the default shell. Selecting `WithHex1bApp`,
`WithPtyProcess`, `WithProcess`, or `WithWorkload` replaces that default; the
player still owns the constructed terminal and its workload.

The factory configures the supplied builder, calls `Build` exactly once, and
returns that terminal. Do not start the workload inside the factory. The player
validates the configured builder before construction, attaches capture before
startup, and disposes the terminal and workload when playback ends. Leaving
`TerminalFactory` unset uses the default factory, which calls `Build` for you.

The factory runs once per playback and is never invoked by `ValidateAsync`.
With a custom factory, validation reports a warning that workload-specific shell
checks and adapter capabilities are deferred to playback. Preparation during
validation uses the system time provider; playback uses the configured terminal's
provider. The document and includes are snapshotted before the factory runs,
so changes made inside it do not replace that execution's source.
Adapter-dependent checks run after construction, before playback starts.

An explicit `TapePlaybackOptions.TerminalSize` overrides builder dimensions.
`DefaultShell`, `Environment`, `InheritEnvironment`, and the tape's `Shell`,
`Env`, and `Require` commands apply only to the default shell. Explicit shell
launch options and these commands are rejected for a custom workload. Configure
that workload through its builder method instead. `WorkingDirectory` still
resolves tape includes and output paths; it does not change a custom workload's
working directory.

## Play against an existing terminal

```csharp
var player = new TapePlayer();
var validation = await player.ValidateAsync(tape, terminal);
if (!validation.CanExecute)
{
    foreach (var diagnostic in validation.Diagnostics)
        Console.Error.WriteLine(diagnostic.Message);
    return;
}

using var result = await player.PlayAsync(tape, terminal);
```

The caller supplies the live terminal and owns its workload lifecycle. Playback
does not restart or dispose either of them. This overload uses the supplied
terminal instead of invoking `TerminalFactory`, including during validation.
Disposing the result releases only its final
snapshot. The existing dimensions are retained unless `TerminalSize` explicitly
specifies a cell-size override. Overlapping Tape playbacks on one terminal are
rejected; unrelated external input is not blocked.
HMP1-backed terminals must retain their producer-owned dimensions: a cell-size
override is rejected during preflight rather than reporting an unconfirmed resize.

Both `PlayAsync` overloads prepare and validate every invocation automatically.
Calling `ValidateAsync` is useful for presenting diagnostics, but is not a token
that bypasses validation on a later call. Includes and options are resolved once
per invocation and the resulting prepared commands are used for that invocation.
No input, resizing, output-file creation, or child launch takes place until
preflight succeeds.

## Supported effects and compatibility boundaries

Core typing, supported terminal keys/chords, sleeps, regex waits, typing/wait
settings, includes, and capture visibility controls run through Hex1b input
sequences. With its default owned shell, the player also handles shell selection,
child environment declarations, and executable requirements.

The initial contiguous `Set`/`Output`/`Require` prefix configures execution.
After that prefix, `TypingSpeed` can still change dynamically; late
`WaitPattern` and `WaitTimeout` settings are ignored with diagnostics.
With the default owned shell, late `Require` commands are likewise ignored. Shell selection
and `Env` declarations are collected before launch regardless of their position.

Playback cannot change how an existing or custom workload was launched, so it
rejects `Shell`, `Env`, and `Require` effects for those targets. Video/PNG outputs, screenshots,
fonts and presentation settings, clipboard operations, and VHS viewport
scrolling are initially unsupported. They parse normally and produce explicit
execution diagnostics. VHS scrolling is not translated into mouse-wheel input.

VHS `Width` and `Height` are **pixels**, not character cells. Hex1b never
reinterprets them as columns and rows. Use the explicit `TerminalSize` option
for cell-based sizing.

Wait syntax is checked against Go regular-expression rules. Execution uses a
compatible subset of those rules rather than silently treating every Go
pattern as a .NET regex. Valid patterns outside that subset are rejected in
preflight. In particular, unsupported regex flags/escapes and POSIX character
classes receive execution diagnostics. Case-insensitive (`i`) folding, the
ungreedy (`U`) flag, word-boundary escapes, and Unicode property classes are
currently rejected rather than approximated using different .NET semantics.

`Source` paths are relative to the playback working directory, not the
including file's directory. Parsing alone does not open includes. Preparation
retains source provenance, discards included `Output` directives as VHS does,
detects cycles, and rejects nesting beyond 256 levels instead of risking
unbounded recursion.

Tape commands and extensions are trusted automation code, not a sandbox.
Typing into a shell can execute arbitrary programs. Removing syntax entries
controls accepted script commands, not what shell input or callback code can do.
Use an appropriately isolated workload for untrusted input.

## Captures

No artifact is written unless requested through capture options or a supported
text `Output` directive. Paths resolve against the playback working directory;
existing destinations are not overwritten unless `Overwrite` is explicitly
enabled. Parent directories must already exist.

`AsciinemaPath` writes asciicast v2: terminal output, timing, initial state, and
resizes. It is an additional Hex1b output option; `.cast` is not advertised as a
native VHS `Output` format. Movie rendering and font selection belong to a
separate rendering tool.

Golden text is UTF-8 without a BOM, uses LF line endings, trims row-end
whitespace, and appends an 80-character `U+2500` separator after each visible
executed command. Identical snapshots are retained. A custom command
has one checkpoint, regardless of how many input steps it generates. Golden
text contains neither styling nor cursor metadata.

For compatibility with the pinned VHS buffer reader, golden snapshots read
terminal-height rows starting at active-buffer index zero. This can differ
from the visible viewport when the primary screen has scrollback.

`Hide` continues executing and updating terminal state without capturing it.
`Show` resynchronizes the recording to the authoritative state and dimensions,
with hidden time removed from recording timing. The result's `Elapsed` still
includes hidden execution time.

Command completion is not an assertion that the application has responded.
Scripts must use `Wait` or `Sleep` where synchronization is required; playback
does not add an implicit settling delay.

Successful execution finalizes captures before returning. A runtime failure
throws `TapePlaybackException` with the source command, completed-command count,
failure text, original exception, and partial artifact paths. Cancellation
remains `OperationCanceledException`. Partial captures are finalized where
possible without converting a failed run into success.

## Commands and extensions

`TapeParserOptions.SyntaxExtensions` contains all the built-in VHS command
callbacks by default. Add a keyword to extend the language, replace its dictionary
entry to change its behavior, or remove it to disable that command. There is no
separate command policy or compiler registry.

Each parser callback reads operands and returns a playback callback. During
preparation, that callback receives `TapePlayContext` and returns either
`TapeCommandResult.Reject(message)` or
`TapeCommandResult.Accept(sequenceBuilder)`. Acceptance means the command is
ready for preparation, not that it has executed. The player gathers results for
every command before sending any input, resizing, starting its workload, or
creating captures. A returned error stops the whole tape up front, even when
earlier commands returned accepted builders.

For example, add a `WaitForText` command with operand validation and its
execution sequence in the same callback:

```csharp
using Hex1b;
using Hex1b.Automation;

var options = new TapeParserOptions();
options.SyntaxExtensions["WaitForText"] = parse =>
{
    var operand = parse.Reader.Read();
    return play =>
    {
        if (operand.Kind != TapeTokenKind.String ||
            string.IsNullOrWhiteSpace(operand.Value))
            return TapeCommandResult.Reject("WaitForText requires a non-empty string.");

        return TapeCommandResult.Accept(
            new Hex1bTerminalInputSequenceBuilder()
                .WithOptions(play.SequenceOptions)
                .WaitUntil(snapshot => snapshot.ContainsText(operand.Value),
                    play.WaitTimeout, $"Waiting for '{operand.Value}'"));
    };
};

var parser = new TapeParser(options);
using var result = await new TapePlayer().PlayAsync(parser.Parse("WaitForText 'Ready'"),
    new TapePlaybackOptions
    {
        TerminalFactory = builder => builder.WithDimensions(30, 5)
            .WithHex1bApp(context => context.Text("Ready"))
            .Build()
    });
Console.WriteLine(result.FinalSnapshot.GetScreenText());
```

The outer callback receives a `TapeParseContext`. Its `Reader` is positioned
after the keyword has been consumed. Read operands with `parse.Reader.Read()`
and report syntax errors with `parse.Report(...)` here; source information is
available through `parse.CommandSpan` and `parse.SourceName`.
Capture immutable operand tokens or their values in the returned callback, not
the context or reader. Instructions are token-based, not line-based; consume only
the operands belonging to your command. Registered bare keywords delimit commands
just like built-ins; quote a keyword when using it as a string operand.

`TapePlayContext` provides effective `TypingSpeed`, `WaitTimeout`, and
`SequenceOptions` at the command's position. It is used by both `ValidateAsync`
and `PlayAsync`; callbacks must create builders without executing input. Accepted
builders are immediately built into sequence snapshots, then playback applies
those sequences in order. `ValidateAsync` returns the same preparation diagnostics
without executing the sequences. Calling playback afterwards prepares them again.
`WaitForText ''` therefore returns a preparation error; text that never appears
instead fails at runtime when the wait times out.

Each occurrence is stored as a `TapeExtensionCommand` with its `Keyword` and
full consumed source `Span`. Returned errors carry that source location. Commands
can be moved or removed through `TapeDocument.Commands`; a custom command's captured
operands are opaque rather than exposed as typed properties. Default built-ins
retain their typed command records for inspection and editing. One extension
command produces one normal golden checkpoint regardless of its number of input steps.

Built-in directives such as `Source`, `Set`, and `Hide` still need their include,
configuration, and capture handling; they are not approximated as keystrokes.
Their syntax entries can nevertheless be removed, replaced, or reused like any
custom entry:

```csharp
var options = new TapeParserOptions();
options.SyntaxExtensions.Remove("Source");
options.SyntaxExtensions.Remove("Output");
options.SyntaxExtensions["Pause"] = options.SyntaxExtensions["Sleep"];
var parser = new TapeParser(options);
```

The dictionary uses case-sensitive keys. Use `Add` to reject duplicate keys or
the indexer to replace a callback; `Remove` and `Clear` work normally. Invalid
keywords, reserved setting/unit/boolean names, and null parsing callbacks are
rejected when constructing the parser. Null playback callbacks or results are
implementation errors; unexpected callback exceptions propagate rather than
being hidden as validation failures.

Each parser snapshots the dictionary at construction, and included tapes use
that same parser. Later additions, replacements, and removals affect only newly
constructed parsers. Do not edit the dictionary concurrently with construction.
No type discovery or reflective construction is involved.

Editing `TapeDocument.Commands` remains available, including adding typed
built-in commands programmatically. Syntax restrictions apply to parsing, not
to deliberate programmatic document edits.

The offline conformance corpus and its reference-generation instructions live
under `tests/Hex1b.Tests/TestData/Tape/` and `tools/TapeConformance/`.

Run the cross-platform shell example with `dotnet run --project samples/TapeDemo`.
Pass a tape path and an existing output directory to also capture `demo.cast`
and `demo.txt`; existing artifacts are not overwritten.

For a longer recording, `samples/TapeDemo/asciiearth.tape` launches the
AsciiEarth sample, waits for its controls, presses T to enable the tour, waits
five seconds after the tour indicator appears, and sends Ctrl+C to return to
the shell. Run this source tape from a repository checkout:

```sh
out="$(mktemp -d)"
dotnet run --project samples/TapeDemo -- \
  "$PWD/samples/TapeDemo/asciiearth.tape" "$out"
```

The nested `dotnet run` builds AsciiEarth as needed; the tape allows up to two
minutes for startup. AsciiEarth uses cached or downloaded OpenStreetMap tiles,
so map detail depends on tile availability.
