// Flow Reflow Probe — manual visual test for the host terminal's cursor-and-cell
// behaviour during horizontal resize. Used to validate whether the proposed
// "DSR-after-resize anchor" design for Hex1b.Flow is achievable on the user's
// terminal matrix.
//
// Run with:
//   dotnet run --project samples/FlowReflowProbe
//
// What to do:
//   1. Read the prediction printed at the top.
//   2. Resize the terminal horizontally — shrink several columns, then expand.
//   3. Visually check whether the cursor and the cell-positioned 'step'
//      content end up where the prediction said they should.
//   4. Press R to redraw at the new size, Q to quit.
//
// What this validates:
//   * Whether the host terminal moves the cursor with reflowed logical lines
//     (required for DSR-after-resize to recover the post-reflow tombstone
//     bottom row).
//   * Whether cell-positioned content stays at its absolute row (required for
//     the active step's render to land where we expect it to).

using Hex1b.Reflow;

const string Esc = "\x1b";
const string Reset = Esc + "[0m";
const string Bold = Esc + "[1m";
const string Dim = Esc + "[2m";
const string Cyan = Esc + "[36m";
const string Yellow = Esc + "[33m";
const string Green = Esc + "[32m";
const string Red = Esc + "[31m";
const string Magenta = Esc + "[35m";
const string ClearScreen = Esc + "[2J" + Esc + "[H";
const string ShowCursor = Esc + "[?25h";

var detected = AutoReflowStrategy.Instance.DetectedStrategy;
var detectedName = detected.GetType().Name;
var (preserveCursorRow, reflowsCursor, reflowsSavedCursor, comment) = ClassifyStrategy(detected);

Render();

while (true)
{
    var key = Console.ReadKey(intercept: true);
    if (key.Key == ConsoleKey.Q) break;
    if (key.Key == ConsoleKey.R)
    {
        Render();
    }
}

Console.Write(ShowCursor);
Console.WriteLine();
Console.WriteLine("Probe ended.");

void Render()
{
    var width = Math.Max(1, Console.WindowWidth);
    var height = Math.Max(1, Console.WindowHeight);

    Console.Write(ClearScreen);
    Console.Write(ShowCursor);

    // Cursor is now at row 1 col 1 (1-indexed). Track the absolute row we'd
    // be on if the terminal didn't scroll — we'll fold scrolling in at the
    // end via a single CUP, since CUU is bounded by the viewport top and
    // can't recover a row that scrolled off.
    int currentAbsoluteRow = 1;
    int? yellowAbsoluteRow = null;

    PrintHeader();
    PrintInstructions();
    PrintTombstone();
    PrintReferenceLine();
    PrintStepContent();
    PositionCursorAtAnchor();
    Console.Out.Flush();

    void TrackedWriteLine(string s)
    {
        Console.WriteLine(s);
        var visible = VisibleLength(s);
        var rows = Math.Max(1, (visible + width - 1) / width);
        currentAbsoluteRow += rows;
    }

    void TrackedBlankLine()
    {
        Console.WriteLine();
        currentAbsoluteRow += 1;
    }

    void PrintHeader()
    {
        TrackedWriteLine($"{Bold}{Cyan}=== Hex1b Flow Reflow Probe ==={Reset}");
        TrackedBlankLine();
        TrackedWriteLine($"  {Dim}Terminal:{Reset}        {detectedName}");
        TrackedWriteLine($"  {Dim}Window size:{Reset}     {width} x {height}");
        TrackedWriteLine($"  {Dim}TERM:{Reset}            {Environment.GetEnvironmentVariable("TERM") ?? "(unset)"}");
        TrackedWriteLine($"  {Dim}TERM_PROGRAM:{Reset}    {Environment.GetEnvironmentVariable("TERM_PROGRAM") ?? "(unset)"}");
        TrackedWriteLine($"  {Dim}WT_SESSION:{Reset}      {(Environment.GetEnvironmentVariable("WT_SESSION") is not null ? "set" : "(unset)")}");
        TrackedBlankLine();
        TrackedWriteLine($"  {Bold}Prediction (from Hex1b's reflow strategy):{Reset}");
        TrackedWriteLine($"    Reflows logical lines on horizontal resize:  {Color(reflowsCursor)}");
        TrackedWriteLine($"    Cursor follows reflowed content:             {Color(reflowsCursor)}");
        TrackedWriteLine($"    DECSC saved cursor is reflowed:              {Color(reflowsSavedCursor)}");
        TrackedWriteLine($"    preserveCursorRow (Kitty-style anchor):      {Color(preserveCursorRow)}");
        if (!string.IsNullOrEmpty(comment))
        {
            TrackedWriteLine($"    {Dim}Note:{Reset} {comment}");
        }
        TrackedBlankLine();
    }

    void PrintInstructions()
    {
        TrackedWriteLine($"{Bold}What to do:{Reset} resize horizontally, then press R to redraw, Q to quit.");
        TrackedWriteLine($"  {Green}OK{Reset} cursor follows the {Yellow}YELLOW{Reset} reference  -> terminal reflows cursor (good).");
        TrackedWriteLine($"  {Red}NO{Reset} cursor drifts away                  -> terminal does NOT reflow cursor.");
        TrackedBlankLine();
    }

    void PrintTombstone()
    {
        TrackedWriteLine($"{Cyan}--- BEGIN TOMBSTONE (one long logical line, should reflow) ---{Reset}");
        var filler = new string('=', 220);
        TrackedWriteLine($"{Cyan}[TOMBSTONE]{Reset} {filler} {Cyan}[/TOMBSTONE]{Reset}");
        TrackedWriteLine($"{Cyan}--- END TOMBSTONE ---{Reset}");
    }

    void PrintReferenceLine()
    {
        // Capture the row at which the YELLOW line will be printed BEFORE we
        // print it. This is the row the cursor should ultimately park on.
        yellowAbsoluteRow = currentAbsoluteRow;
        TrackedWriteLine($"{Yellow}>>> CURSOR-ANCHOR REFERENCE - cursor should park at col 0 of this YELLOW row <<<{Reset}");
    }

    void PrintStepContent()
    {
        for (int i = 1; i <= 3; i++)
        {
            TrackedWriteLine($"{Magenta}[step row {i}] cell-positioned content - should NOT reflow on resize{Reset}");
        }
    }

    void PositionCursorAtAnchor()
    {
        if (yellowAbsoluteRow is null)
        {
            return;
        }

        // Total content rows we asked the terminal to display past row 1.
        // If this exceeds the viewport, the terminal has scrolled and the
        // YELLOW line has effectively shifted up by that amount.
        var lastRowOccupied = currentAbsoluteRow - 1;
        var scrollAmount = Math.Max(0, lastRowOccupied - height);
        var visibleYellowRow = yellowAbsoluteRow.Value - scrollAmount;

        if (visibleYellowRow < 1)
        {
            // YELLOW line scrolled off. Surface a clear banner at the top so
            // the user understands the cursor anchor isn't observable here,
            // rather than letting CUU silently clamp to the wrong row.
            Console.Write($"{Esc}[1;1H");
            var banner = $"{Bold}{Red}[VIEWPORT TOO SMALL]{Reset} YELLOW reference scrolled off; resize taller/wider and press R.";
            Console.Write(banner);
            Console.Write($"{Esc}[K"); // clear to end of line so any tail of pre-existing content goes away
            return;
        }

        // CUP is 1-indexed; visibleYellowRow is already 1-indexed.
        Console.Write($"{Esc}[{visibleYellowRow};1H");
    }
}

static int VisibleLength(string s)
{
    var count = 0;
    var inEsc = false;
    foreach (var c in s)
    {
        if (inEsc)
        {
            // Final byte of a CSI / SGR sequence is in 0x40..0x7E; we end on it.
            if (c >= 0x40 && c <= 0x7E) inEsc = false;
            continue;
        }
        if (c == '\x1b')
        {
            inEsc = true;
            continue;
        }
        count++;
    }
    return count;
}

static string Color(bool value) => value
    ? $"{Esc}[32mYES{Esc}[0m"
    : $"{Esc}[31mNO{Esc}[0m";

static (bool preserveCursorRow, bool reflowsCursor, bool reflowsSavedCursor, string comment)
    ClassifyStrategy(ITerminalReflowProvider provider)
{
    if (provider is AutoReflowStrategy auto)
    {
        return ClassifyStrategy(auto.DetectedStrategy);
    }

    var typeName = provider.GetType().Name;
    return typeName switch
    {
        nameof(NoReflowStrategy) => (false, false, false,
            "No reflow at all - cursor and content both stay at absolute positions on resize."),

        nameof(WindowsTerminalReflowStrategy) => (false, true, false,
            "Bottom-fill reflow; cursor follows logical content. DECSC saved cursor becomes stale across reflow."),

        nameof(KittyReflowStrategy) => (true, true, false,
            "Cursor visual row is anchored (Kitty-style). DECSC saved cursor becomes stale."),

        nameof(GhosttyReflowStrategy) => (true, true, true,
            "Cursor visual row anchored AND DECSC saved cursor is reflowed."),

        nameof(VteReflowStrategy) => (true, true, true,
            "VTE-based (gnome-terminal, tilix, xfce4-terminal). Cursor anchored, DECSC reflowed."),

        nameof(WezTermReflowStrategy) => (true, true, false,
            "Cursor anchored. DECSC saved cursor becomes stale."),

        nameof(FootReflowStrategy) => (true, true, true,
            "Cursor anchored, DECSC reflowed."),

        nameof(ITerm2ReflowStrategy) => (false, true, false,
            "Bottom-fill reflow; cursor follows logical content. DECSC saved cursor becomes stale."),

        nameof(AlacrittyReflowStrategy) => (false, false, false,
            "Alacritty does not reflow logical lines on resize. Cursor and content stay at their absolute positions."),

        nameof(XtermReflowStrategy) => (false, false, false,
            "Xterm crops/extends; logical lines do not reflow. Cursor and content stay at their absolute positions."),

        _ => (false, false, false,
            $"Unknown strategy: {typeName}. Test empirically."),
    };
}

