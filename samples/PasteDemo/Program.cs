using System.Diagnostics;
using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

// Streaming paste state
long totalChars = 0;
long totalLines = 0;
long totalChunks = 0;
double charsPerSec = 0;
string status = "idle";
string lastLine = "";
var recentLines = new List<string>();
var stopwatch = new Stopwatch();
bool pasteComplete = false;
int pasteCount = 0;

// Build a simple progress bar string
static string ProgressBar(long current, long max, int width = 30)
{
    if (max <= 0) return new string('░', width);
    var ratio = Math.Min(1.0, (double)current / max);
    var filled = (int)(ratio * width);
    return new string('█', filled) + new string('░', width - filled);
}

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        var elapsed = stopwatch.IsRunning ? stopwatch.Elapsed : TimeSpan.Zero;

        return ctx.VStack(v => [
            v.Text("╔══════════════════════════════════════════════════════╗"),
            v.Text("║         Streaming Paste Demo                        ║"),
            v.Text("╚══════════════════════════════════════════════════════╝"),
            v.Text(""),
            v.Text(" Tab into the paste zone below, then paste a huge file."),
            v.Text(" The handler processes chunks with a simulated delay,"),
            v.Text(" showing live stats as data streams in."),
            v.Text(" Press Escape to cancel a paste in progress."),
            v.Text(""),
            v.Separator(),

            // Live stats
            v.Text($"  Status:       {status}"),
            v.Text($"  Pastes:       {pasteCount}"),
            v.Text($"  Characters:   {totalChars:N0}"),
            v.Text($"  Lines:        {totalLines:N0}"),
            v.Text($"  Chunks:       {totalChunks:N0}"),
            v.Text($"  Throughput:   {charsPerSec:N0} chars/sec"),
            v.Text($"  Elapsed:      {elapsed.TotalSeconds:F1}s"),
            v.Text(""),

            // Progress indicator
            v.Text($"  {ProgressBar(totalChars, totalChars > 0 ? totalChars + (status == "streaming..." ? 10000 : 0) : 0)}"),
            v.Text(""),
            v.Separator(),

            // Paste zone
            v.Text(" Paste zone (Tab here first):"),
            v.Pastable(
                v.Interactable(ic => ic.Border(v2 => [
                    v2.Text(status == "idle"
                        ? "  Waiting for paste... (Tab here, then Ctrl+V)"
                        : status == "streaming..."
                            ? $"  ▶ Receiving: {totalChars:N0} chars | {totalLines:N0} lines | {totalChunks:N0} chunks"
                            : $"  ✓ Complete: {totalChars:N0} chars, {totalLines:N0} lines in {elapsed.TotalSeconds:F1}s"),
                    v2.Text(""),
                    v2.Text("  Last 8 lines received:"),
                    .. (recentLines.Count > 0
                        ? recentLines.TakeLast(8).Select((l, i) =>
                        {
                            var display = l.Length > 60 ? l[..57] + "..." : l;
                            return v2.Text($"    {totalLines - recentLines.Count + i + 1,6}: {display}");
                        }).ToArray()
                        : [v2.Text("    (nothing yet)")]),
                ]))
            )
            .OnPaste(async paste =>
            {
                // Reset state for new paste
                totalChars = 0;
                totalLines = 0;
                totalChunks = 0;
                charsPerSec = 0;
                pasteComplete = false;
                recentLines.Clear();
                status = "streaming...";
                pasteCount++;
                stopwatch.Restart();
                app.Invalidate();

                try
                {
                    await foreach (var chunk in paste.ReadChunksAsync())
                    {
                        totalChunks++;
                        totalChars += chunk.Length;

                        // Count lines in this chunk
                        foreach (var ch in chunk)
                        {
                            if (ch == '\n') totalLines++;
                        }

                        // Track recent lines for display
                        var lines = chunk.Split('\n');
                        foreach (var line in lines)
                        {
                            if (!string.IsNullOrEmpty(line))
                            {
                                var trimmed = line.TrimEnd('\r');
                                if (trimmed.Length > 0)
                                    recentLines.Add(trimmed);
                            }
                        }
                        // Keep only last 20 lines in memory
                        if (recentLines.Count > 20)
                            recentLines.RemoveRange(0, recentLines.Count - 20);

                        // Calculate throughput
                        var secs = stopwatch.Elapsed.TotalSeconds;
                        charsPerSec = secs > 0 ? totalChars / secs : 0;

                        // Simulated slow processing — 15ms per chunk
                        await Task.Delay(15);

                        app.Invalidate();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Paste was cancelled (Escape key)
                }

                stopwatch.Stop();
                var finalSecs = stopwatch.Elapsed.TotalSeconds;
                charsPerSec = finalSecs > 0 ? totalChars / finalSecs : 0;
                pasteComplete = true;
                status = paste.IsCancelled
                    ? $"cancelled after {totalChars:N0} chars"
                    : "done";
                app.Invalidate();
            }),

            v.Text(""),
            v.Separator(),
            v.Text(" Press Ctrl+C to exit."),
        ]);
    })
    .Build();

await terminal.RunAsync();
