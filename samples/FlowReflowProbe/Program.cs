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
    var width = Console.WindowWidth;

    Console.Write(ClearScreen);
    Console.Write(ShowCursor);

    PrintHeader();
    PrintInstructions();
    PrintTombstone();
    PrintReferenceLine();
    PrintStepContent();
    PositionCursorAtAnchor();
    Console.Out.Flush();

    void PrintHeader()
    {
        Console.WriteLine($"{Bold}{Cyan}=== Hex1b Flow Reflow Probe ==={Reset}");
        Console.WriteLine();
        Console.WriteLine($"  {Dim}Terminal:{Reset}        {detectedName}");
        Console.WriteLine($"  {Dim}Window size:{Reset}     {width} x {Console.WindowHeight}");
        Console.WriteLine($"  {Dim}TERM:{Reset}            {Environment.GetEnvironmentVariable("TERM") ?? "(unset)"}");
        Console.WriteLine($"  {Dim}TERM_PROGRAM:{Reset}    {Environment.GetEnvironmentVariable("TERM_PROGRAM") ?? "(unset)"}");
        Console.WriteLine($"  {Dim}WT_SESSION:{Reset}      {(Environment.GetEnvironmentVariable("WT_SESSION") is not null ? "set" : "(unset)")}");
        Console.WriteLine();
        Console.WriteLine($"  {Bold}Prediction (from Hex1b's reflow strategy):{Reset}");
        Console.WriteLine($"    Reflows logical lines on horizontal resize:  {Color(reflowsCursor)}");
        Console.WriteLine($"    Cursor follows reflowed content:             {Color(reflowsCursor)}");
        Console.WriteLine($"    DECSC saved cursor is reflowed:              {Color(reflowsSavedCursor)}");
        Console.WriteLine($"    preserveCursorRow (Kitty-style anchor):      {Color(preserveCursorRow)}");
        if (!string.IsNullOrEmpty(comment))
        {
            Console.WriteLine($"    {Dim}Note:{Reset} {comment}");
        }
        Console.WriteLine();
    }

    void PrintInstructions()
    {
        Console.WriteLine($"{Bold}What to do:{Reset}");
        Console.WriteLine($"  1. Look at where the cursor lands (block at start of the {Yellow}YELLOW{Reset} reference line).");
        Console.WriteLine($"  2. Resize the terminal horizontally - shrink several columns, then expand back.");
        Console.WriteLine($"  3. Observe:");
        Console.WriteLine($"     {Green}OK{Reset} cursor moves with the {Yellow}YELLOW{Reset} reference line  -> terminal reflows cursor (good)");
        Console.WriteLine($"     {Red}NO{Reset} cursor drifts away from the {Yellow}YELLOW{Reset} line          -> terminal does NOT reflow cursor (problem)");
        Console.WriteLine($"     {Green}OK{Reset} {Magenta}MAGENTA{Reset} '[step row N]' lines stay at their rows -> cell content does not reflow (good)");
        Console.WriteLine($"     {Red}NO{Reset} {Magenta}MAGENTA{Reset} lines wrap or relocate                  -> cell content also reflows (problem)");
        Console.WriteLine($"  4. Press {Bold}R{Reset} to redraw with the new size. Press {Bold}Q{Reset} to quit.");
        Console.WriteLine();
    }

    void PrintTombstone()
    {
        Console.WriteLine($"{Cyan}--- BEGIN TOMBSTONE (one long logical line, should reflow with terminal width) ---{Reset}");

        var filler = new string('=', 220);
        // Single logical line ending in \n. The host terminal owns wrapping.
        Console.WriteLine($"{Cyan}[TOMBSTONE]{Reset} {filler} {Cyan}[/TOMBSTONE]{Reset}");
        Console.WriteLine($"{Cyan}--- END TOMBSTONE ---{Reset}");
    }

    void PrintReferenceLine()
    {
        // Logical line - should reflow alongside the tombstone.
        // We want the cursor to land at column 0 of THIS row when no reflow has
        // happened, and to follow the reflow of THIS row as the user resizes.
        Console.WriteLine($"{Yellow}>>> CURSOR-ANCHOR REFERENCE - the cursor should sit at col 0 of this YELLOW row <<<{Reset}");
    }

    void PrintStepContent()
    {
        // 'Step' content: cell-positioned, like an active flow step would be.
        for (int i = 1; i <= 5; i++)
        {
            Console.WriteLine($"{Magenta}[step row {i}] cell-positioned content - should NOT reflow on resize{Reset}");
        }
        Console.WriteLine();
        Console.WriteLine($"{Dim}(cursor will return to col 0 of the YELLOW reference line above){Reset}");
    }

    void PositionCursorAtAnchor()
    {
        // Move the cursor back to column 0 of the YELLOW reference line.
        // We've just printed (relative to the YELLOW line):
        //   YELLOW line                   (1)
        //   step row 1..5                 (5)
        //   blank line                    (1)
        //   "(cursor will return ...)"    (1)
        // = 8 rows past the YELLOW line.
        // CUU is bounded to the top of the viewport, so even if reflow has
        // already extended the printed content slightly, it can't go below 0.
        const int RowsToMoveUp = 8;
        Console.Write($"{Esc}[{RowsToMoveUp}A");
        Console.Write("\r");
    }
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
