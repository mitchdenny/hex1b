// KeyBindingTester
//
// A manual portability harness: a checklist of expected key/mouse combinations
// that ticks off as the user presses each one. Run this on every terminal you
// target — Windows Terminal, ConPTY, iTerm2, GNOME Terminal, xterm, ssh sessions
// from PuTTY/MobaXterm, etc. — to see which combos are actually deliverable on
// each platform. Useful for documenting platform support and for choosing default
// bindings that will work everywhere.
//
// Letter-key caveat: most terminals cannot distinguish Ctrl+Shift+letter from
// Ctrl+letter (Ctrl strips ASCII bit 6 of a letter; Shift gets dropped). Special
// keys (arrows, function keys, Home/End/PgUp/PgDn) and mouse buttons carry an
// explicit modifier code in their CSI sequences and deliver Ctrl+Shift reliably.
// Letter rows in the checklist are flagged with † as a reminder.
//
// Run with: dotnet run --project samples/KeyBindingTester

using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

// ── Test binding registry ─────────────────────────────────────────────────────

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
        // MouseStepBuilder does not currently support Alt — Alt+click on most terminals is consumed by the OS or terminal emulator.
        step.Action(_ => { fire(); return Task.CompletedTask; }, label);
    });

var bindingsList = new List<TestBinding>
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
    Key("Arrows", "Ctrl+→",             Hex1bKey.RightArrow, Hex1bModifiers.Control),
    Key("Arrows", "Shift+→",            Hex1bKey.RightArrow, Hex1bModifiers.Shift),
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
    Key("Editing", "Tab",               Hex1bKey.Tab),
    Key("Editing", "Shift+Tab",         Hex1bKey.Tab,        Hex1bModifiers.Shift),

    // Letter keys (caveat row — most terminals collapse Ctrl+Shift+letter into Ctrl+letter)
    LetterKey("Letters †", "Ctrl+A",          Hex1bKey.A,    Hex1bModifiers.Control),
    LetterKey("Letters †", "Ctrl+Shift+A",    Hex1bKey.A,    Hex1bModifiers.Control, Hex1bModifiers.Shift),
    LetterKey("Letters †", "Ctrl+Z",          Hex1bKey.Z,    Hex1bModifiers.Control),
    LetterKey("Letters †", "Ctrl+Shift+Z",    Hex1bKey.Z,    Hex1bModifiers.Control, Hex1bModifiers.Shift),

    // Mouse + modifier matrix
    Mouse("Mouse", "Click",                MouseButton.Left),
    Mouse("Mouse", "Ctrl+Click",           MouseButton.Left, Hex1bModifiers.Control),
    Mouse("Mouse", "Shift+Click",          MouseButton.Left, Hex1bModifiers.Shift),
    Mouse("Mouse", "Ctrl+Shift+Click",     MouseButton.Left, Hex1bModifiers.Control, Hex1bModifiers.Shift),
    Mouse("Mouse", "Right Click",          MouseButton.Right),
    Mouse("Mouse", "Scroll Up",            MouseButton.ScrollUp),
    Mouse("Mouse", "Scroll Down",          MouseButton.ScrollDown),
};

string? lastFired = null;

// ── Reset action ──────────────────────────────────────────────────────────────

void Reset()
{
    foreach (var b in bindingsList)
    {
        b.FireCount = 0;
        b.LastFired = null;
    }
    lastFired = "(reset)";
}

// ── Main UI ───────────────────────────────────────────────────────────────────

try
{
    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithHex1bApp((app, options) => ctx =>
        {
            var receivedCount = bindingsList.Count(b => b.FireCount > 0);
            var totalCount = bindingsList.Count;

            return ctx.VStack(root => [
                root.Border(
                    root.VStack(body =>
                    {
                        var rows = new List<Hex1bWidget>
                        {
                            body.Text($"Key Binding Tester — {receivedCount}/{totalCount} received"),
                            body.Text("Press each combination below to tick it off. R resets, Esc/Ctrl+C exits."),
                            body.Text("† = letter-key caveat: most terminals collapse Ctrl+Shift+letter into Ctrl+letter."),
                            body.Text(""),
                        };

                        string? lastCategory = null;
                        foreach (var b in bindingsList)
                        {
                            if (b.Category != lastCategory)
                            {
                                if (lastCategory is not null) rows.Add(body.Text(""));
                                rows.Add(body.Text($"── {b.Category} ──"));
                                lastCategory = b.Category;
                            }

                            var tick = b.FireCount > 0 ? "[✓]" : "[ ]";
                            var detail = b.FireCount > 0
                                ? $"  (received {b.FireCount}× last {(DateTime.UtcNow - b.LastFired!.Value).TotalSeconds:F1}s ago)"
                                : "";
                            rows.Add(body.Text($"{tick} {b.Label}{detail}"));
                        }

                        rows.Add(body.Text(""));
                        rows.Add(body.Text($"Last fired: {lastFired ?? "(none yet — press a key)"}"));

                        return rows.ToArray();
                    })
                ).Title("KeyBindingTester").Fill(),

                root.InfoBar([
                    "Press combos", "tick off",
                    "R", "reset",
                    "Esc/Ctrl+C", "exit",
                ]),
            ]).WithInputBindings(b =>
            {
                // Each test binding registers its key combo + handler that ticks itself off.
                foreach (var entry in bindingsList)
                {
                    var captured = entry; // for closure
                    captured.Register(b, () =>
                    {
                        captured.FireCount++;
                        captured.LastFired = DateTime.UtcNow;
                        lastFired = captured.Label;
                    });
                }

                // Reset: R (no modifiers).
                b.Key(Hex1bKey.R).Action(_ => { Reset(); return Task.CompletedTask; }, "Reset checklist");

                // Exit: Escape (Ctrl+C is handled by EnableDefaultCtrlCExit).
                b.Key(Hex1bKey.Escape).Action(ctx =>
                {
                    ctx.RequestStop();
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

Console.WriteLine("KeyBindingTester exited.");

sealed record TestBinding(string Category, string Label, Action<InputBindingsBuilder, Action> Register, bool LetterCaveat = false)
{
    public int FireCount { get; set; }
    public DateTime? LastFired { get; set; }
}
