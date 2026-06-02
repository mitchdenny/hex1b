using AgenticPromptDemo;
using Hex1b;
using Hex1b.Composition;
using Hex1b.Input;
using Hex1b.Widgets;

namespace AgenticPromptDemo.Tests;

/// <summary>
/// Tests for <see cref="SlashCommandPromptWidget"/> in isolation. Each test
/// hosts the widget inside a minimal composite shell, drives it through the
/// headless input pipeline, and asserts on the rendered terminal snapshot
/// (and on captured submit payloads where relevant).
///
/// Hosts are intentionally varied (no surrounding focusables, focusable
/// sibling above, multiple focusables) so we cover the trees the demo —
/// and other consumers — actually use.
/// </summary>
[TestClass]
public class SlashCommandPromptWidgetTests
{
    private static readonly IReadOnlyList<SlashCommand> Commands =
    [
        new("picker",        "Reply with action buttons"),
        new("clear",         "Clear the transcript"),
        new("help",          "Show help"),
        new("washthedishes", "Pretend"),
    ];

    // ---- Filter / typing ----

    /// <summary>
    /// Typing '/' as the first character must:
    ///  - open the palette with all known commands listed,
    ///  - leave the literal '/' in the textbox so the user can keep typing
    ///    a prefix to filter the list, AND
    ///  - show a /<first-match> PREVIEW in the textbox (the user hasn't
    ///    confirmed any command yet — the preview is just a visual hint).
    ///
    /// This is the bug the user hit: when the palette opened, the inner
    /// VStack reshuffled and recreated the textbox node — and because the
    /// composite previously passed Text=null (textbox owns its own text),
    /// the new node started empty, dropping the '/' the user just typed.
    /// </summary>
    [TestMethod]
    public async Task TypingSlash_OpensPaletteAndShowsFirstMatchPreview()
    {
        var screen = await DriveAsync(b => b
            .Type("/")
            .WaitUntil(s => s.ContainsText("picker"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(150)));

        // All commands visible in the palette.
        Assert.Contains("picker", screen);
        Assert.Contains("clear", screen);
        Assert.Contains("help", screen);
        Assert.Contains("washthedishes", screen);

        // Textbox shows the preview of the first match — '/picker' — which
        // happens to start with '/' (the literal char the user typed) but
        // also includes the rest of the command name.
        AssertTextboxContains(screen, "/picker");
    }

    /// <summary>
    /// Typing '/p' as you go must filter the palette down to only commands
    /// starting with 'p' AND show a preview of the (now sole) match in the
    /// textbox.
    /// </summary>
    [TestMethod]
    public async Task TypingPrefix_FiltersPaletteAndShowsPreview()
    {
        var screen = await DriveAsync(b => b
            .Type("/p")
            .WaitUntil(s => s.ContainsText("picker"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(150)));

        // Filtered: only 'picker' matches.
        Assert.Contains("picker", screen);
        Assert.DoesNotContain("clear", screen);
        Assert.DoesNotContain("help", screen);
        Assert.DoesNotContain("washthedishes", screen);

        // Textbox shows the preview of the only remaining match.
        AssertTextboxContains(screen, "/picker");
    }

    /// <summary>
    /// Typing a non-slash leading char must NOT open the palette, and the
    /// textbox must hold what was typed.
    /// </summary>
    [TestMethod]
    public async Task TypingPlainText_DoesNotOpenPalette()
    {
        var screen = await DriveAsync(b => b
            .Type("hello")
            .Wait(TimeSpan.FromMilliseconds(250)));

        Assert.DoesNotContain("picker", screen);
        Assert.DoesNotContain("Commands", screen);  // border title
        AssertTextboxContains(screen, "hello");
    }

    // ---- Navigation ----

    /// <summary>
    /// DownArrow with the palette open moves the '❯' marker to the next row.
    /// </summary>
    [TestMethod]
    public async Task DownArrow_MovesPaletteSelection()
    {
        var screen = await DriveAsync(b => b
            .Type("/")
            .WaitUntil(s => s.ContainsText("picker"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(150))
            .Key(Hex1bKey.DownArrow)
            .Wait(TimeSpan.FromMilliseconds(250)));

        var lines = screen.Split('\n');
        var pickerLine = lines.FirstOrDefault(l => l.Contains("picker")) ?? "";
        var clearLine = lines.FirstOrDefault(l => l.Contains("clear")) ?? "";

        Assert.Contains('\u276F', clearLine);
        Assert.DoesNotContain('\u276F', pickerLine);
    }

    /// <summary>
    /// UpArrow at the top of the list is a no-op (does not wrap).
    /// </summary>
    [TestMethod]
    public async Task UpArrow_AtTop_StaysOnFirstRow()
    {
        var screen = await DriveAsync(b => b
            .Type("/")
            .WaitUntil(s => s.ContainsText("picker"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(150))
            .Key(Hex1bKey.UpArrow)
            .Wait(TimeSpan.FromMilliseconds(250)));

        var lines = screen.Split('\n');
        var pickerLine = lines.FirstOrDefault(l => l.Contains("picker")) ?? "";
        Assert.Contains('\u276F', pickerLine);
    }

    /// <summary>
    /// Same scenario as <see cref="DownArrow_MovesPaletteSelection"/> but
    /// with another focusable sibling above the prompt. This is the demo's
    /// real shape (a scrolling transcript above the prompt). Without the
    /// composite-level focus restoration, FocusRing.EnsureFocus snaps to
    /// the FIRST focusable in the app and the DownArrow ends up scrolling
    /// the transcript instead of moving the palette selection.
    /// </summary>
    [TestMethod]
    public async Task DownArrow_WithFocusableAbove_MovesPaletteSelection()
    {
        var screen = await DriveAsync<FocusableAboveHostWidget>(b => b
            .Key(Hex1bKey.Tab)  // focus the textbox past the dummy button
            .Wait(TimeSpan.FromMilliseconds(150))
            .Type("/")
            .WaitUntil(s => s.ContainsText("picker"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(150))
            .Key(Hex1bKey.DownArrow)
            .Wait(TimeSpan.FromMilliseconds(250)));

        var lines = screen.Split('\n');
        var clearLine = lines.FirstOrDefault(l => l.Contains("clear")) ?? "";
        Assert.Contains('\u276F', clearLine);
    }

    // ---- Preview / confirm ----

    /// <summary>
    /// Moving the palette selection with DownArrow updates the preview shown
    /// in the textbox to the newly highlighted command — so the user can see
    /// exactly what would be inserted before they confirm.
    /// </summary>
    [TestMethod]
    public async Task DownArrow_UpdatesPreviewInTextbox()
    {
        var screen = await DriveAsync(b => b
            .Type("/")
            .WaitUntil(s => s.ContainsText("picker"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(150))
            .Key(Hex1bKey.DownArrow)
            .Wait(TimeSpan.FromMilliseconds(250)));

        // Preview followed selection from /picker → /clear.
        AssertTextboxContains(screen, "/clear");
    }

    /// <summary>
    /// Backspace while a preview is showing must shrink the user's TYPED
    /// text by one — not the preview text. After typing '/p' the textbox
    /// shows the preview '/picker'. One backspace takes typed text from
    /// '/p' to '/', which still matches every command, so the preview
    /// remains '/picker' (now showing the first match of the unfiltered
    /// list).
    /// </summary>
    [TestMethod]
    public async Task Backspace_ShrinksTypedTextNotPreview()
    {
        var screen = await DriveAsync(b => b
            .Type("/p")
            .WaitUntil(s => s.ContainsText("picker"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(150))
            .Key(Hex1bKey.Backspace)
            .Wait(TimeSpan.FromMilliseconds(250)));

        // All four commands are now visible (typed text dropped to '/').
        Assert.Contains("picker", screen);
        Assert.Contains("clear", screen);
        Assert.Contains("help", screen);
        Assert.Contains("washthedishes", screen);

        // The preview (still the first match) is /picker.
        AssertTextboxContains(screen, "/picker");
    }

    /// <summary>
    /// A second backspace from the '/' state must clear the typed text,
    /// close the palette and leave the textbox empty — proving that the
    /// preview was a hint, not a commitment.
    /// </summary>
    [TestMethod]
    public async Task BackspaceTwice_ClearsTypedTextAndClosesPalette()
    {
        var screen = await DriveAsync(b => b
            .Type("/p")
            .WaitUntil(s => s.ContainsText("picker"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(150))
            .Key(Hex1bKey.Backspace)
            .Wait(TimeSpan.FromMilliseconds(150))
            .Key(Hex1bKey.Backspace)
            .Wait(TimeSpan.FromMilliseconds(250)));

        Assert.DoesNotContain("Commands", screen);
        Assert.DoesNotContain("picker", screen);

        // Textbox row only contains the prompt chevron — no preview, no typed text.
        var lines = screen.Split('\n');
        var promptRow = lines.First(l => l.Contains(SlashCommandPromptWidget.PromptChevron));
        Assert.AreEqual(SlashCommandPromptWidget.PromptChevron.TrimEnd(), promptRow.TrimEnd());
    }

    /// <summary>
    /// Typing onto a preview must extend the user's TYPED text by the
    /// keystroke — not append to the preview. After '/p' the preview is
    /// '/picker'; typing 'x' must take typed text from '/p' → '/px',
    /// which matches nothing → palette closes → textbox shows literal '/px'.
    /// </summary>
    [TestMethod]
    public async Task TypingOntoPreview_AppendsToTypedTextNotPreview()
    {
        var screen = await DriveAsync(b => b
            .Type("/p")
            .WaitUntil(s => s.ContainsText("picker"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(150))
            .Type("x")
            .Wait(TimeSpan.FromMilliseconds(250)));

        // Palette gone — '/px' matches nothing.
        Assert.DoesNotContain("Commands", screen);
        Assert.DoesNotContain("picker", screen);

        // Textbox shows the literal typed text.
        AssertTextboxContains(screen, "/px");
    }

    // ---- Mouse interaction ----

    /// <summary>
    /// Hovering the mouse over a palette row updates the highlighted
    /// selection and, by extension, the preview shown in the textbox.
    /// </summary>
    [TestMethod]
    public async Task MouseHoverOnPaletteRow_UpdatesPreviewInTextbox()
    {
        var screen = await DriveAsync(b => b
            .Type("/")
            .WaitUntil(s => s.ContainsText("picker"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(150))
            .MouseMoveTo(MouseColumnFor("clear"), MouseRowFor("clear"))
            .Wait(TimeSpan.FromMilliseconds(250)));

        // We can't pre-measure the exact row positions in this builder API,
        // so MouseColumnFor/MouseRowFor return a best-effort coordinate that
        // sits inside the 'clear' palette row at render time. The hover
        // should switch the preview from /picker to /clear.
        AssertTextboxContains(screen, "/clear");

        // Highlighted marker should also have moved.
        var lines = screen.Split('\n');
        var clearLine = lines.FirstOrDefault(l => l.Contains("clear")) ?? "";
        Assert.Contains('\u276F', clearLine);
    }

    /// <summary>
    /// Clicking on a palette row CONFIRMS that command — bakes /<name>
    /// (with trailing space) into the typed text — without firing OnSubmit.
    /// </summary>
    [TestMethod]
    public async Task MouseClickOnPaletteRow_ConfirmsCommand()
    {
        var submitted = new List<string>();
        var screen = await DriveAsync(b => b
            .Type("/")
            .WaitUntil(s => s.ContainsText("picker"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(150))
            .ClickAt(MouseColumnFor("clear"), MouseRowFor("clear"))
            .Wait(TimeSpan.FromMilliseconds(250)),
            submitted.Add);

        // Did NOT submit (clicking confirms but does not send).
        Assert.IsEmpty(submitted);

        // Confirmed text is in the box and the palette is closed (because
        // the trailing space disqualifies the prefix).
        AssertTextboxContains(screen, "/clear");
        Assert.DoesNotContain("Commands", screen);
    }

    /// <summary>
    /// Looks up the on-screen column for a command row in the palette. We
    /// pick a column comfortably inside the border (col 5) so a 1-px
    /// off-by-one in border thickness can't push the click outside the row.
    /// </summary>
    private static int MouseColumnFor(string commandName) => 5;

    /// <summary>
    /// On-screen row for a given command in the palette. The host layout is:
    ///   row 0: "transcript line 1"
    ///   row 1: "transcript line 2"
    ///   row 2: ""
    ///   row 3: separator
    ///   row 4: palette top border
    ///   row 5: /picker
    ///   row 6: /clear
    ///   row 7: /help
    ///   row 8: /washthedishes
    /// </summary>
    private static int MouseRowFor(string commandName) => commandName switch
    {
        "picker"        => 5,
        "clear"         => 6,
        "help"          => 7,
        "washthedishes" => 8,
        _ => throw new ArgumentOutOfRangeException(nameof(commandName)),
    };

    // ---- Accept / Submit ----

    /// <summary>
    /// Pressing Enter while the palette has matches inserts the selected
    /// command (with a trailing space for arguments) into the textbox and
    /// does NOT fire OnSubmit.
    /// </summary>
    [TestMethod]
    public async Task EnterWithVisiblePalette_InsertsSelectedCommand()
    {
        var submitted = new List<string>();
        var screen = await DriveAsync(b => b
            .Type("/p")
            .WaitUntil(s => s.ContainsText("picker"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(150))
            .Key(Hex1bKey.Enter)
            .Wait(TimeSpan.FromMilliseconds(250)),
            submitted.Add);

        Assert.IsEmpty(submitted);
        AssertTextboxContains(screen, "/picker");
    }

    /// <summary>
    /// Tab acts as another accept gesture (parity with Enter while the
    /// palette is visible).
    /// </summary>
    [TestMethod]
    public async Task TabWithVisiblePalette_InsertsSelectedCommand()
    {
        var submitted = new List<string>();
        var screen = await DriveAsync(b => b
            .Type("/p")
            .WaitUntil(s => s.ContainsText("picker"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(150))
            .Key(Hex1bKey.Tab)
            .Wait(TimeSpan.FromMilliseconds(250)),
            submitted.Add);

        Assert.IsEmpty(submitted);
        AssertTextboxContains(screen, "/picker");
    }

    /// <summary>
    /// When no commands match the typed prefix the palette closes, and
    /// pressing Enter submits the literal text via OnSubmit rather than
    /// completing anything.
    /// </summary>
    [TestMethod]
    public async Task EnterWithNoMatches_SubmitsTextAsIs()
    {
        var submitted = new List<string>();
        await DriveAsync(b => b
            .Type("/zzzz")
            .Wait(TimeSpan.FromMilliseconds(250))
            .Key(Hex1bKey.Enter)
            .Wait(TimeSpan.FromMilliseconds(250)),
            submitted.Add);

        Assert.HasCount(1, submitted);
        Assert.AreEqual("/zzzz", submitted[0]);
    }

    /// <summary>
    /// Plain (non-slash) text submits via OnSubmit and clears the textbox.
    /// </summary>
    [TestMethod]
    public async Task EnterWithPlainText_SubmitsAndClears()
    {
        var submitted = new List<string>();
        var screen = await DriveAsync(b => b
            .Type("hello world")
            .Wait(TimeSpan.FromMilliseconds(250))
            .Key(Hex1bKey.Enter)
            .Wait(TimeSpan.FromMilliseconds(250)),
            submitted.Add);

        Assert.HasCount(1, submitted);
        Assert.AreEqual("hello world", submitted[0]);
        Assert.DoesNotContain("hello world", screen);
    }

    /// <summary>
    /// Pressing Escape while the palette is visible clears the textbox and
    /// dismisses the palette.
    /// </summary>
    [TestMethod]
    public async Task EscapeWithVisiblePalette_ClearsTextAndDismisses()
    {
        var screen = await DriveAsync(b => b
            .Type("/p")
            .WaitUntil(s => s.ContainsText("picker"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(150))
            .Key(Hex1bKey.Escape)
            .Wait(TimeSpan.FromMilliseconds(250)));

        Assert.DoesNotContain("picker", screen);
        Assert.DoesNotContain("Commands", screen);
    }

    /// <summary>
    /// After accepting a command the user can keep typing arguments after
    /// the inserted '/cmd ' and submit the full string — which must include
    /// both the command and the typed arguments.
    /// </summary>
    [TestMethod]
    public async Task AcceptThenTypeArgs_SubmitsFullCommand()
    {
        var submitted = new List<string>();
        await DriveAsync(b => b
            .Type("/p")
            .WaitUntil(s => s.ContainsText("picker"), TimeSpan.FromSeconds(5))
            .Wait(TimeSpan.FromMilliseconds(150))
            .Key(Hex1bKey.Enter)        // insert '/picker '
            .Wait(TimeSpan.FromMilliseconds(150))
            .Type("now")
            .Wait(TimeSpan.FromMilliseconds(150))
            .Key(Hex1bKey.Enter)        // submit
            .Wait(TimeSpan.FromMilliseconds(250)),
            submitted.Add);

        Assert.HasCount(1, submitted);
        Assert.AreEqual("/picker now", submitted[0]);
    }

    // ---- Drivers / hosts ----

    private static Task<string> DriveAsync(
        Func<Hex1bTerminalInputSequenceBuilder, Hex1bTerminalInputSequenceBuilder> script,
        Action<string>? onSubmit = null)
        => DriveAsync<HostWidget>(script, onSubmit);

    private static async Task<string> DriveAsync<THost>(
        Func<Hex1bTerminalInputSequenceBuilder, Hex1bTerminalInputSequenceBuilder> script,
        Action<string>? onSubmit = null)
        where THost : Hex1bWidget, new()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // The submit recorder is global to the test for simplicity — every
        // host implementation reads it from this static slot.
        SubmitSink.Set(onSubmit);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new THost()),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true });

        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);

        // Run the user's script and snapshot the final state BEFORE Ctrl+C
        // tears down the alternate screen (which would leave us with a blank
        // screen to assert on).
        var snapshot = await script(new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5)))
            .Build()
            .ApplyAsync(terminal, ct);

        // Now tear down the app cleanly.
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);
        await runTask;

        SubmitSink.Set(null);

        return string.Join("\n", Enumerable.Range(0, 24).Select(r =>
        {
            try { return snapshot.GetTextAt(r, 0, 79); }
            catch { return ""; }
        }));
    }

    private static void AssertTextboxContains(string screen, string expected)
    {
        // The textbox row is the row that holds the prompt chevron prefix.
        // Anchoring on the chevron is more robust than excluding palette
        // rows by command name (which fails when the textbox itself holds
        // a command name like "/picker").
        var lines = screen.Split('\n');
        var textboxRow = lines.FirstOrDefault(l => l.Contains(SlashCommandPromptWidget.PromptChevron));
        Assert.IsTrue(textboxRow is not null, $"Could not locate prompt row in screen:\n{screen}");
        Assert.IsTrue(textboxRow!.Contains(expected),
            $"Textbox row '{textboxRow.TrimEnd()}' did not contain expected '{expected}'.\n\nScreen:\n{screen}");
    }

    /// <summary>
    /// Default host: just the prompt with a transcript stub above. No
    /// extra focusables — useful for tests that don't care about focus
    /// shenanigans.
    /// </summary>
    private sealed record HostWidget : Hex1bWidget
    {
        protected override Hex1bWidget Build(CompositionContext ctx)
        {
            return ctx.VStack(v =>
            [
                v.Text("--- transcript line 1 ---"),
                v.Text("--- transcript line 2 ---"),
                v.Text(""),
                v.Separator(),
                v.SlashCommandPrompt(Commands).OnSubmit(SubmitSink.Get()),
            ]);
        }
    }

    /// <summary>
    /// Host with a focusable Button above the prompt — exercises the
    /// composite-level focus restoration in the default
    /// <see cref="Hex1bWidget.Build"/> reconciliation pipeline.
    /// </summary>
    private sealed record FocusableAboveHostWidget : Hex1bWidget
    {
        protected override Hex1bWidget Build(CompositionContext ctx)
        {
            return ctx.VStack(v =>
            [
                v.Button("dummy"),
                v.Separator(),
                v.SlashCommandPrompt(Commands).OnSubmit(SubmitSink.Get()),
            ]);
        }
    }

    /// <summary>
    /// Tiny static sink used to thread an OnSubmit callback through host
    /// widgets without making each host carry its own constructor parameter.
    /// AsyncLocal isolates concurrent runs.
    /// </summary>
    private static class SubmitSink
    {
        private static readonly AsyncLocal<Action<string>?> Current = new();

        public static void Set(Action<string>? handler) => Current.Value = handler;

        public static Action<string> Get() => Current.Value ?? (_ => { });
    }
}
