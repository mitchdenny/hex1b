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
var recentLines = new List<string>();
var stopwatch = new Stopwatch();
int pasteCount = 0;

// Spinner frames for activity indicator
var spinnerFrames = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };

// Format byte-like counts with K/M suffixes
static string FormatCount(long n) => n switch
{
    >= 1_000_000 => $"{n / 1_000_000.0:F1}M",
    >= 1_000 => $"{n / 1_000.0:F1}K",
    _ => n.ToString()
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        var elapsed = stopwatch.IsRunning ? stopwatch.Elapsed : TimeSpan.Zero;
        var spinner = spinnerFrames[(int)(elapsed.TotalMilliseconds / 80) % spinnerFrames.Length];
        var isStreaming = status == "streaming";

        return ctx.VStack(v => [
            v.Text("╔══════════════════════════════════════════════════════╗"),
            v.Text("║         Streaming Paste Demo                        ║"),
            v.Text("╚══════════════════════════════════════════════════════╝"),
            v.Text(""),
            v.Text(" Tab into the paste zone below, then paste a huge file."),
            v.Text(" Press Escape to cancel a paste in progress."),
            v.Text(""),
            v.Separator(),

            // Live stats — spinner animates while streaming
            v.Text(isStreaming
                ? $"  {spinner} Status:       RECEIVING..."
                : $"    Status:       {status}"),
            v.Text($"    Pastes:       {pasteCount}"),
            v.Text($"    Characters:   {FormatCount(totalChars)}"),
            v.Text($"    Lines:        {FormatCount(totalLines)}"),
            v.Text($"    Chunks:       {FormatCount(totalChunks)}"),
            v.Text($"    Throughput:   {FormatCount((long)charsPerSec)}/sec"),
            v.Text($"    Elapsed:      {elapsed.TotalSeconds:F1}s"),
            v.Text(""),
            v.Separator(),

            // Paste zone
            v.Text(" Paste zone (Tab here first):"),
            v.Pastable(
                v.Interactable(ic => ic.Border(v2 => [
                    v2.Text(isStreaming
                        ? $"  {spinner} Receiving: {FormatCount(totalChars)} chars │ {FormatCount(totalLines)} lines │ {FormatCount(totalChunks)} chunks │ {elapsed.TotalSeconds:F1}s"
                        : status == "idle"
                            ? "    Waiting for paste... (Tab here, then Ctrl+V)"
                            : $"    {status}"),
                    v2.Text(""),
                    v2.Text("  Recent lines:"),
                    .. (recentLines.Count > 0
                        ? recentLines.TakeLast(8).Select((l, i) =>
                        {
                            var lineNum = totalLines - recentLines.Count + i + 1;
                            var display = l.Length > 60 ? l[..57] + "..." : l;
                            return v2.Text($"  {lineNum,7} │ {display}");
                        }).ToArray()
                        : [v2.Text("          │ (nothing yet)")]),
                ]))
            )
            .OnPaste(async e =>
            {
                var paste = e.Paste;
                // Reset state for new paste
                totalChars = 0;
                totalLines = 0;
                totalChunks = 0;
                charsPerSec = 0;
                recentLines.Clear();
                status = "streaming";
                pasteCount++;
                stopwatch.Restart();
                app.Invalidate();

                // Periodic UI refresh so spinner animates between chunks
                using var refreshCts = new CancellationTokenSource();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!refreshCts.Token.IsCancellationRequested)
                        {
                            await Task.Delay(80, refreshCts.Token);
                            app.Invalidate();
                        }
                    }
                    catch (OperationCanceledException) { }
                });

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
                            var trimmed = line.TrimEnd('\r');
                            if (trimmed.Length > 0)
                                recentLines.Add(trimmed);
                        }
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

                refreshCts.Cancel();

                stopwatch.Stop();
                var finalSecs = stopwatch.Elapsed.TotalSeconds;
                charsPerSec = finalSecs > 0 ? totalChars / finalSecs : 0;
                status = paste.IsCancelled
                    ? $"✗ Cancelled after {FormatCount(totalChars)} chars in {finalSecs:F1}s"
                    : $"✓ Done: {FormatCount(totalChars)} chars, {FormatCount(totalLines)} lines in {finalSecs:F1}s ({FormatCount((long)charsPerSec)}/sec)";
                app.Invalidate();
            }),

            v.Text(""),
            v.Separator(),
            v.Text(" Press Ctrl+C to exit."),
        ]);
    })
    .Build();

await terminal.RunAsync();
