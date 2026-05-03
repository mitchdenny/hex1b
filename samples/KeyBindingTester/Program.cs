// KeyBindingTester
//
// One-test-at-a-time portability checklist. The current expected combo is shown
// prominently; pressing it advances to the next. Press S to skip a combo your
// terminal can't deliver. At the end, click "Copy report" (or press C) to get a
// markdown report on your clipboard that you can paste into a GitHub issue.
//
// Why: Hex1b's unit tests prove the *internal* dispatch path is correct, but
// they can't prove that any *given terminal emulator* actually delivers the
// bytes the wiring expects. Some terminals (e.g. Windows Terminal) intercept
// combos like Ctrl+Shift+Up/Down/Home for their own scroll bindings; the OS
// or window manager may swallow others (Alt+Shift+arrow, etc.). The tester
// exists to gather portability data per-platform.
//
// Run with: dotnet run --project samples/KeyBindingTester

using System.Runtime.InteropServices;
using System.Text;
using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

// ── Test binding helpers (record + helpers below; types live at file end) ────

static TestBinding Key(string category, string label, Hex1bKey key, params Hex1bModifiers[] modifierFlags)
    => new(category, label, (b, fire) =>
    {
        var step = b.Key(key);
        if (modifierFlags.Contains(Hex1bModifiers.Control)) step = step.Ctrl();
        if (modifierFlags.Contains(Hex1bModifiers.Shift)) step = step.Shift();
        if (modifierFlags.Contains(Hex1bModifiers.Alt)) step = step.Alt();
        step.Action(_ => { fire(); return Task.CompletedTask; }, label);
    });

static TestBinding LetterKey(string category, string label, Hex1bKey key, params Hex1bModifiers[] modifierFlags)
    => Key(category, label, key, modifierFlags) with { LetterCaveat = true };

static TestBinding Mouse(string category, string label, MouseButton button, params Hex1bModifiers[] modifierFlags)
    => new(category, label, (b, fire) =>
    {
        var step = b.Mouse(button);
        if (modifierFlags.Contains(Hex1bModifiers.Control)) step = step.Ctrl();
        if (modifierFlags.Contains(Hex1bModifiers.Shift)) step = step.Shift();
        // MouseStepBuilder does not support Alt — most terminals consume Alt+click.
        step.Action(_ => { fire(); return Task.CompletedTask; }, label);
    });

var bindings = new List<TestBinding>
{
    // Arrow + modifier matrix (the case driving issue #293)
    Key("Arrows", "←",                  Hex1bKey.LeftArrow),
    Key("Arrows", "Shift+←",            Hex1bKey.LeftArrow,  Hex1bModifiers.Shift),
    Key("Arrows", "Ctrl+←",             Hex1bKey.LeftArrow,  Hex1bModifiers.Control),
    Key("Arrows", "Alt+←",              Hex1bKey.LeftArrow,  Hex1bModifiers.Alt),
    Key("Arrows", "Ctrl+Shift+←",       Hex1bKey.LeftArrow,  Hex1bModifiers.Control, Hex1bModifiers.Shift),
    Key("Arrows", "Alt+Shift+←",        Hex1bKey.LeftArrow,  Hex1bModifiers.Alt, Hex1bModifiers.Shift),
    Key("Arrows", "Alt+Ctrl+←",         Hex1bKey.LeftArrow,  Hex1bModifiers.Alt, Hex1bModifiers.Control),
    Key("Arrows", "Alt+Ctrl+Shift+←",   Hex1bKey.LeftArrow,  Hex1bModifiers.Alt, Hex1bModifiers.Control, Hex1bModifiers.Shift),
    Key("Arrows", "→",                  Hex1bKey.RightArrow),
    Key("Arrows", "Shift+→",            Hex1bKey.RightArrow, Hex1bModifiers.Shift),
    Key("Arrows", "Ctrl+→",             Hex1bKey.RightArrow, Hex1bModifiers.Control),
    Key("Arrows", "Ctrl+Shift+→",       Hex1bKey.RightArrow, Hex1bModifiers.Control, Hex1bModifiers.Shift),
    Key("Arrows", "↑",                  Hex1bKey.UpArrow),
    Key("Arrows", "Shift+↑",            Hex1bKey.UpArrow,    Hex1bModifiers.Shift),
    Key("Arrows", "Ctrl+↑",             Hex1bKey.UpArrow,    Hex1bModifiers.Control),
    Key("Arrows", "Ctrl+Shift+↑",       Hex1bKey.UpArrow,    Hex1bModifiers.Control, Hex1bModifiers.Shift),
    Key("Arrows", "↓",                  Hex1bKey.DownArrow),
    Key("Arrows", "Shift+↓",            Hex1bKey.DownArrow,  Hex1bModifiers.Shift),
    Key("Arrows", "Ctrl+↓",             Hex1bKey.DownArrow,  Hex1bModifiers.Control),
    Key("Arrows", "Ctrl+Shift+↓",       Hex1bKey.DownArrow,  Hex1bModifiers.Control, Hex1bModifiers.Shift),

    // Navigation block
    Key("Navigation", "Home",           Hex1bKey.Home),
    Key("Navigation", "Shift+Home",     Hex1bKey.Home,       Hex1bModifiers.Shift),
    Key("Navigation", "Ctrl+Home",      Hex1bKey.Home,       Hex1bModifiers.Control),
    Key("Navigation", "Ctrl+Shift+Home",Hex1bKey.Home,       Hex1bModifiers.Control, Hex1bModifiers.Shift),
    Key("Navigation", "End",            Hex1bKey.End),
    Key("Navigation", "Shift+End",      Hex1bKey.End,        Hex1bModifiers.Shift),
    Key("Navigation", "Ctrl+End",       Hex1bKey.End,        Hex1bModifiers.Control),
    Key("Navigation", "Ctrl+Shift+End", Hex1bKey.End,        Hex1bModifiers.Control, Hex1bModifiers.Shift),
    Key("Navigation", "PageUp",         Hex1bKey.PageUp),
    Key("Navigation", "Shift+PageUp",   Hex1bKey.PageUp,     Hex1bModifiers.Shift),
    Key("Navigation", "Ctrl+PageUp",    Hex1bKey.PageUp,     Hex1bModifiers.Control),
    Key("Navigation", "PageDown",       Hex1bKey.PageDown),
    Key("Navigation", "Shift+PageDown", Hex1bKey.PageDown,   Hex1bModifiers.Shift),
    Key("Navigation", "Ctrl+PageDown",  Hex1bKey.PageDown,   Hex1bModifiers.Control),

    // Function keys
    Key("Function", "F1",               Hex1bKey.F1),
    Key("Function", "Shift+F1",         Hex1bKey.F1,         Hex1bModifiers.Shift),
    Key("Function", "Ctrl+F1",          Hex1bKey.F1,         Hex1bModifiers.Control),
    Key("Function", "Ctrl+Shift+F1",    Hex1bKey.F1,         Hex1bModifiers.Control, Hex1bModifiers.Shift),
    Key("Function", "F12",              Hex1bKey.F12),
    Key("Function", "Shift+F12",        Hex1bKey.F12,        Hex1bModifiers.Shift),

    // Editing
    Key("Editing", "Backspace",         Hex1bKey.Backspace),
    Key("Editing", "Ctrl+Backspace",    Hex1bKey.Backspace,  Hex1bModifiers.Control),
    Key("Editing", "Delete",            Hex1bKey.Delete),
    Key("Editing", "Ctrl+Delete",       Hex1bKey.Delete,     Hex1bModifiers.Control),

    // Letter keys (caveat: most terminals collapse Ctrl+Shift+letter into Ctrl+letter)
    LetterKey("Letters",  "Ctrl+A",          Hex1bKey.A,    Hex1bModifiers.Control),
    LetterKey("Letters",  "Ctrl+Shift+A",    Hex1bKey.A,    Hex1bModifiers.Control, Hex1bModifiers.Shift),
    LetterKey("Letters",  "Ctrl+Z",          Hex1bKey.Z,    Hex1bModifiers.Control),
    LetterKey("Letters",  "Ctrl+Shift+Z",    Hex1bKey.Z,    Hex1bModifiers.Control, Hex1bModifiers.Shift),

    // Mouse + modifier matrix
    Mouse("Mouse", "Click",                MouseButton.Left),
    Mouse("Mouse", "Ctrl+Click",           MouseButton.Left, Hex1bModifiers.Control),
    Mouse("Mouse", "Shift+Click",          MouseButton.Left, Hex1bModifiers.Shift),
    Mouse("Mouse", "Ctrl+Shift+Click",     MouseButton.Left, Hex1bModifiers.Control, Hex1bModifiers.Shift),
    Mouse("Mouse", "Right Click",          MouseButton.Right),
    Mouse("Mouse", "Scroll Up",            MouseButton.ScrollUp),
    Mouse("Mouse", "Scroll Down",          MouseButton.ScrollDown),
};

// ── State ─────────────────────────────────────────────────────────────────────

const string PASS = "pass";
const string FAIL = "fail";
const string SKIP = "skip";
const string PENDING = "";

var status = new string[bindings.Count];
Array.Fill(status, PENDING);

int currentIndex = 0;
string? lastWrongPress = null;
string? clipboardStatus = null;

void Reset()
{
    Array.Fill(status, PENDING);
    currentIndex = 0;
    lastWrongPress = null;
    clipboardStatus = null;
}

void MarkAndAdvance(string state)
{
    if (currentIndex < bindings.Count)
    {
        status[currentIndex] = state;
        currentIndex++;
        lastWrongPress = null;
    }
}

string BuildReport()
{
    var sb = new StringBuilder();
    sb.AppendLine("## KeyBindingTester results");
    sb.AppendLine();
    sb.AppendLine($"- **OS:** {RuntimeInformation.OSDescription}");
    sb.AppendLine($"- **Architecture:** {RuntimeInformation.OSArchitecture}");
    sb.AppendLine($"- **Date (UTC):** {DateTime.UtcNow:yyyy-MM-dd HH:mm}");
    sb.AppendLine("- **Terminal:** _(fill in: Windows Terminal, ConPTY, iTerm2, GNOME Terminal, xterm, ssh from PuTTY/MobaXterm, etc.)_");
    sb.AppendLine("- **Hex1b version:** _(fill in if known)_");
    sb.AppendLine();

    var totalPass = status.Count(s => s == PASS);
    var totalFail = status.Count(s => s == FAIL);
    var totalSkip = status.Count(s => s == SKIP);
    var totalPending = status.Count(s => s == PENDING);
    sb.AppendLine($"**Result:** {totalPass} passed · {totalFail} failed · {totalSkip} skipped · {totalPending} not reached · {bindings.Count} total");
    sb.AppendLine();

    string? lastCat = null;
    for (int i = 0; i < bindings.Count; i++)
    {
        if (bindings[i].Category != lastCat)
        {
            sb.AppendLine();
            sb.AppendLine($"### {bindings[i].Category}");
            lastCat = bindings[i].Category;
        }

        var box = status[i] == PASS ? "[x]" : "[ ]";
        var note = status[i] switch
        {
            FAIL => " — ❌ **failed** (terminal/OS does not deliver this combo)",
            SKIP => " — ⏭️ skipped",
            PENDING => " — _not reached_",
            _ => ""
        };
        var caveat = bindings[i].LetterCaveat ? " †" : "";
        sb.AppendLine($"- {box} `{bindings[i].Label}`{caveat}{note}");
    }

    sb.AppendLine();
    sb.AppendLine("† = letter-key caveat: most terminals collapse `Ctrl+Shift+letter` into `Ctrl+letter` (Ctrl strips ASCII bit 6, Shift gets dropped). Failures here are usually a terminal limitation, not a Hex1b bug.");
    return sb.ToString();
}

// ── Main UI ───────────────────────────────────────────────────────────────────

try
{
    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithHex1bApp((app, options) => ctx =>
        {
            bool isDone = currentIndex >= bindings.Count;
            int totalPass = status.Count(s => s == PASS);
            int totalFail = status.Count(s => s == FAIL);
            int totalSkip = status.Count(s => s == SKIP);

            return ctx.VStack(root => [
                root.Border(
                    isDone
                        ? root.VStack(body => [
                            body.Text(""),
                            body.Text("    All tests complete!"),
                            body.Text(""),
                            body.Text($"    Passed:  {totalPass}"),
                            body.Text($"    Failed:  {totalFail}"),
                            body.Text($"    Skipped: {totalSkip}"),
                            body.Text($"    Total:   {bindings.Count}"),
                            body.Text(""),
                            body.HStack(buttons => [
                                buttons.Text("    "),
                                buttons.Button(" Copy report to clipboard ").OnClick(e =>
                                {
                                    e.Context.CopyToClipboard(BuildReport());
                                    clipboardStatus = $"Copied {DateTime.Now:HH:mm:ss}.";
                                }),
                                buttons.Text("  "),
                                buttons.Button(" Restart ").OnClick(_ => Reset()),
                            ]),
                            body.Text(""),
                            body.Text($"    {clipboardStatus ?? "Click \"Copy report\" or press C. The report is also printed to stdout on exit."}"),
                          ])
                        : root.VStack(body =>
                        {
                            var current = bindings[currentIndex];
                            var caveat = current.LetterCaveat
                                ? "  †  (terminal may collapse this — press F to fail or S to skip)"
                                : "";

                            return new Hex1bWidget[]
                            {
                                body.Text(""),
                                body.Text($"    Test {currentIndex + 1} of {bindings.Count}    ·    Category: {current.Category}"),
                                body.Text(""),
                                body.Text($"    Press:    {current.Label}{caveat}"),
                                body.Text(""),
                                body.Text($"    Progress: {totalPass} passed   {totalFail} failed   {totalSkip} skipped   {bindings.Count - currentIndex} remaining"),
                                body.Text(""),
                                body.Text(lastWrongPress is null
                                    ? "    "
                                    : $"    (You pressed {lastWrongPress} — still waiting for {current.Label}.)"),
                                body.Text(""),
                                body.Text("    Press F if this combo doesn't fire on your terminal, S to skip without testing."),
                            };
                        })
                ).Title("KeyBindingTester").Fill(),

                root.InfoBar([
                    "F", "fail",
                    "S", "skip",
                    "R", "reset",
                    "C", "copy report",
                    "Esc/Ctrl+C", "exit",
                ]),
            ]).WithInputBindings(b =>
            {
                // Each test binding registers its key combo + handler that ticks
                // off the matching test. Pressing the wrong combo (out of order)
                // updates the "last wrong press" indicator so the user can see
                // that input is being received.
                for (int i = 0; i < bindings.Count; i++)
                {
                    var idx = i;
                    bindings[i].Register(b, () =>
                    {
                        if (currentIndex == idx)
                        {
                            MarkAndAdvance(PASS);
                        }
                        else
                        {
                            lastWrongPress = bindings[idx].Label;
                        }
                    });
                }

                b.Key(Hex1bKey.F).Action(_ => { MarkAndAdvance(FAIL); return Task.CompletedTask; }, "Mark current as failed");
                b.Key(Hex1bKey.S).Action(_ => { MarkAndAdvance(SKIP); return Task.CompletedTask; }, "Skip current");
                b.Key(Hex1bKey.R).Action(_ => { Reset(); return Task.CompletedTask; }, "Reset");
                b.Key(Hex1bKey.C).Action(c =>
                {
                    c.CopyToClipboard(BuildReport());
                    clipboardStatus = $"Copied {DateTime.Now:HH:mm:ss}.";
                    return Task.CompletedTask;
                }, "Copy report");
                b.Key(Hex1bKey.Escape).Action(c =>
                {
                    c.RequestStop();
                    return Task.CompletedTask;
                }, "Exit");
            });
        })
        .WithMouse()
        .Build();

    await terminal.RunAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}

// Always print the report on exit so the user has a fallback if clipboard
// access (OSC 52) is disabled in their terminal.
Console.WriteLine();
Console.WriteLine("──────────────────────────────────────────────────────────────");
Console.WriteLine(" KeyBindingTester report (paste into a GitHub issue)");
Console.WriteLine("──────────────────────────────────────────────────────────────");
Console.WriteLine();
Console.WriteLine(BuildReport());

sealed record TestBinding(
    string Category,
    string Label,
    Action<InputBindingsBuilder, Action> Register,
    bool LetterCaveat = false);
