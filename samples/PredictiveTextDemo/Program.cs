using Hex1b;
using Hex1b.Widgets;

// A small in-memory dictionary of "next-character" completions. The predictor
// picks the longest entry whose key is a prefix of the current text and
// returns the suffix as the inline suggestion.
var phrases = new[]
{
    "the quick brown fox jumps over the lazy dog",
    "hello world",
    "hex1b is a terminal ui library for .net",
    "predictive text saves keystrokes",
    "this is a sample of inline completions",
    "type any letter to see suggestions appear",
};

var lastSubmitted = "(none)";
var submissionCount = 0;
var stats = new Stats();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(ctx => ctx.VStack(v => [
        v.Text("Predictive Text Demo"),
        v.Separator(),
        v.Text(""),

        v.Text("Start typing — a suggestion appears in dim text after the cursor."),
        v.Text("RightArrow accepts the suggestion. Escape dismisses it."),
        v.Text(""),

        v.Border(b => [
            b.Text("Instant predictor (no debounce):"),
            b.TextBox()
                .Predict(async (text, ct) =>
                {
                    Interlocked.Increment(ref stats.InstantCalls);
                    // Synthesize an async hop to mirror what a real predictor
                    // would do (e.g., calling out to an LLM).
                    await Task.Yield();
                    return Predict(phrases, text);
                })
                .OnSubmit(e =>
                {
                    lastSubmitted = e.Text;
                    Interlocked.Increment(ref submissionCount);
                }),
        ]).Title($"Instant ({stats.InstantCalls} calls)"),

        v.Text(""),

        v.Border(b => [
            b.Text("Debounced predictor (150 ms — simulates a slow backend):"),
            b.TextBox()
                .Predict(async (text, ct) =>
                {
                    Interlocked.Increment(ref stats.DebouncedCalls);
                    // Pretend the predictor is expensive.
                    await Task.Delay(40, ct);
                    return Predict(phrases, text);
                }, TimeSpan.FromMilliseconds(150))
                .OnSubmit(e =>
                {
                    lastSubmitted = e.Text;
                    Interlocked.Increment(ref submissionCount);
                }),
        ]).Title($"Debounced 150 ms ({stats.DebouncedCalls} calls)"),

        v.Text(""),
        v.Separator(),
        v.Text($"Last submitted: {lastSubmitted}"),
        v.Text($"Total submissions: {submissionCount}"),
        v.Text(""),
        v.Text("Tab moves between text boxes. Press Ctrl+C to exit."),
    ]))
    .WithMouse()
    .Build();

await terminal.RunAsync();

static string? Predict(IEnumerable<string> dictionary, string input)
{
    if (string.IsNullOrEmpty(input)) return null;

    // Find the longest dictionary entry that starts with the current input.
    string? best = null;
    foreach (var entry in dictionary)
    {
        if (entry.Length <= input.Length) continue;
        if (!entry.StartsWith(input, StringComparison.OrdinalIgnoreCase)) continue;
        if (best is null || entry.Length > best.Length) best = entry;
    }

    return best is null ? null : best[input.Length..];
}

internal sealed class Stats
{
    public int InstantCalls;
    public int DebouncedCalls;
}
